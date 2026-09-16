using EbayClone.Application.Authentication.Commands.Login;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Authentication;

public class LoginTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IJwtService> _jwtMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _jwtMock = new Mock<IJwtService>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task Handle_ValidLogin_ShouldReturnToken()
    {
        // Arrange
        var password = "Password123";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        var user = new User { Email = "test@example.com", Password = hashedPassword, TwoFactorEnabled = false };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns("TestToken");

        var handler = new LoginCommandHandler(_context, _jwtMock.Object);
        var command = new LoginCommand("test@example.com", password);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Token.ShouldBe("TestToken");
        result.RequireTwoFactor.ShouldBeFalse();
    }

    [Test]
    public async Task Handle_LoginWith2FA_ShouldReturnRequireTwoFactor()
    {
        // Arrange
        var password = "Password123";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        var user = new User { Email = "2fa@example.com", Password = hashedPassword, TwoFactorEnabled = true };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var handler = new LoginCommandHandler(_context, _jwtMock.Object);
        var command = new LoginCommand("2fa@example.com", password);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.RequireTwoFactor.ShouldBeTrue();
        result.Token.ShouldBeNull();
    }

    [Test]
    public async Task Handle_InvalidCredentials_ShouldReturnFailure()
    {
        // Arrange
        var handler = new LoginCommandHandler(_context, _jwtMock.Object);
        var command = new LoginCommand("wrong@example.com", "any");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Email hoặc mật khẩu không đúng");
    }
}
