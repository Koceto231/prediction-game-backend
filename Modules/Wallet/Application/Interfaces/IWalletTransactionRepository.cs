using BPFL.API.Modules.Wallet.Domain.Entities;

namespace BPFL.API.Modules.Wallet.Applications.Interfaces
{
    public interface IWalletTransactionRepository
    {
        Task AddAsync(WalletTransaction transaction, CancellationToken ct = default);
    }
}
