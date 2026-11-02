using System.Security.Cryptography;
using System.Text;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;

namespace EbayClone.Security.SqlTests;

[Category("SqlMigration")]
public class SensitiveDataMigrationTests
{
    private MsSqlContainer _container = null!;
    private ApplicationDbContext _db = null!;
    private AesEncryptionService _crypto = null!;
    private const string Previous = "20260324181656_RefactorReturnRequestStatus";

    [OneTimeSetUp]
    public async Task StartSql()
    {
        _container = new MsSqlBuilder().Build();
        await _container.StartAsync();
    }

    [SetUp]
    public async Task CreateDatabase()
    {
        var name = "W701_" + Guid.NewGuid().ToString("N");
        await _container.ExecScriptAsync($"CREATE DATABASE [{name}]");
        var connection = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = name };
        _db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options);
        _crypto = new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:EncryptionKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()),
            ["Security:LegacyEncryptionKey"] = "test-legacy-only"
        }).Build());
    }

    [TearDown] public async Task CloseDatabase() => await _db.DisposeAsync();
    [OneTimeTearDown] public async Task StopSql() { if (_container != null) await _container.DisposeAsync(); }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Migrate_FreshAndPreviousSchema_CreateManagedAuditTable(bool upgrade)
    {
        if (upgrade) await _db.GetService<IMigrator>().MigrateAsync(Previous);
        await _db.Database.MigrateAsync();
        Assert.That(await _db.Database.GetPendingMigrationsAsync(), Is.Empty);
        _db.SensitiveDataLogs.Add(new SensitiveDataLog { ActorId = 0, ActorRole = "System", Action = "Test", TargetEntity = "User",
            EncryptedData = _crypto.Encrypt("{}"), DataHash = _crypto.ComputeHmac("{}") });
        await _db.SaveChangesAsync();
        Assert.That(await _db.SensitiveDataLogs.CountAsync(), Is.EqualTo(1));
        Assert.That((await new SensitiveDataMigrationService(_db, _crypto).RunAsync(false)).NeedsConversion, Is.False);
    }

    private async Task SeedLegacy()
    {
        await _db.GetService<IMigrator>().MigrateAsync(Previous);
        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE SensitiveDataLogs (
                Id INT IDENTITY(1,1) PRIMARY KEY, ActorId INT NOT NULL,
                ActorUsername NVARCHAR(256) NULL, ActorRole NVARCHAR(50) NULL,
                Action NVARCHAR(100) NOT NULL, TargetEntity NVARCHAR(100) NOT NULL,
                TargetId NVARCHAR(100) NULL, DataClassification NVARCHAR(50) NOT NULL,
                EncryptedData NVARCHAR(MAX) NOT NULL, EncryptionAlgorithm NVARCHAR(50) NOT NULL,
                KeyIdentifier NVARCHAR(50) NOT NULL, DataHash NVARCHAR(256) NULL,
                IpAddress NVARCHAR(50) NULL, UserAgent NVARCHAR(500) NULL,
                CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                CreatedBy NVARCHAR(256) NULL, LastModified DATETIMEOFFSET NULL, LastModifiedBy NVARCHAR(256) NULL
            );
            """);
        var key = SHA256.HashData(Encoding.UTF8.GetBytes("test-legacy-only"));
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        const string plain = "{\"cccd\":\"001234567890\"}";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(plain)).ToArray();
        var cipher = Convert.ToBase64String(aes.IV.Concat(aes.EncryptCbc(bytes, aes.IV)).ToArray());
        var hash = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(plain)));
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO SensitiveDataLogs (ActorId, Action, TargetEntity, DataClassification, EncryptedData, EncryptionAlgorithm, KeyIdentifier, DataHash)
            VALUES (0, 'LegacyRead', 'User', 'PII', {cipher}, 'AES-256-CBC', 'v1', {hash});
            """);
        await _db.Database.MigrateAsync();
        _db.Users.Add(new User { Username = "fixture", Role = "Seller", CCCD = "001234567890",
            BankAccountMock = "{\"accountNumber\":\"001122\"}", VerificationDocuments = "{\"cccd\":\"001234567890\"}" });
        await _db.SaveChangesAsync();
        var sellerId = (await _db.Users.SingleAsync()).Id;
        _db.PayoutTransactions.Add(new PayoutTransaction { SellerId = sellerId, Amount = 20, Status = "Failed", BankSnapshot = "{\"accountNumber\":\"001122\"}" });
        _db.WithdrawalRequests.Add(new WithdrawalRequest { SellerId = sellerId, Amount = 10, BankName = "VCB", BankAccountNumber = "001122", BankAccountName = "Fixture" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [Test]
    public async Task AdoptLegacyTable_DryRunDoesNotWrite_ApplyPreservesRowsAndIsIdempotent()
    {
        await SeedLegacy();
        var service = new SensitiveDataMigrationService(_db, _crypto);
        var before = (await _db.SensitiveDataLogs.AsNoTracking().SingleAsync()).EncryptedData;
        var dry = await service.RunAsync(false);
        Assert.That(dry.NeedsConversion, Is.True);
        Assert.That((await _db.Users.AsNoTracking().SingleAsync()).CCCD, Is.EqualTo("001234567890"));
        Assert.That((await _db.SensitiveDataLogs.AsNoTracking().SingleAsync()).EncryptedData, Is.EqualTo(before));
        await service.RunAsync(true);
        _db.ChangeTracker.Clear();
        var user = await _db.Users.SingleAsync();
        Assert.That(user.CCCD, Does.StartWith("gcm:v1:"));
        Assert.That(_crypto.Decrypt(user.CCCD!), Is.EqualTo("001234567890"));
        Assert.That(user.CccdLookupHash, Is.EqualTo(_crypto.ComputeHmac("cccd:001234567890")));
        Assert.That(_crypto.Decrypt(user.BankAccountMock!), Is.EqualTo("{\"accountNumber\":\"001122\"}"));
        Assert.That(_crypto.Decrypt((await _db.WithdrawalRequests.SingleAsync()).BankAccountNumber!), Is.EqualTo("001122"));
        Assert.That(_crypto.Decrypt((await _db.PayoutTransactions.SingleAsync()).BankSnapshot!), Is.EqualTo("{\"accountNumber\":\"001122\"}"));
        var log = await _db.SensitiveDataLogs.SingleAsync();
        Assert.That(log.Id, Is.EqualTo(1));
        Assert.That(log.EncryptionAlgorithm, Is.EqualTo("AES-256-GCM"));
        Assert.That(_crypto.Decrypt(log.EncryptedData), Is.EqualTo("{\"cccd\":\"001234567890\"}"));
        Assert.That((await service.RunAsync(true)).NeedsConversion, Is.False);
        Assert.That(await _db.SensitiveDataLogs.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task BadLegacyHash_AbortsWholeConversion_AndLeavesPlaintextUntouched()
    {
        await SeedLegacy();
        await _db.Database.ExecuteSqlRawAsync("UPDATE SensitiveDataLogs SET DataHash = 'bad'");
        Assert.CatchAsync<CryptographicException>(() => new SensitiveDataMigrationService(_db, _crypto).RunAsync(true));
        _db.ChangeTracker.Clear();
        Assert.That((await _db.Users.SingleAsync()).CCCD, Is.EqualTo("001234567890"));
        Assert.That((await _db.WithdrawalRequests.SingleAsync()).BankAccountNumber, Is.EqualTo("001122"));
        Assert.That((await _db.SensitiveDataLogs.SingleAsync()).EncryptionAlgorithm, Is.EqualTo("AES-256-CBC"));
    }

    [Test]
    public async Task SaveFailure_RollsBackPiiAndAuditConversion()
    {
        await SeedLegacy();
        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectSensitiveConversion ON SensitiveDataLogs AFTER UPDATE AS
            BEGIN
                THROW 51001, 'Fixture stops conversion', 1;
            END;
            """);
        Assert.ThrowsAsync<DbUpdateException>(() => new SensitiveDataMigrationService(_db, _crypto).RunAsync(true));
        _db.ChangeTracker.Clear();
        Assert.That((await _db.Users.SingleAsync()).CCCD, Is.EqualTo("001234567890"));
        Assert.That((await _db.WithdrawalRequests.SingleAsync()).BankAccountNumber, Is.EqualTo("001122"));
        Assert.That((await _db.SensitiveDataLogs.SingleAsync()).EncryptionAlgorithm, Is.EqualTo("AES-256-CBC"));
        Assert.That(await _db.SensitiveDataLogs.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task SeparateApplicationProcesses_SameKey_DecryptPersistedSqlData()
    {
        await SeedLegacy();
        await new SensitiveDataMigrationService(_db, _crypto).RunAsync(true);
        var before = await _db.Users.AsNoTracking().Select(x => x.CCCD).SingleAsync();
        var web = typeof(EbayClone.Web.Endpoints.Users).Assembly.Location;
        var processIds = new List<int>();
        for (var instance = 0; instance < 2; instance++)
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(web)!
            };
            start.ArgumentList.Add(web);
            start.ArgumentList.Add("--migrate-sensitive-data");
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
            start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
            start.Environment["ConnectionStrings__MyCnn"] = _db.Database.GetConnectionString();
            start.Environment["Security__EncryptionKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());
            start.Environment["Security__LegacyEncryptionKey"] = "test-legacy-only";
            start.Environment["Jwt__Key"] = "w701-isolated-process-fixture-signing-key";
            start.Environment["AZURE_KEY_VAULT_ENDPOINT"] = "";
            start.Environment["Redis__ConnectionString"] = "";
            start.Environment["Logging__LogLevel__Default"] = "Information";
            using var process = System.Diagnostics.Process.Start(start)!;
            processIds.Add(process.Id);
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { process.Kill(entireProcessTree: true); throw; }
            var text = await output;
            await errors; // Never publish raw subprocess output or connection configuration.
            Assert.That(process.ExitCode, Is.Zero, "Isolated application dry-run must succeed.");
            Assert.That(text, Does.Contain("PiiFields=0, Logs=0, LookupHashes=0"),
                "Each new process must validate persisted ciphertext with the same key.");
        }
        Assert.That(processIds.Distinct().Count(), Is.EqualTo(2));
        Assert.That(await _db.Users.AsNoTracking().Select(x => x.CCCD).SingleAsync(), Is.EqualTo(before));
        Assert.That(_crypto.Decrypt(before!), Is.EqualTo("001234567890"));
    }
}
