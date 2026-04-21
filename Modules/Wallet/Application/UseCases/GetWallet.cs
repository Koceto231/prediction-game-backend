using BPFL.API.Modules.Wallet.Applications.DTOs;
using BPFL.API.Modules.Wallet.Applications.Interfaces;

namespace BPFL.API.Modules.Wallet.Applications.UseCases
{
    public class GetWallet
    {
        private readonly IWalletRepository walletRepository;

        public GetWallet(IWalletRepository _walletRepository)
        {
            walletRepository = _walletRepository;
        }

        public async Task<WalletResponse?> ExecuteAsync(int userId, CancellationToken ct = default)
        {
            var wallet = await walletRepository.GetByUserIdAsync(userId,ct);

            if (wallet == null)
            {
                return null;
            }

            return new WalletResponse
            {
                Balance = wallet.Balance,
                StartingBalance = wallet.StartingBalance,
                UpdatedAt = wallet.UpdatedAt
            };
        }
    }
}
