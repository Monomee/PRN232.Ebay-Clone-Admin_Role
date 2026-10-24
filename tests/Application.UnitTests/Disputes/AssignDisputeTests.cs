using Ardalis.GuardClauses;
using EbayClone.Application.Admin.Disputes.Commands.AssignDispute;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace EbayClone.Application.UnitTests.Disputes;

/// <summary>
/// W7-20 — AssignDisputeCommandHandler: giao dispute cho một admin xử lý.
/// </summary>
public class AssignDisputeTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IUser> _userMock = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userMock = new Mock<IUser>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private AssignDisputeCommandHandler CreateHandler() =>
        new(_context, _userMock.Object, NullLogger<AssignDisputeCommandHandler>.Instance);

    private async Task SeedDisputeAsync(string status)
    {
        _context.Disputes.Add(new Dispute { Id = 1, CaseId = "CASE-1", Status = status });
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task Handle_EscalatedDispute_ShouldAssignToCurrentAdminAndChangeStatus()
    {
        // Arrange
        await SeedDisputeAsync(DisputeStatuses.Escalated);
        _userMock.Setup(u => u.Id).Returns("42");

        // Act
        var result = await CreateHandler().Handle(new AssignDisputeCommand(1), CancellationToken.None);

        // Assert
        result.ShouldBeTrue();

        var dispute = await _context.Disputes.SingleAsync(d => d.Id == 1);
        dispute.AssignedTo.ShouldBe(42);
        dispute.AssignedAt.ShouldNotBeNull();
        dispute.Status.ShouldBe(DisputeStatuses.AssignedToAdmin);

        var message = await _context.DisputeMessages.SingleAsync();
        message.DisputeId.ShouldBe(1);
        message.IsInternal.ShouldBeTrue();

        (await _context.AdminActions.CountAsync(a => a.Action == "AssignDispute" && a.AdminId == 42)).ShouldBe(1);
    }

    [Test]
    public async Task Handle_ExplicitAdminId_ShouldOverrideCurrentUser()
    {
        // Arrange: dispute chưa escalate thì giữ nguyên trạng thái
        await SeedDisputeAsync(DisputeStatuses.Open);
        _userMock.Setup(u => u.Id).Returns("42");

        // Act
        await CreateHandler().Handle(new AssignDisputeCommand(1, AdminId: 7), CancellationToken.None);

        // Assert
        var dispute = await _context.Disputes.SingleAsync(d => d.Id == 1);
        dispute.AssignedTo.ShouldBe(7);
        dispute.Status.ShouldBe(DisputeStatuses.Open);
    }

    [Test]
    public void Validator_InvalidDisputeId_ShouldReturnError()
    {
        var result = new AssignDisputeCommandValidator().Validate(new AssignDisputeCommand(0));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(AssignDisputeCommand.DisputeId));
    }

    [Test]
    public async Task Handle_DisputeNotFound_ShouldThrowNotFoundException()
    {
        _userMock.Setup(u => u.Id).Returns("42");

        await Should.ThrowAsync<NotFoundException>(
            () => CreateHandler().Handle(new AssignDisputeCommand(999), CancellationToken.None));
    }

    [Test]
    public async Task Handle_CannotDetermineAdmin_ShouldThrowAndNotAssign()
    {
        // Arrange: không có AdminId trong lệnh và user hiện tại không có Id hợp lệ
        await SeedDisputeAsync(DisputeStatuses.Escalated);
        _userMock.Setup(u => u.Id).Returns((string?)null);

        // Act + Assert
        await Should.ThrowAsync<ValidationException>(
            () => CreateHandler().Handle(new AssignDisputeCommand(1), CancellationToken.None));

        var dispute = await _context.Disputes.SingleAsync(d => d.Id == 1);
        dispute.AssignedTo.ShouldBeNull();
        (await _context.AdminActions.CountAsync()).ShouldBe(0);
    }
}
