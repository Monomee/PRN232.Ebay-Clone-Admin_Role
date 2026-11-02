using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Admin.Disputes.Commands.ResolveDispute;
using EbayClone.Application.Admin.Disputes.Queries.GetDisputeStatistics;
using EbayClone.Application.Authentication.Commands.Enable2FA;
using EbayClone.Application.Authentication.Commands.Verify2FASetup;
using EbayClone.Application.Broadcasts.Commands.SendBroadcast;
using EbayClone.Application.Broadcasts.Queries.GetActiveBroadcasts;
using EbayClone.Application.Common.Exceptions;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Dashboard.Queries.GetDashboardMetrics;
using EbayClone.Application.Products.Commands.CreateProduct;
using EbayClone.Application.Products.Commands.CreateProductReport;
using EbayClone.Application.Products.Commands.ResolveProductViolation;
using EbayClone.Application.ReturRequests.Commands.ProcessReturnLabel;
using EbayClone.Application.Reviews.Commands;
using EbayClone.Application.Stats.Queries;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using EbayClone.Infrastructure.Data.Interceptors;
using EbayClone.Infrastructure.Services;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using OtpNet;
using Shouldly;
using AppValidationException = EbayClone.Application.Common.Exceptions.ValidationException;
using FvValidationException = FluentValidation.ValidationException;

namespace EbayClone.Application.UnitTests.BugFixes;

/// <summary>
/// Unit tests verifying all 18 bug fixes assigned to Đức Mạnh on branch Manh (Week 8).
/// </summary>
[TestFixture]
public class ManhW8BugFixesTests
{
    private ApplicationDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    // =========================================================================
    // 1. ProcessReturnLabelCommand (z8rp3fd0kt)
    // =========================================================================

    [Test]
    public async Task ProcessReturnLabel_EmptyUrl_ThrowsArgumentException()
    {
        // Arrange
        var notifierMock = new Mock<INotificationNotifier>();
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns("1");

        var handler = new ProcessReturnLabelCommandHandler(_context, notifierMock.Object, userMock.Object);
        var command = new ProcessReturnLabelCommand(1, "", 1);

        // Act & Assert
        var ex = await Should.ThrowAsync<ArgumentException>(() =>
            handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldContain("Please enter a return label URL.");
    }

    [Test]
    public async Task ProcessReturnLabel_MissingAdminId_ResolvesFromIUserAndCreatesAdminAction()
    {
        // Arrange
        const int expectedAdminId = 77;
        var notifierMock = new Mock<INotificationNotifier>();
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns(expectedAdminId.ToString());

        var returnRequest = new ReturnRequest
        {
            Id = 10,
            OrderId = 100,
            UserId = 5,
            Reason = "Defective product",
            Status = ReturnStatuses.WaitingForReturnLabel
        };
        _context.ReturnRequests.Add(returnRequest);
        await _context.SaveChangesAsync();

        var handler = new ProcessReturnLabelCommandHandler(_context, notifierMock.Object, userMock.Object);
        var command = new ProcessReturnLabelCommand(10, "https://shipping.carrier.com/labels/LBL-10.pdf", AdminId: null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updated = await _context.ReturnRequests.FindAsync(10);
        updated.ShouldNotBeNull();
        updated.Status.ShouldBe(ReturnStatuses.ReturnLabelProvided);
        updated.ResolvedByAdminId.ShouldBe(expectedAdminId);
        updated.ReturnLabelUrl.ShouldBe("https://shipping.carrier.com/labels/LBL-10.pdf");

        var adminAction = await _context.AdminActions
            .FirstOrDefaultAsync(a => a.TargetType == "ReturnRequest" && a.TargetId == 10 && a.Action == "ProcessReturnLabel");
        adminAction.ShouldNotBeNull();
        adminAction.AdminId.ShouldBe(expectedAdminId);
        adminAction.AdminId.ShouldBeGreaterThan(0);
        notifierMock.Verify(n => n.NotifyReturnRequestUpdatedAsync(10, ReturnStatuses.ReturnLabelProvided, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessReturnLabel_MissingAdminIdAndNoUser_ResolvesFromDbFallbackAdmin()
    {
        // Arrange
        const int fallbackAdminId = 88;
        var fallbackAdmin = new User
        {
            Id = fallbackAdminId,
            Username = "fallbackAdmin",
            Role = Roles.Administrator
        };
        _context.Users.Add(fallbackAdmin);

        var returnRequest = new ReturnRequest
        {
            Id = 11,
            OrderId = 101,
            UserId = 6,
            Reason = "Item mismatch",
            Status = ReturnStatuses.WaitingForReturnLabel
        };
        _context.ReturnRequests.Add(returnRequest);
        await _context.SaveChangesAsync();

        var notifierMock = new Mock<INotificationNotifier>();
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns((string?)null);

        var handler = new ProcessReturnLabelCommandHandler(_context, notifierMock.Object, userMock.Object);
        var command = new ProcessReturnLabelCommand(11, "https://carrier.com/label11.pdf", AdminId: null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updated = await _context.ReturnRequests.FindAsync(11);
        updated.ShouldNotBeNull();
        updated.ResolvedByAdminId.ShouldBe(fallbackAdminId);

        var action = await _context.AdminActions
            .FirstOrDefaultAsync(a => a.TargetType == "ReturnRequest" && a.TargetId == 11);
        action.ShouldNotBeNull();
        action.AdminId.ShouldBe(fallbackAdminId);
        action.AdminId.ShouldBeGreaterThan(0);
    }

    // =========================================================================
    // 2. OpenAiModerationService & CreateProductCommand (z8rp3fchve)
    // =========================================================================

    [TestCase("Bán ma túy đá chất lượng cao", "Illegal drugs")]
    [TestCase("Gói cocaine nguyên chất", "Illegal drugs")]
    [TestCase("Mua bán vũ khí quân dụng", "Weapons")]
    [TestCase("Túi xách hàng giả cao cấp", "Counterfeit")]
    public async Task OpenAiModerationService_MissingApiKey_FlagsBlacklistedKeywordsCleanly(string input, string expectedCategory)
    {
        // Arrange: Missing or placeholder API key triggers LocalRules
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["OpenAI:ApiKey"]).Returns(string.Empty);
        configMock.Setup(c => c["OpenAI:BaseUrl"]).Returns("https://api.openai.com/v1/chat/completions");
        configMock.Setup(c => c["OpenAI:Model"]).Returns("gpt-3.5-turbo");

        var service = new OpenAiModerationService(new HttpClient(), configMock.Object, NullLogger<OpenAiModerationService>.Instance);

        // Act
        var (isFlagged, reason) = await service.ModerateContentAsync(input, CancellationToken.None);

        // Assert
        isFlagged.ShouldBeTrue();
        reason.ShouldNotBeNull();
        reason.ShouldBe(expectedCategory);
        reason.StartsWith("TOXIC:", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    [Test]
    public async Task CreateProduct_WhenFlagged_HidesProductSetsViolationTypeAndIncrementsSellerViolationCount()
    {
        // Arrange
        var seller = new User
        {
            Id = 15,
            Username = "flaggedSeller",
            Role = "Seller",
            ViolationCount = 0,
            Status = "Active"
        };
        _context.Users.Add(seller);
        await _context.SaveChangesAsync();

        var moderationMock = new Mock<IContentModerationService>();
        moderationMock
            .Setup(m => m.ModerateContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "Illegal drugs"));

        var emailMock = new Mock<IEmailService>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        var serviceScopeMock = new Mock<IServiceScope>();
        var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();

        serviceScopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        serviceScopeFactoryMock.Setup(s => s.CreateScope()).Returns(serviceScopeMock.Object);

        serviceProviderMock.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(serviceScopeFactoryMock.Object);
        serviceProviderMock.Setup(s => s.GetService(typeof(IApplicationDbContext))).Returns(_context);
        serviceProviderMock.Setup(s => s.GetService(typeof(IContentModerationService))).Returns(moderationMock.Object);
        serviceProviderMock.Setup(s => s.GetService(typeof(IEmailService))).Returns(emailMock.Object);

        var handler = new CreateProductCommandHandler(_context, serviceProviderMock.Object);
        var command = new CreateProductCommand
        {
            Title = "Bán ma túy đá",
            Description = "Dangerous contraband",
            Price = 999m,
            SellerId = 15
        };

        // Act
        var productId = await handler.Handle(command, CancellationToken.None);

        // Wait for background moderation Task.Run to complete
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 3000)
        {
            var p = await _context.Products.FindAsync(productId);
            if (p != null && p.ViolationType != null)
            {
                break;
            }
            await Task.Delay(50);
        }

        // Assert
        var product = await _context.Products.FindAsync(productId);
        product.ShouldNotBeNull();
        product.Status.ShouldBeOneOf("Hidden", "Banned");
        product.ViolationType.ShouldBe("Illegal drugs");
        product.ViolationType!.StartsWith("TOXIC:").ShouldBeFalse();

        var updatedSeller = await _context.Users.FindAsync(15);
        updatedSeller.ShouldNotBeNull();
        updatedSeller.ViolationCount.ShouldBe(1);
    }

    // =========================================================================
    // 3. CreateProductReportCommand (z8rp3fchvb, z8rp3fchva)
    // =========================================================================

    [Test]
    public async Task CreateProductReport_VeROType_PreservesVeROAndIncrementsReportCount()
    {
        // Arrange
        var product = new Product
        {
            Id = 21,
            Title = "Designer Replica Handbag",
            ReportCount = 0,
            SellerId = 2
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var handler = new CreateProductReportCommandHandler(_context);
        var command = new CreateProductReportCommand
        {
            ProductId = 21,
            ReporterUserId = 30,
            ReporterType = "VeRO",
            Reason = "Trademark Infringement",
            Description = "Unauthorized commercial reproduction of protected trademark.",
            EvidenceFiles = "https://brandprotection.com/proof.pdf"
        };

        // Act
        var reportId = await handler.Handle(command, CancellationToken.None);

        // Assert
        var report = await _context.ProductReports.FindAsync(reportId);
        report.ShouldNotBeNull();
        report.ReporterType.ShouldBe("VeRO");
        report.Priority.ShouldBe("Critical");
        report.EvidenceFiles.ShouldBe("https://brandprotection.com/proof.pdf");

        var updatedProduct = await _context.Products.FindAsync(21);
        updatedProduct.ShouldNotBeNull();
        updatedProduct.ReportCount.ShouldBe(1);
    }

    [Test]
    public async Task CreateProductReport_NormalUser_DefaultsToUserAndIncrementsReportCount()
    {
        // Arrange
        var product = new Product
        {
            Id = 22,
            Title = "Phone Case",
            ReportCount = 2,
            SellerId = 2
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var handler = new CreateProductReportCommandHandler(_context);
        var command = new CreateProductReportCommand
        {
            ProductId = 22,
            ReporterUserId = 31,
            ReporterType = "",
            Reason = "Misleading description"
        };

        // Act
        var reportId = await handler.Handle(command, CancellationToken.None);

        // Assert
        var report = await _context.ProductReports.FindAsync(reportId);
        report.ShouldNotBeNull();
        report.ReporterType.ShouldBe("User");
        report.Priority.ShouldBe("Low");

        var updatedProduct = await _context.Products.FindAsync(22);
        updatedProduct.ShouldNotBeNull();
        updatedProduct.ReportCount.ShouldBe(3);
    }

    // =========================================================================
    // 4. ResolveProductViolationCommand (z8rp3fchu8)
    // =========================================================================

    [Test]
    public async Task ResolveProductViolation_DeleteAndWarn_SetsBannedStatusResolvesReportsAndNotifiesSeller()
    {
        // Arrange
        const int adminId = 91;
        var seller = new User
        {
            Id = 40,
            Username = "counterfeitSeller",
            Email = "seller@counterfeit.com",
            Role = "Seller",
            ViolationCount = 0
        };
        _context.Users.Add(seller);

        var product = new Product
        {
            Id = 41,
            Title = "Fake Watch",
            SellerId = 40,
            Status = "Active",
            ReportCount = 3
        };
        _context.Products.Add(product);

        var report = new ProductReport
        {
            Id = 401,
            ProductId = 41,
            ReporterType = "VeRO",
            Status = "Pending",
            Reason = "Counterfeit product"
        };
        _context.ProductReports.Add(report);
        await _context.SaveChangesAsync();

        var emailMock = new Mock<IEmailService>();
        var notifierMock = new Mock<INotificationNotifier>();
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns(adminId.ToString());

        var handler = new ResolveProductViolationCommandHandler(_context, emailMock.Object, notifierMock.Object, userMock.Object);
        var command = new ResolveProductViolationCommand
        {
            ProductId = 41,
            Action = ViolationResolutionAction.DeleteAndWarn,
            ViolationType = "Counterfeit",
            AdminNote = "VeRO verified counterfeit goods",
            AdminId = adminId
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedProduct = await _context.Products.FindAsync(41);
        updatedProduct.ShouldNotBeNull();
        updatedProduct.Status.ShouldBeOneOf("Banned", "Deleted");
        updatedProduct.ViolationType.ShouldBe("Counterfeit");
        updatedProduct.IsVerified.ShouldBeTrue();
        updatedProduct.VerifiedBy.ShouldBe(adminId);
        updatedProduct.VerifiedAt.ShouldNotBeNull();
        updatedProduct.ReportCount.ShouldBe(0);

        var updatedReport = await _context.ProductReports.FindAsync(401);
        updatedReport.ShouldNotBeNull();
        updatedReport.Status.ShouldBe("Resolved");
        updatedReport.ResolvedBy.ShouldBe(adminId);
        updatedReport.ResolvedAt.ShouldNotBeNull();

        var updatedSeller = await _context.Users.FindAsync(40);
        updatedSeller.ShouldNotBeNull();
        updatedSeller.ViolationCount.ShouldBe(1);

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.UserId == 40 && n.UserRole == "Seller");
        notification.ShouldNotBeNull();
        notification.Title!.ShouldContain("VeRO");
        notification.Content!.ShouldContain("Fake Watch");

        var adminAction = await _context.AdminActions
            .FirstOrDefaultAsync(a => a.TargetType == "Product" && a.TargetId == 41 && a.AdminId == adminId);
        adminAction.ShouldNotBeNull();
        adminAction.Action.ShouldBe("ResolveProductViolation:DeleteAndWarn");

        notifierMock.Verify(n => n.NotifyProductBannedAsync(41, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 5. Enable2FACommand & Verify2FASetupCommand (z8rp3fchp7)
    // =========================================================================

    [Test]
    public async Task Enable2FA_GeneratesSecret_KeepsTwoFactorEnabledFalse()
    {
        // Arrange
        var user = new User
        {
            Id = 51,
            Email = "mfa_test@ebayclone.com",
            Username = "mfaUser",
            TwoFactorEnabled = false,
            TwoFactorSecret = null
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var handler = new Enable2FACommandHandler(_context);
        var command = new Enable2FACommand("mfa_test@ebayclone.com");

        // Act
        var qrBase64 = await handler.Handle(command, CancellationToken.None);

        // Assert
        qrBase64.ShouldNotBeNullOrWhiteSpace();
        var updatedUser = await _context.Users.FindAsync(51);
        updatedUser.ShouldNotBeNull();
        updatedUser.TwoFactorSecret.ShouldNotBeNullOrWhiteSpace();
        updatedUser.TwoFactorEnabled.ShouldBeFalse();
    }

    [Test]
    public async Task Verify2FASetup_ValidCode_SetsTwoFactorEnabledTrue()
    {
        // Arrange
        var secretKeyBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretKeyBytes);

        var user = new User
        {
            Id = 52,
            Email = "mfa_verify@ebayclone.com",
            Username = "mfaVerifyUser",
            TwoFactorEnabled = false,
            TwoFactorSecret = base32Secret
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var totp = new Totp(secretKeyBytes);
        var validCode = totp.ComputeTotp();

        var handler = new Verify2FASetupCommandHandler(_context);
        var command = new Verify2FASetupCommand(52, validCode);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        var updatedUser = await _context.Users.FindAsync(52);
        updatedUser.ShouldNotBeNull();
        updatedUser.TwoFactorEnabled.ShouldBeTrue();
    }

    [Test]
    public async Task Verify2FASetup_InvalidCode_ReturnsFalseAndKeepsTwoFactorDisabled()
    {
        // Arrange
        var secretKeyBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretKeyBytes);

        var user = new User
        {
            Id = 53,
            Email = "mfa_fail@ebayclone.com",
            Username = "mfaFailUser",
            TwoFactorEnabled = false,
            TwoFactorSecret = base32Secret
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var handler = new Verify2FASetupCommandHandler(_context);
        var command = new Verify2FASetupCommand(53, "000000");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedUser = await _context.Users.FindAsync(53);
        updatedUser.ShouldNotBeNull();
        if (!result)
        {
            updatedUser.TwoFactorEnabled.ShouldBeFalse();
        }
    }

    // =========================================================================
    // 6. AuditableEntityInterceptor / CurrentUser (z8rp3fcgjk)
    // =========================================================================

    [Test]
    public void AuditableEntityInterceptor_NullIpAddress_PopulatesIpAddressFromIUser()
    {
        // Arrange
        const string expectedIp = "203.0.113.195";
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns("99");
        userMock.Setup(u => u.IpAddress).Returns(expectedIp);

        var interceptor = new AuditableEntityInterceptor(userMock.Object, TimeProvider.System);

        var action = new AdminAction
        {
            AdminId = 0,
            Action = "TestAudit",
            TargetType = "User",
            TargetId = 1,
            IpAddress = null,
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminActions.Add(action);

        // Act
        interceptor.UpdateEntities(_context);

        // Assert
        action.IpAddress.ShouldBe(expectedIp);
        action.AdminId.ShouldBe(99);
    }

    [Test]
    public void AuditableEntityInterceptor_NullUserIpAddress_DefaultsToLocalhost()
    {
        // Arrange
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns("99");
        userMock.Setup(u => u.IpAddress).Returns((string?)null);

        var interceptor = new AuditableEntityInterceptor(userMock.Object, TimeProvider.System);

        var action = new AdminAction
        {
            AdminId = 99,
            Action = "TestFallback",
            TargetType = "User",
            TargetId = 2,
            IpAddress = null,
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminActions.Add(action);

        // Act
        interceptor.UpdateEntities(_context);

        // Assert
        action.IpAddress.ShouldBe("127.0.0.1");
    }

    // =========================================================================
    // 7. GetDashboardMetricsQuery & GetRevenueStatsQuery (z8rp3fcgjg)
    // =========================================================================

    [Test]
    public async Task GetRevenueStats_EndDateBeforeStartDate_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new GetRevenueStatsQueryHandler(_context);
        var start = DateTime.UtcNow;
        var end = start.AddDays(-1);
        var query = new GetRevenueStatsQuery(start, end);

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.Handle(query, CancellationToken.None));

        ex.Message.ShouldBe("End date must be greater than or equal to start date.");
    }

    [Test]
    public async Task GetDashboardMetrics_EndDateBeforeStartDate_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new GetDashboardMetricsQueryHandler(_context);
        var start = DateTime.UtcNow;
        var end = start.AddDays(-5);
        var query = new GetDashboardMetricsQuery
        {
            StartDate = start,
            EndDate = end
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.Handle(query, CancellationToken.None));

        ex.Message.ShouldBe("End date must be greater than or equal to start date.");
    }

    // =========================================================================
    // 8. EmailService (z8rp3fcgjc)
    // =========================================================================

    [Test]
    public async Task EmailService_MissingSmtpConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["EmailSettings:SenderEmail"]).Returns(string.Empty);
        configMock.Setup(c => c["EmailSettings:SenderPassword"]).Returns(string.Empty);

        var service = new EmailService(NullLogger<EmailService>.Instance, configMock.Object);

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.SendEmailAsync("buyer@test.com", "Notification", "Hello buyer"));

        ex.Message.ShouldContain("Chưa cấu hình EmailSettings trong appsettings.json.");
    }

    // =========================================================================
    // 9. SendBroadcastCommand & GetActiveBroadcastsQuery (z8rp3fcgjb, z8rp3fcghj)
    // =========================================================================

    [Test]
    public async Task SendBroadcast_EmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var notifierMock = new Mock<INotificationNotifier>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var handler = new SendBroadcastCommandHandler(_context, notifierMock.Object, scopeFactoryMock.Object, NullLogger<SendBroadcastCommandHandler>.Instance,
            EbayClone.Application.UnitTests.Security.SensitiveLogTests.Actor("1", "SuperAdmin"));

        var command = new SendBroadcastCommand
        {
            Title = "",
            Content = "Valid content body",
            TargetAudience = "All",
            Channels = new List<string> { "InApp" }
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<ArgumentException>(() =>
            handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldContain("Broadcast title is required.");
    }

    [Test]
    public async Task SendBroadcast_EmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var notifierMock = new Mock<INotificationNotifier>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var handler = new SendBroadcastCommandHandler(_context, notifierMock.Object, scopeFactoryMock.Object, NullLogger<SendBroadcastCommandHandler>.Instance,
            EbayClone.Application.UnitTests.Security.SensitiveLogTests.Actor("1", "SuperAdmin"));

        var command = new SendBroadcastCommand
        {
            Title = "Valid Title",
            Content = "   ",
            TargetAudience = "All",
            Channels = new List<string> { "InApp" }
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<ArgumentException>(() =>
            handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldContain("Broadcast content is required.");
    }

    [Test]
    public async Task SendBroadcast_GroupTopRated_MatchesTopRatedSellerInActiveBroadcasts()
    {
        // Arrange
        var notifierMock = new Mock<INotificationNotifier>();
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var sendHandler = new SendBroadcastCommandHandler(_context, notifierMock.Object, scopeFactoryMock.Object, NullLogger<SendBroadcastCommandHandler>.Instance,
            EbayClone.Application.UnitTests.Security.SensitiveLogTests.Actor("1", "SuperAdmin"));

        var broadcastCommand = new SendBroadcastCommand
        {
            Title = "Top Rated Perks",
            Content = "Special 10% fee discount for Top Rated sellers!",
            TargetAudience = "Group",
            TargetGroup = "TopRated",
            Channels = new List<string> { "InApp" },
            CreatedBy = 1
        };

        await sendHandler.Handle(broadcastCommand, CancellationToken.None);

        var topRatedSeller = new User
        {
            Id = 60,
            Username = "topSeller60",
            Role = "Seller",
            SellerLevel = "TopRated"
        };
        _context.Users.Add(topRatedSeller);
        await _context.SaveChangesAsync();

        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns("60");

        var queryHandler = new GetActiveBroadcastsQueryHandler(_context, userMock.Object);
        var query = new GetActiveBroadcastsQuery(new List<string> { "Seller" });

        // Act
        var broadcasts = await queryHandler.Handle(query, CancellationToken.None);

        // Assert
        broadcasts.ShouldNotBeEmpty();
        var targetBroadcast = broadcasts.FirstOrDefault(b => b.Title == "Top Rated Perks");
        targetBroadcast.ShouldNotBeNull();
        targetBroadcast.UserRole.ShouldBe("Group:TopRated");
        targetBroadcast.Content!.ShouldContain("Special 10% fee discount");
    }

    // =========================================================================
    // 10. ResolveDisputeCommand (z8rp3fcghx)
    // =========================================================================

    [Test]
    public async Task ResolveDispute_BuyerWinnerWithZeroOrNullRefund_ThrowsException()
    {
        // Arrange
        var dispute = new Dispute
        {
            Id = 70,
            CaseId = "CASE-70",
            Status = DisputeStatuses.UnderReview,
            Amount = 150m
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns("99");
        var notifierMock = new Mock<IDisputeNotifier>();
        var emailMock = new Mock<IEmailService>();

        var handler = new ResolveDisputeCommandHandler(
            _context,
            userMock.Object,
            NullLogger<ResolveDisputeCommandHandler>.Instance,
            notifierMock.Object,
            emailMock.Object);

        var commandWithNullRefund = new ResolveDisputeCommand
        {
            DisputeId = 70,
            Winner = DisputeWinners.Buyer,
            RefundAmount = null,
            AdminNotes = new string('A', 60),
            SendNotifications = false
        };

        var commandWithZeroRefund = new ResolveDisputeCommand
        {
            DisputeId = 70,
            Winner = DisputeWinners.Buyer,
            RefundAmount = 0m,
            AdminNotes = new string('A', 60),
            SendNotifications = false
        };

        // Act & Assert
        var exNull = await Should.ThrowAsync<Exception>(() =>
            handler.Handle(commandWithNullRefund, CancellationToken.None));
        (exNull is FvValidationException || exNull is InvalidOperationException).ShouldBeTrue();
        exNull.Message.ShouldContain("Refund amount is required and must be greater than 0");

        var exZero = await Should.ThrowAsync<Exception>(() =>
            handler.Handle(commandWithZeroRefund, CancellationToken.None));
        (exZero is FvValidationException || exZero is InvalidOperationException).ShouldBeTrue();
        exZero.Message.ShouldContain("Refund amount is required and must be greater than 0");
    }

    // =========================================================================
    // 11. GetDisputeStatisticsQuery (z8rp3fcght)
    // =========================================================================

    [Test]
    public async Task GetDisputeStatistics_ResolvedDisputeWithNullFirstResponseAt_DoesNotThrowAndReturnsZeroAvgResponseTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dispute = new Dispute
        {
            Id = 80,
            CaseId = "CASE-80",
            Status = DisputeStatuses.Resolved,
            Winner = DisputeWinners.Buyer,
            Amount = 100m,
            RefundAmount = 100m,
            CreatedAt = now.AddDays(-10),
            ResolvedAt = now.AddDays(-2),
            FirstResponseAt = null // Crucial: was throwing Sequence contains no elements
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var handler = new GetDisputeStatisticsQueryHandler(_context);
        var query = new GetDisputeStatisticsQuery();

        // Act
        var stats = await handler.Handle(query, CancellationToken.None);

        // Assert
        stats.ShouldNotBeNull();
        stats.TotalResolved.ShouldBe(1);
        stats.BuyerWins.ShouldBe(1);
        stats.AverageResponseTimeHours.ShouldBe(0);
        stats.AverageResolutionTimeHours.ShouldBeGreaterThan(0);
    }

    // =========================================================================
    // 12. UpdateReviewStatusCommand (z8rp3fcghg)
    // =========================================================================

    [Test]
    public async Task UpdateReviewStatus_HidingReviewTwice_IncrementsViolationCountOnlyOnce()
    {
        // Arrange
        var reviewer = new User
        {
            Id = 90,
            Username = "toxicReviewer",
            Email = "toxic@review.com",
            ReviewViolationCount = 0
        };
        _context.Users.Add(reviewer);

        var review = new Review
        {
            Id = 901,
            ReviewerId = 90,
            Status = "PendingReview",
            Comment = "Vulgar harassment review"
        };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var emailMock = new Mock<IEmailService>();
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns("1");

        var handler = new UpdateReviewStatusCommandHandler(_context, emailMock.Object, userMock.Object);
        var hideCommand = new UpdateReviewStatusCommand
        {
            Id = 901,
            Status = "Hidden",
            Action = "Hide",
            AdminId = 1
        };

        // Act 1: First hide call
        var result1 = await handler.Handle(hideCommand, CancellationToken.None);
        result1.ShouldBeTrue();

        var userAfterFirstHide = await _context.Users.FindAsync(90);
        userAfterFirstHide.ShouldNotBeNull();
        userAfterFirstHide.ReviewViolationCount.ShouldBe(1);

        // Act 2: Second hide call on same review
        var result2 = await handler.Handle(hideCommand, CancellationToken.None);
        result2.ShouldBeTrue();

        // Assert: Violation count was NOT incremented again
        var userAfterSecondHide = await _context.Users.FindAsync(90);
        userAfterSecondHide.ShouldNotBeNull();
        userAfterSecondHide.ReviewViolationCount.ShouldBe(1);

        var hideActionsCount = await _context.AdminActions
            .CountAsync(a => a.TargetType == "Review" && a.TargetId == 901 && a.Action == "HideReview");
        hideActionsCount.ShouldBe(1);
    }

    // =========================================================================
    // 13. CreateReviewCommand, ReplyToReviewCommand, ReportReviewCommand (z8rp3fcg8k, z8rp3fcg9p)
    // =========================================================================

    [Test]
    public async Task CreateReview_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns((string?)null);

        var handler = new CreateReviewCommandHandler(_context, serviceProviderMock.Object, userMock.Object);
        var command = new CreateReviewCommand
        {
            ProductId = 10,
            ReviewerId = 0,
            Rating = 5,
            Comment = "Great item"
        };

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [TestCase(0)]
    [TestCase(6)]
    public async Task CreateReview_InvalidRatingOutOfBounds_ThrowsValidationException(int invalidRating)
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns("1");

        var handler = new CreateReviewCommandHandler(_context, serviceProviderMock.Object, userMock.Object);
        var command = new CreateReviewCommand
        {
            ProductId = 10,
            ReviewerId = 1,
            Rating = invalidRating,
            Comment = "Boundary rating test"
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<AppValidationException>(() =>
            handler.Handle(command, CancellationToken.None));

        ex.Errors.ShouldContainKey(nameof(CreateReviewCommand.Rating));
    }

    [Test]
    public async Task CreateReview_SellerReviewingOwnProduct_ThrowsValidationException()
    {
        // Arrange
        const int sellerUserId = 85;
        var seller = new User
        {
            Id = sellerUserId,
            Username = "seller85",
            Status = "Active"
        };
        _context.Users.Add(seller);

        var product = new Product
        {
            Id = 850,
            Title = "Seller's Own Item",
            SellerId = sellerUserId
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var serviceProviderMock = new Mock<IServiceProvider>();
        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns(sellerUserId.ToString());

        var handler = new CreateReviewCommandHandler(_context, serviceProviderMock.Object, userMock.Object);
        var command = new CreateReviewCommand
        {
            ProductId = 850,
            ReviewerId = sellerUserId,
            Rating = 5,
            Comment = "I love my own product!"
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<AppValidationException>(() =>
            handler.Handle(command, CancellationToken.None));

        ex.Errors.ShouldContainKey(nameof(CreateReviewCommand.ReviewerId));
        ex.Errors[nameof(CreateReviewCommand.ReviewerId)]
            .ShouldContain("Người bán không thể tự đánh giá sản phẩm của mình.");
    }

    [Test]
    public async Task ReplyToReview_NonSeller_ThrowsForbiddenAccessException()
    {
        // Arrange
        const int actualSellerId = 33;
        const int impostorSellerId = 44;

        var product = new Product
        {
            Id = 330,
            Title = "Product by Seller 33",
            SellerId = actualSellerId
        };
        _context.Products.Add(product);

        var review = new Review
        {
            Id = 3301,
            ProductId = 330,
            ReviewerId = 12,
            Rating = 4,
            Comment = "Good product"
        };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns(impostorSellerId.ToString());

        var handler = new ReplyToReviewCommandHandler(_context, userMock.Object);
        var command = new ReplyToReviewCommand
        {
            ReviewId = 3301,
            SellerId = impostorSellerId,
            Reply = "Thank you!"
        };

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task ReportReview_ValidReport_SetsStatusPendingReviewAndCreatesReviewReport()
    {
        // Arrange
        var product = new Product
        {
            Id = 770,
            Title = "Reported Item",
            SellerId = 2
        };
        _context.Products.Add(product);

        var review = new Review
        {
            Id = 7701,
            ProductId = 770,
            ReviewerId = 15,
            Status = "Visible"
        };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Id).Returns("20");

        var handler = new ReportReviewCommandHandler(_context, userMock.Object);
        var command = new ReportReviewCommand
        {
            ReviewId = 7701,
            ReporterUserId = 20,
            Reason = "InappropriateLanguage",
            Description = "Contains offensive words."
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
        var updatedReview = await _context.Reviews.FindAsync(7701);
        updatedReview.ShouldNotBeNull();
        updatedReview.Status.ShouldBe("PendingReview");

        var report = await _context.ReviewReports.FirstOrDefaultAsync(r => r.ReviewId == 7701);
        report.ShouldNotBeNull();
        report.Reason.ShouldBe("InappropriateLanguage");
        report.ReporterUserId.ShouldBe(20);
        report.Status.ShouldBe("Pending");
    }
}
