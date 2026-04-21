using BPFL.API.Data;
using BPFL.API.Modules.Wallet.Applications.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Modules.Wallet.Infrastructures.Repositories
{
    public class WalletRepository : IWalletRepository
    {
        private readonly BPFL_DBContext bPFL_DBContext;

        public WalletRepository(BPFL_DBContext _bPFL_DBContext)
        {
            bPFL_DBContext = _bPFL_DBContext;
        }

        public async Task AddAsync(Domain.Entities.Wallet wallet, CancellationToken ct = default)
        {
            await bPFL_DBContext.Wallets.AddAsync(wallet, ct);
        }

        public async Task<Domain.Entities.Wallet?> GetByUserIdAsync(int userId, CancellationToken ct = default)
        {
            return await bPFL_DBContext.Wallets.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId,ct);   
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await bPFL_DBContext.SaveChangesAsync(ct);
        }

        public Task UpdateAsync(Domain.Entities.Wallet wallet, CancellationToken ct = default)
        {
            bPFL_DBContext.Wallets.Update(wallet);
            return Task.CompletedTask;
        }
    }
}
