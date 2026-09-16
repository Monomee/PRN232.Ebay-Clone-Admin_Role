using EbayClone.Application.Common.Interfaces;

namespace EbayClone.Infrastructure.Services;

/// <summary>
/// Simulates a bank transfer payment gateway.
/// Probability: 90% Success, 10% Failed with a randomised error reason.
/// </summary>
public class MockPaymentGatewayService : IMockPaymentGateway
{
    private static readonly string[] ErrorReasons =
    [
        "Bank connection timeout",
        "Destination account closed",
        "Invalid account number format",
        "Daily transfer limit exceeded",
        "Insufficient bank liquidity",
        "Anti-fraud block triggered"
    ];

    public async Task<PaymentResult> ProcessAsync(decimal amount, string bankInfoJson)
    {
        // Simulate network latency (100–500 ms)
        await Task.Delay(Random.Shared.Next(100, 501));

        bool success = Random.Shared.NextDouble() < 0.90;

        if (success)
            return new PaymentResult(true, null);

        var errorReason = ErrorReasons[Random.Shared.Next(ErrorReasons.Length)];
        return new PaymentResult(false, errorReason);
    }
}
