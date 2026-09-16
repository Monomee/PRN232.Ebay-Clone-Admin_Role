using EbayClone.Application.Common.Interfaces;
using EbayClone.Application.Products.Commands.CreateProduct;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Products;

public class CreateProductTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _serviceProviderMock = new Mock<IServiceProvider>();

        // Setup ServiceProvider for background task (moderation)
        var serviceScopeMock = new Mock<IServiceScope>();
        var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        
        serviceScopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        serviceScopeFactoryMock.Setup(s => s.CreateScope()).Returns(serviceScopeMock.Object);
        
        _serviceProviderMock.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(serviceScopeFactoryMock.Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(IApplicationDbContext))).Returns(_context);
        _serviceProviderMock.Setup(s => s.GetService(typeof(IContentModerationService))).Returns(new Mock<IContentModerationService>().Object);
        _serviceProviderMock.Setup(s => s.GetService(typeof(IEmailService))).Returns(new Mock<IEmailService>().Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task Handle_ValidRequest_ShouldCreateProduct()
    {
        // Arrange
        var handler = new CreateProductCommandHandler(_context, _serviceProviderMock.Object);
        var command = new CreateProductCommand 
        { 
            Title = "Laptop Pro", 
            Price = 1500, 
            SellerId = 1 
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldBeGreaterThan(0);
        var product = await _context.Products.FindAsync(result);
        product.ShouldNotBeNull();
        product.Title.ShouldBe("Laptop Pro");
        product.Status.ShouldBe("Active");
    }

    [Test]
    public void Handle_BannedSeller_ShouldThrowException()
    {
        // Arrange
        var seller = new User { Id = 2, Status = "Banned" };
        _context.Users.Add(seller);
        _context.SaveChanges();

        var handler = new CreateProductCommandHandler(_context, _serviceProviderMock.Object);
        var command = new CreateProductCommand { Title = "Item", SellerId = 2 };

        // Act & Assert
        Should.ThrowAsync<Exception>(() => handler.Handle(command, CancellationToken.None))
            .Result.Message.ShouldBe("Tài khoản của bạn đã bị khóa.");
    }

    [Test]
    public void Handle_SellerWithProductBan_ShouldThrowException()
    {
        // Arrange
        var seller = new User { Id = 3, ProductBanUntil = DateTime.UtcNow.AddDays(1) };
        _context.Users.Add(seller);
        _context.SaveChanges();

        var handler = new CreateProductCommandHandler(_context, _serviceProviderMock.Object);
        var command = new CreateProductCommand { Title = "Item", SellerId = 3 };

        // Act & Assert
        Should.ThrowAsync<Exception>(() => handler.Handle(command, CancellationToken.None))
            .Result.Message.ShouldContain("Chức năng đăng sản phẩm của bạn bị tạm khóa");
    }
}
