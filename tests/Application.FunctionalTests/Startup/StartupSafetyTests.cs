using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Data.Seeders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace EbayClone.Security.StartupTests;

public class StartupSafetyTests
{
    private MsSqlContainer _container = null!;
    private string _connection = null!;
    [OneTimeSetUp] public async Task Start() { _container = new MsSqlBuilder().Build(); await _container.StartAsync(); }
    [OneTimeTearDown] public async Task Stop() { if (_container != null) await _container.DisposeAsync(); }
    [SetUp]
    public async Task Create()
    {
        var name = "StartupSafety_" + Guid.NewGuid().ToString("N");
        await _container.ExecScriptAsync($"CREATE DATABASE [{name}]");
        _connection = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = name }.ConnectionString;
        await using var db = Database(); await db.Database.MigrateAsync();
    }
    private ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options);
    private WebApplicationBuilder Builder(string? mode = null, string environment = "Development", bool jobs = false, bool migrate = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MyCnn"] = _connection,
            ["Jwt:Key"] = new string('t', 64),
            ["Security:EncryptionKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()),
            ["BackgroundJobs:Enabled"] = jobs.ToString(),
            ["DatabaseInitialization:RunMigrationsOnStartup"] = migrate.ToString(),
            ["Seeding:Mode"] = mode
        });
        builder.AddApplicationServices(); builder.AddInfrastructureServices(); builder.AddWebServices();
        return builder;
    }
    [Test]
    public async Task DefaultStartupDoesNotWriteOrMigrate()
    {
        await using (var db = Database()) { db.AdminRoles.Add(new AdminRole { RoleName = "Support", Permissions = "[]", Description = "custom" }); db.Users.Add(new User { Username = "existing" }); await db.SaveChangesAsync(); var user = await db.Users.SingleAsync(); db.SellerWallets.Add(new SellerWallet { SellerId = user.Id, PendingBalance = 27, DisputedBalance = 4 }); await db.SaveChangesAsync(); }
        // READ_ONLY makes any accidental migration, DML or seeding fail at SQL Server.
        await _container.ExecScriptAsync($"ALTER DATABASE [{new SqlConnectionStringBuilder(_connection).InitialCatalog}] SET READ_ONLY WITH ROLLBACK IMMEDIATE;");
        await using var app = Builder().Build(); await app.InitialiseDatabaseAsync();
        await using var check = Database();
        Assert.That(await check.AdminRoles.CountAsync(), Is.EqualTo(1));
        Assert.That((await check.AdminRoles.SingleAsync()).Permissions, Is.EqualTo("[]"));
        Assert.That(await check.Set<EbayClone.Infrastructure.Identity.ApplicationUser>().CountAsync(), Is.Zero);
        Assert.That((await check.Users.SingleAsync()).Username, Is.EqualTo("existing"));
        Assert.That((await check.SellerWallets.SingleAsync()).PendingBalance, Is.EqualTo(27));
        Assert.That((await check.SellerWallets.SingleAsync()).DisputedBalance, Is.EqualTo(4));
        var readOnly = await _container.ExecScriptAsync($"IF DATABASEPROPERTYEX('{new SqlConnectionStringBuilder(_connection).InitialCatalog}', 'Updateability') <> 'READ_ONLY' THROW 51000, 'Not read only', 1;");
        Assert.That(readOnly.ExitCode, Is.Zero);
    }
    [Test]
    public async Task SystemAddsMissingRolesOnly()
    {
        await using (var db = Database()) { db.AdminRoles.Add(new AdminRole { RoleName = "Support", Permissions = "[]", Description = "custom" }); await db.SaveChangesAsync(); }
        await using var app = Builder("System").Build(); await app.InitialiseDatabaseAsync(); await app.InitialiseDatabaseAsync();
        await using var check = Database();
        Assert.That(await check.AdminRoles.CountAsync(), Is.EqualTo(3));
        Assert.That((await check.AdminRoles.SingleAsync(x => x.RoleName == "Support")).Description, Is.EqualTo("custom"));
        Assert.That((await check.AdminRoles.SingleAsync(x => x.RoleName == "Support")).Permissions, Is.EqualTo("[]"));
        Assert.That(await check.Set<EbayClone.Infrastructure.Identity.ApplicationUser>().CountAsync(), Is.Zero);
        Assert.That(await check.Users.CountAsync(), Is.Zero);
    }
    [Test]
    public async Task DemoCreatesDataAndRestartDoesNotRepeat()
    {
        await using var app = Builder("Demo").Build(); await app.InitialiseDatabaseAsync();
        await using var db = Database();
        var users = await db.Users.CountAsync(); var disputes = await db.Disputes.CountAsync(); var messages = await db.DisputeMessages.CountAsync();
        Assert.That(users, Is.GreaterThan(0)); Assert.That(disputes, Is.GreaterThanOrEqualTo(10)); Assert.That(messages, Is.GreaterThan(0));
        await app.InitialiseDatabaseAsync();
        Assert.That(await db.Users.CountAsync(), Is.EqualTo(users)); Assert.That(await db.Disputes.CountAsync(), Is.EqualTo(disputes)); Assert.That(await db.DisputeMessages.CountAsync(), Is.EqualTo(messages));
    }
    [Test]
    public async Task DemoSkipsExistingCatalogBeforeCreatingRolesOrAccounts()
    {
        await using (var db = Database()) { db.Categories.Add(new Category { Name = "existing" }); await db.SaveChangesAsync(); }
        await using var app = Builder("Demo").Build(); await app.InitialiseDatabaseAsync();
        await using var check = Database(); Assert.That(await check.Roles.CountAsync(), Is.Zero); Assert.That(await check.Users.CountAsync(), Is.Zero); Assert.That(await check.Categories.CountAsync(), Is.EqualTo(1));
    }
    [Test]
    public async Task SeederFailureRollsBackRolesAccountAndPriorSeeders()
    {
        await using (var db = Database()) await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailDemo ON Category AFTER INSERT AS THROW 51000, 'isolated failure', 1;");
        await using var app = Builder("Demo").Build(); Assert.ThrowsAsync<DbUpdateException>(() => app.InitialiseDatabaseAsync());
        await using var check = Database(); Assert.That(await check.Roles.CountAsync(), Is.Zero); Assert.That(await check.AdminRoles.CountAsync(), Is.Zero); Assert.That(await check.Categories.CountAsync(), Is.Zero); Assert.That(await check.Set<EbayClone.Infrastructure.Identity.ApplicationUser>().CountAsync(), Is.Zero);
    }
    [TestCase("Invalid", "Development")]
    [TestCase("Demo", "Production")]
    public async Task InvalidConfigurationFailsBeforeMigration(string mode, string environment)
    {
        await using (var db = Database()) await db.Database.EnsureDeletedAsync();
        await using var app = Builder(mode, environment, migrate: true).Build();
        Assert.ThrowsAsync<InvalidOperationException>(() => app.InitialiseDatabaseAsync());
        await using var check = Database(); Assert.That(await check.Database.CanConnectAsync(), Is.False);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void FiveWriterJobsAreRegisteredOnlyWhenEnabled(bool enabled)
    {
        var builder = Builder(jobs: enabled);
        var names = new[] { "AutoReviewUsersService", "ReturnRequestEscalationService", "SellerEvaluationBackgroundService", "SettlementBackgroundService", "PayoutEngineBackgroundService" };
        var jobs = builder.Services.Where(x => x.ServiceType == typeof(IHostedService) && names.Contains(x.ImplementationType?.Name)).ToArray();
        Assert.That(jobs.Length, Is.EqualTo(enabled ? 5 : 0));
    }
    [Test]
    public async Task CliDryRunBypassesStartupWritersEvenWhenOptedIn()
    {
        var web = typeof(EbayClone.Web.Endpoints.Users).Assembly.Location;
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(web)! };
        start.ArgumentList.Add(web); start.ArgumentList.Add("--migrate-sensitive-data");
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["ConnectionStrings__MyCnn"] = _connection;
        start.Environment["Security__EncryptionKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());
        start.Environment["Jwt__Key"] = new string('t', 64);
        start.Environment["AZURE_KEY_VAULT_ENDPOINT"] = "";
        start.Environment["Redis__ConnectionString"] = "";
        start.Environment["Seeding__Mode"] = "Demo";
        start.Environment["BackgroundJobs__Enabled"] = "true";
        start.Environment["DatabaseInitialization__RunMigrationsOnStartup"] = "true";
        start.Environment["Logging__LogLevel__Default"] = "Information";
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        var text = await output; await errors;
        Assert.That(process.ExitCode, Is.Zero, "Isolated CLI must return successfully.");
        Assert.That(text, Does.Contain("PiiFields=0, Logs=0, LookupHashes=0"));
        await using var check = Database(); Assert.That(await check.Roles.CountAsync(), Is.Zero); Assert.That(await check.Users.CountAsync(), Is.Zero);
    }
    [TestCase("DatabaseInitialization:RunMigrationsOnStartup")]
    [TestCase("BackgroundJobs:Enabled")]
    public void InvalidBooleanFailsBeforeAnyDatabaseAccess(string key)
    {
        var builder = Builder(); builder.Configuration[key] = "invalid";
        Assert.Throws<InvalidOperationException>(() => ApplicationDbContextInitialiser.ValidateConfiguration(builder.Configuration, builder.Environment));
    }
    [Test]
    public async Task ExplicitMigrationOptInCreatesSchema()
    {
        await using (var db = Database()) await db.Database.EnsureDeletedAsync();
        await using var app = Builder(migrate: true).Build(); await app.InitialiseDatabaseAsync();
        await using var check = Database(); Assert.That(await check.Database.GetPendingMigrationsAsync(), Is.Empty); Assert.That(await check.Users.CountAsync(), Is.Zero);
    }
    [Test]
    public void SeederInventoryIsExplicitlyReviewed()
    {
        var types = typeof(ISeeder).Assembly.GetTypes().Where(x => typeof(ISeeder).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract).Select(x => x.Name).Order().ToArray();
        Assert.That(types, Is.EqualTo(new[] { "AdminRolesSeeder", "CategoriesSeeder", "PlatformFeesSeeder", "UsersSeeder", "SellerWalletsSeeder", "ProductsSeeder", "StoresSeeder", "AddressesSeeder", "DisputesSeeder", "ReturnRequestsSeeder", "FinanceDemoSeeder", "StatsDemoSeeder", "AdditionalDataSeeder", "PayoutEngineDemoSeeder", "SellerLevelCriteriaSeeder", "SensitiveDataLogsSeeder" }.Order().ToArray()));
    }
}
