using BPFL.API.Modules.Wallet.Domain.Entities;

namespace BPFL.API.Modules.Wallet.Applications.Interfaces
{
    public interface IWalletRepository
    {
        Task<Domain.Entities.Wallet?> GetByUserIdAsync(int userId, CancellationToken ct = default);

        Task AddAsync(Domain.Entities.Wallet wallet, CancellationToken ct = default);
        Task UpdateAsync(Domain.Entities.Wallet wallet, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
