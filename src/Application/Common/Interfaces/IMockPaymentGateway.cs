namespace EbayClone.Application.Common.Interfaces;

public record PaymentResult(bool Success, string? ErrorMessage);

/// <summary>
/// Abstraction over the (mock) payment gateway used by the Payout Engine.
/// In production, swap the implementation with a real bank-transfer provider.
/// </summary>
public interface IMockPaymentGateway
{
    /// <summary>
    /// Simulates a bank transfer.
    /// 90% → Success, 10% → Failed with a random error reason.
    /// </summary>
    /// <param name="amount">Amount to transfer in USD.</param>
    /// <param name="bankInfoJson">JSON string with bankName, accountNumber, accountName.</param>
    Task<PaymentResult> ProcessAsync(decimal amount, string bankInfoJson);
}
