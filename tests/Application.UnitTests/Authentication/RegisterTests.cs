using EbayClone.Application.Authentication.Commands.Login;
using EbayClone.Application.Authentication.Commands.Register;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.UnitTests.Security;
using EbayClone.Application.Users.Risk;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;
using ValidationException = EbayClone.Application.Common.Exceptions.ValidationException;

namespace EbayClone.Application.UnitTests.Authentication;

public class RegisterTests
{
    private ApplicationDbContext _db = null!;
    private Mock<ICaptchaVerifier> _captcha = null!;
    private Mock<IEmailService> _email = null!;
    private readonly List<string> _sentBodies = [];

    [SetUp]
    public void Setup()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _captcha = new Mock<ICaptchaVerifier>();
        _captcha.Setup(c => c.VerifyAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _email = new Mock<IEmailService>();
        _sentBodies.Clear();
        _email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, body) => _sentBodies.Add(body)).Returns(Task.CompletedTask);
    }

    [TearDown] public void TearDown() => _db.Dispose();

    private Task<RegisterResult> Register(RegisterCommand command) =>
        new RegisterCommandHandler(_db, EncryptionServiceTests.Create(), _captcha.Object, _email.Object, NullLogger<RegisterCommandHandler>.Instance)
            .Handle(command, CancellationToken.None);

    private Task<string?> Verify(string email, string code) =>
        new VerifyEmailCommandHandler(_db, EncryptionServiceTests.Create()).Handle(new VerifyEmailCommand(email, code), CancellationToken.None);

    private string LastCode() => System.Text.RegularExpressions.Regex.Match(_sentBodies[^1], @"\b\d{6}\b").Value;

    private static RegisterCommand Seller(string email = "Shop@Example.com") => new()
    {
        Email = email, Username = "shop_1", Password = "secret123", Role = "Seller", Phone = "0912345678",
        FullName = "Nguyễn Văn A", Cccd = "079095001234", DateOfBirth = new DateOnly(1995, 3, 1), RemoteIp = "14.161.1.1",
    };

    [TestCase("079095001234", 1995, true)]
    [TestCase("001301001234", 2001, true)]   // female, 2000s
    [TestCase("079095001234", 1996, false)]  // year does not match the date of birth
    [TestCase("003095001234", 1995, false)]  // 003 is not a province code
    [TestCase("07909500123", 1995, false)]   // 11 digits
    [TestCase("07909500123A", 1995, false)]
    public void CccdFormat_ChecksProvinceCenturyAndYear(string cccd, int year, bool valid) =>
        CccdFormat.IsValid(cccd, new DateOnly(year, 6, 1)).ShouldBe(valid);

    [Test]
    public void Validator_SellerNeedsValidKyc_BuyerDoesNot()
    {
        var validator = new RegisterCommandValidator();
        validator.Validate(Seller()).IsValid.ShouldBeTrue();
        validator.Validate(Seller() with { Cccd = null }).IsValid.ShouldBeFalse();
        validator.Validate(Seller() with { Cccd = "079096001234" }).IsValid.ShouldBeFalse(); // made-up: year mismatch
        validator.Validate(Seller() with { DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-16) }).IsValid.ShouldBeFalse();
        validator.Validate(Seller() with { Role = "SuperAdmin" }).IsValid.ShouldBeFalse();
        validator.Validate(Seller() with { Password = "password" }).IsValid.ShouldBeFalse(); // no digit
        validator.Validate(new RegisterCommand { Email = "b@example.com", Username = "buyer", Password = "secret123" }).IsValid.ShouldBeTrue();
    }

    // Sign-up -> Unverified (cannot log in, not reviewed) -> correct code -> Pending (picked up by the risk review).
    [Test]
    public async Task Register_ThenVerifyEmail_MovesToRiskReview()
    {
        var result = await Register(Seller());
        result.EmailSent.ShouldBeTrue();

        var user = await _db.Users.Include(u => u.Addresses).SingleAsync();
        user.Email.ShouldBe("shop@example.com");
        user.Status.ShouldBe("Unverified");
        user.ApprovalStatus.ShouldBe("PendingApproval");
        user.IsVerified.ShouldBeFalse();
        user.CCCD.ShouldStartWith("gcm:");
        user.CccdLookupHash.ShouldBe(EncryptionServiceTests.Create().ComputeHmac("cccd:079095001234"));
        user.Password.ShouldNotBe("secret123");
        user.EmailOtpHash!.ShouldNotContain(LastCode());
        AccountLinking.KycName(user.VerificationDocuments, EncryptionServiceTests.Create()).ShouldBe("Nguyễn Văn A");
        user.Addresses.Single().Phone.ShouldBe("0912345678");

        var login = await new LoginCommandHandler(_db, new Mock<IJwtService>().Object).Handle(new LoginCommand("shop@example.com", "secret123"), CancellationToken.None);
        login.Success.ShouldBeFalse();
        AccountStanding.Problem(user).ShouldNotBeNull();

        (await Verify("SHOP@example.com ", LastCode())).ShouldBeNull();
        user.Status.ShouldBe("Pending");
        user.EmailVerifiedAt.ShouldNotBeNull();
        user.EmailOtpHash.ShouldBeNull();
    }

    [Test]
    public async Task VerifyEmail_FiveWrongCodes_LockTheCodeUntilResend()
    {
        await Register(Seller());
        var code = LastCode();
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < EmailOtp.MaxAttempts; i++) (await Verify("shop@example.com", wrong)).ShouldNotBeNull();
        (await Verify("shop@example.com", code)).ShouldNotBeNull("locked even with the right code");

        var user = await _db.Users.SingleAsync();
        user.EmailOtpSentAt = DateTime.UtcNow - EmailOtp.ResendCooldown; // cooldown over
        await _db.SaveChangesAsync();
        await new ResendEmailCodeCommandHandler(_db, EncryptionServiceTests.Create(), _email.Object, NullLogger<ResendEmailCodeCommandHandler>.Instance)
            .Handle(new ResendEmailCodeCommand("shop@example.com"), CancellationToken.None);
        (await Verify("shop@example.com", LastCode())).ShouldBeNull();
    }

    [Test]
    public async Task VerifyEmail_ExpiredCode_IsRejected()
    {
        await Register(Seller());
        var user = await _db.Users.SingleAsync();
        user.EmailOtpSentAt = DateTime.UtcNow - EmailOtp.Lifetime - TimeSpan.FromSeconds(1);
        await _db.SaveChangesAsync();
        (await Verify("shop@example.com", LastCode())).ShouldNotBeNull();
        user.Status.ShouldBe("Unverified");
    }

    [Test]
    public async Task Register_FailedCaptchaOrTakenEmail_CreatesNothingNew()
    {
        _captcha.Setup(c => c.VerifyAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Should.ThrowAsync<ValidationException>(() => Register(Seller()));
        (await _db.Users.CountAsync()).ShouldBe(0);

        _captcha.Setup(c => c.VerifyAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Register(Seller());
        await Should.ThrowAsync<ValidationException>(() => Register(Seller("shop@EXAMPLE.com")));
        (await _db.Users.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Register_MailDown_AccountKeptAndCodeCanBeResent()
    {
        _email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("smtp"));
        (await Register(Seller())).EmailSent.ShouldBeFalse();
        (await _db.Users.SingleAsync()).Status.ShouldBe("Unverified");
    }

    // Testing with real SMTP settings present must still never mail a real address.
    [Test]
    public async Task EmailService_LogInsteadOfSending_WinsOverConfiguredSmtp()
    {
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["EmailSettings:SmtpServer"] = "smtp.invalid", ["EmailSettings:SenderEmail"] = "me@example.com",
            ["EmailSettings:SenderPassword"] = "secret", ["EmailSettings:LogInsteadOfSending"] = "true",
        })
            config.Setup(c => c[key]).Returns(value);
        var service = new EbayClone.Infrastructure.Services.EmailService(NullLogger<EbayClone.Infrastructure.Services.EmailService>.Instance, config.Object);
        await Should.NotThrowAsync(() => service.SendEmailAsync("someone@example.com", "OTP", "123456")); // would fail to reach smtp.invalid
    }
}
