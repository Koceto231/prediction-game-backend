using BPFL.API.Modules.Wallet.Applications.Interfaces;
using BPFL.API.Modules.Wallet.Infrastructures.Repositories;

namespace BPFL.API.Modules.Wallet.Applications.UseCases
{
    public class ResetDemoBalanceUseCase
    {
        private readonly IWalletRepository walletRepository;
        private readonly WalletTransactionRepository walletTransaction;

        public ResetDemoBalanceUseCase(IWalletRepository _walletRepository, WalletTransactionRepository _walletTransaction)
        {
            walletRepository = _walletRepository;
            walletTransaction = _walletTransaction;
        }

        public async Task<decimal?> ExecuteAsync(int userId, CancellationToken ct = default) 
        { 
            var wallet = await walletRepository.GetByUserIdAsync(userId,ct);

            if (wallet == null)
                return null;


            wallet.Balance = wallet.StartingBalance;
            wallet.UpdatedAt = DateTime.UtcNow;

            await walletRepository.UpdateAsync(wallet);

            await walletTransaction.AddAsync(new Domain.Entities.WalletTransaction
            {
                UserId = userId,
                Amount = wallet.StartingBalance,
                Type = "Reset",
                Description = "Demo balance reset",
                CreatedAt = DateTime.UtcNow
            }, ct);

            await walletRepository.SaveChangesAsync(ct);

            return wallet.Balance;
        }
    }
}
