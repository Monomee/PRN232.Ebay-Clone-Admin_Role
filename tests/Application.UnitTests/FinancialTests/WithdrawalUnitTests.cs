//using EbayClone.Application.Withdrawals.Commands.ApproveWithdrawal;
//using EbayClone.Application.Withdrawals.Commands.RejectWithdrawal;
//using EbayClone.Domain.Entities;
//using EbayClone.Infrastructure.Data;
//using EbayClone.Application.Common.Interfaces;
//using Microsoft.EntityFrameworkCore;
//using Moq;
//using Shouldly;
//using NUnit.Framework;

//namespace EbayClone.Application.UnitTests.FinancialTests;

//public class WithdrawalUnitTests
//{
//    private ApplicationDbContext _context = null!;
//    private Mock<IUser> _userMock = null!;

//    [SetUp]
//    public void Setup()
//    {
//        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
//            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
//            .Options;

//        _context = new ApplicationDbContext(options);
//        _userMock = new Mock<IUser>();
//        _userMock.Setup(u => u.Id).Returns("9"); // Default admin ID
//    }

//    [TearDown]
//    public void TearDown()
//    {
//        _context.Dispose();
//    }

//    [Test]
//    public async Task Handle_ApproveWithdrawal_ShouldUpdateStatusAndWallet()
//    {
//        // Arrange
//        var seller = new User { Id = 1, Username = "Seller1" };
//        _context.Users.Add(seller);

//        var wallet = new SellerWallet { Id = 1, SellerId = 1, AvailableBalance = 1000, LockedBalance = 500 };
//        _context.SellerWallets.Add(wallet);

//        var withdrawal = new WithdrawalRequest
//        { 
//            Id = 1, 
//            SellerId = 1, 
//            Amount = 500, 
//            Status = WithdrawalRequest.StatusPending 
//        };
//        _context.WithdrawalRequests.Add(withdrawal);
//        await _context.SaveChangesAsync();

//        var handler = new ApproveWithdrawalCommandHandler(_context, _userMock.Object);
//        var command = new ApproveWithdrawalCommand(1, "TXN123");

//        // Act
//        await handler.Handle(command, CancellationToken.None);

//        // Assert
//        var updatedWithdrawal = await _context.WithdrawalRequests.FindAsync(1);
//        updatedWithdrawal!.Status.ShouldBe(WithdrawalRequest.StatusApproved);
//        updatedWithdrawal.TransactionId.ShouldBe("TXN123");

//        var updatedWallet = await _context.SellerWallets.FindAsync(1);
//        updatedWallet!.LockedBalance.ShouldBe(0);
//        updatedWallet.TotalWithdrawn.ShouldBe(500);

//        var transaction = await _context.FinancialTransactions.FirstOrDefaultAsync(t => t.WithdrawalId == 1);
//        transaction.ShouldNotBeNull();
//        transaction!.Type.ShouldBe("Withdrawal");
//        transaction.Amount.ShouldBe(-500);
//    }

//    [Test]
//    public async Task Handle_RejectWithdrawal_ShouldRefundToAvailable()
//    {
//        // Arrange
//        var seller = new User { Id = 2, Username = "Seller2" };
//        _context.Users.Add(seller);

//        var wallet = new SellerWallet { Id = 2, SellerId = 2, AvailableBalance = 1000, LockedBalance = 500 };
//        _context.SellerWallets.Add(wallet);

//        var withdrawal = new WithdrawalRequest 
//        { 
//            Id = 2, 
//            SellerId = 2, 
//            Amount = 500, 
//            Status = WithdrawalRequest.StatusPending 
//        };
//        _context.WithdrawalRequests.Add(withdrawal);
//        await _context.SaveChangesAsync();

//        var handler = new RejectWithdrawalCommandHandler(_context, _userMock.Object);
//        var command = new RejectWithdrawalCommand(2, "Invalid details");

//        // Act
//        await handler.Handle(command, CancellationToken.None);

//        // Assert
//        var updatedWithdrawal = await _context.WithdrawalRequests.FindAsync(2);
//        updatedWithdrawal!.Status.ShouldBe(WithdrawalRequest.StatusRejected);
//        updatedWithdrawal.RejectionReason.ShouldBe("Invalid details");

//        var updatedWallet = await _context.SellerWallets.FindAsync(2);
//        updatedWallet!.LockedBalance.ShouldBe(0);
//        updatedWallet.AvailableBalance.ShouldBe(1500);
//    }
//}
