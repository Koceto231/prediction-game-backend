using BPFL.API.Data;
using BPFL.API.Modules.Bettings.Application.DTOs;
using BPFL.API.Modules.Bettings.Application.Interfaces;
using BPFL.API.Modules.Bettings.Domain.Entities;
using BPFL.API.Modules.Odds.Application.Interfaces;
using BPFL.API.Modules.Wallet.Applications.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Modules.Bettings.Application.UseCases
{
    public class PlaceBetUseCase
    {

        private readonly IBetRepository betRepository;
        private readonly IWalletRepository walletRepository;
        private readonly IWalletTransactionRepository walletTransactionRepository;
        private readonly IMatchMarketOddsRepository matchMarketOddsRepository;
        private readonly BPFL_DBContext bPFL_DBContext;

        public PlaceBetUseCase(IBetRepository _betRepository, IWalletRepository _walletRepository,
            IWalletTransactionRepository _walletTransactionRepository, IMatchMarketOddsRepository _matchMarketOddsRepository, BPFL_DBContext _bPFL_DBContext)
        {
            
            betRepository = _betRepository;
            walletRepository = _walletRepository;
            walletTransactionRepository = _walletTransactionRepository;
            matchMarketOddsRepository = _matchMarketOddsRepository;
           bPFL_DBContext = _bPFL_DBContext;
        }

        public async Task<PlaceBetResponseDTO?> ExecuteAsync(int userId,
            PlaceBetRequestDTO placeBetRequestDTO,
            CancellationToken ct = default)
        {
            if (placeBetRequestDTO.Stake <= 0)
            {
                throw new Exception("Stake must be greater than zero.");
            }

            var wallet = await bPFL_DBContext.Wallets.FirstOrDefaultAsync(x => x.UserId == userId, ct);

            if (wallet == null)
                throw new Exception("Wallet not found.");

            if (wallet.Balance < placeBetRequestDTO.Stake)
                throw new Exception("Insufficient balance.");

            var match = await bPFL_DBContext.Matches.FirstOrDefaultAsync(x => x.Id == placeBetRequestDTO.MatchId, ct);


            if (match == null)
                throw new Exception("Match not found.");

            if (match.MatchDate <= DateTime.UtcNow)
                throw new Exception("Match already started.");

            var odds = await matchMarketOddsRepository.GetExactAsync(
                  placeBetRequestDTO.MatchId,
                  placeBetRequestDTO.MarketCode,
                  placeBetRequestDTO.SelectionCode,
                  placeBetRequestDTO.PlayerId,
                  placeBetRequestDTO.LineValue,
    ct);

            if (odds == null)
                throw new Exception("Odds not found.");

            var potentialReturn = placeBetRequestDTO.Stake * odds.Odds;

            var bet = new Bet
            {
                UserId = userId,
                MatchId = placeBetRequestDTO.MatchId,
                MarketCode = placeBetRequestDTO.MarketCode,
                SelectionCode = placeBetRequestDTO.SelectionCode,
                PlayerId = placeBetRequestDTO.PlayerId,
                LineValue = placeBetRequestDTO.LineValue,
                Odds = odds.Odds,
                Stake = placeBetRequestDTO.Stake,
                PotentialReturn = potentialReturn,
                CreatedAt = DateTime.UtcNow
            };

            await betRepository.AddAsync(bet);

            wallet.Balance -= placeBetRequestDTO.Stake;
            wallet.UpdatedAt = DateTime.UtcNow;

            await walletRepository.UpdateAsync(wallet, ct);

            await walletTransactionRepository.AddAsync(new Wallet.Domain.Entities.WalletTransaction
            {
                UserId = userId,
                Amount = -placeBetRequestDTO.Stake,
                Type = "BetPlaced",
                Description = $"Bet placed on match {placeBetRequestDTO.MatchId}",
                CreatedAt = DateTime.UtcNow
            }, ct);

            await betRepository.SaveChangesAsync(ct);

            return new PlaceBetResponseDTO {
                BetId = bet.Id,
                Stake = bet.Stake,
                Odds = bet.Odds,
                PotentialReturn = bet.PotentialReturn,
                Status = bet.Status,
                RemainingBalance = wallet.Balance
            };
        }

    }
}
