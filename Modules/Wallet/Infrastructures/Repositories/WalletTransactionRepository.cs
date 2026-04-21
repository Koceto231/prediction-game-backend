using BPFL.API.Data;
using BPFL.API.Modules.Wallet.Applications.Interfaces;
using BPFL.API.Modules.Wallet.Domain.Entities;

namespace BPFL.API.Modules.Wallet.Infrastructures.Repositories
{
    public class WalletTransactionRepository : IWalletTransactionRepository
    {

        private readonly BPFL_DBContext bPFL_DBContext;

        public WalletTransactionRepository(BPFL_DBContext _bPFL_DBContext)
        {
            bPFL_DBContext = _bPFL_DBContext;
        }
        public async Task AddAsync(WalletTransaction transaction, CancellationToken ct = default)
        {
           await bPFL_DBContext.WalletTransactions.AddAsync(transaction, ct);
        }
    }
}
