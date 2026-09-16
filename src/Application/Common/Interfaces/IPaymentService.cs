using System.Threading;
using System.Threading.Tasks;

namespace EbayClone.Application.Common.Interfaces;

public interface IPaymentService
{
    Task HoldEscrowFundsAsync(int sellerId, decimal amount, CancellationToken cancellationToken);
    Task ExecuteRefundAsync(int buyerId, int sellerId, decimal amount, bool useEbayFund, CancellationToken cancellationToken);
}
