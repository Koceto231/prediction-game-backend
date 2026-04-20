using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;
using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Services
{
    public class BetService
    {
        private readonly BPFL_DBContext _db;
        private readonly ILogger<BetService> _logger;

        public BetService(BPFL_DBContext db, ILogger<BetService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<BetResponseDTO> PlaceBetAsync(int userId, PlaceBetDTO dto, CancellationToken ct = default)
        {
            if (dto.Amount <= 0)
                throw new ArgumentException("Bet amount must be greater than zero.");

            var match = await _db.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == dto.MatchId, ct)
                ?? throw new KeyNotFoundException($"Match {dto.MatchId} not found.");

            if (match.MatchDate <= DateTime.UtcNow)
                throw new InvalidOperationException("Cannot place a bet after the match has started.");

            var odds = dto.Pick switch
            {
                MatchWinner.Home => match.HomeOdds,
                MatchWinner.Draw => match.DrawOdds,
                MatchWinner.Away => match.AwayOdds,
                _ => throw new ArgumentException("Invalid pick.")
            } ?? throw new InvalidOperationException("Odds are not available for this match yet.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new KeyNotFoundException("User not found.");

            if (user.Balance < dto.Amount)
                throw new InvalidOperationException("Insufficient balance.");

            user.Balance -= dto.Amount;

            var bet = new Bet
            {
                UserId = userId,
                MatchId = dto.MatchId,
                Pick = dto.Pick,
                Amount = dto.Amount,
                OddsAtBetTime = odds,
                PotentialPayout = Math.Round(dto.Amount * odds, 2),
                Status = BetStatus.Pending
            };

            _db.Bets.Add(bet);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Bet placed: UserId={UserId} MatchId={MatchId} Pick={Pick} Amount={Amount} Odds={Odds}",
                userId, dto.MatchId, dto.Pick, dto.Amount, odds);

            return ToDTO(bet, match);
        }

        public async Task<List<BetResponseDTO>> GetMyBetsAsync(int userId, CancellationToken ct = default)
        {
            return await _db.Bets
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .Include(b => b.Match).ThenInclude(m => m.HomeTeam)
                .Include(b => b.Match).ThenInclude(m => m.AwayTeam)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BetResponseDTO
                {
                    Id = b.Id,
                    MatchId = b.MatchId,
                    HomeTeam = b.Match.HomeTeam.Name,
                    AwayTeam = b.Match.AwayTeam.Name,
                    MatchDate = b.Match.MatchDate,
                    Pick = b.Pick.ToString(),
                    Amount = b.Amount,
                    OddsAtBetTime = b.OddsAtBetTime,
                    PotentialPayout = b.PotentialPayout,
                    Status = b.Status,
                    ActualPayout = b.ActualPayout,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync(ct);
        }

        public async Task ResolveMatchBetsAsync(int matchId, MatchWinner winner, CancellationToken ct = default)
        {
            var bets = await _db.Bets
                .Where(b => b.MatchId == matchId && b.Status == BetStatus.Pending)
                .Include(b => b.User)
                .ToListAsync(ct);

            if (bets.Count == 0) return;

            foreach (var bet in bets)
            {
                if (bet.Pick == winner)
                {
                    bet.Status = BetStatus.Won;
                    bet.ActualPayout = bet.PotentialPayout;
                    bet.User.Balance += bet.PotentialPayout;
                }
                else
                {
                    bet.Status = BetStatus.Lost;
                    bet.ActualPayout = 0;
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Resolved {Count} bets for match {MatchId}", bets.Count, matchId);
        }

        private static BetResponseDTO ToDTO(Bet bet, Match match) => new()
        {
            Id = bet.Id,
            MatchId = bet.MatchId,
            HomeTeam = match.HomeTeam.Name,
            AwayTeam = match.AwayTeam.Name,
            MatchDate = match.MatchDate,
            Pick = bet.Pick.ToString(),
            Amount = bet.Amount,
            OddsAtBetTime = bet.OddsAtBetTime,
            PotentialPayout = bet.PotentialPayout,
            Status = bet.Status,
            ActualPayout = bet.ActualPayout,
            CreatedAt = bet.CreatedAt
        };
    }
}
