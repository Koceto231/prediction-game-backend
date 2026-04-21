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
        private readonly OddsService _oddsService;
        private readonly ILogger<BetService> _logger;

        public BetService(BPFL_DBContext db, OddsService oddsService, ILogger<BetService> logger)
        {
            _db = db;
            _oddsService = oddsService;
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

            var oddsResult = await _oddsService.GetDynamicOddsAsync(
                dto.MatchId, dto.BetType,
                dto.Pick, dto.ScoreHome, dto.ScoreAway,
                dto.BTTSPick, dto.OULine, dto.OUPick, ct)
                ?? throw new InvalidOperationException("Odds not available for this bet type.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new KeyNotFoundException("User not found.");

            if (user.Balance < dto.Amount)
                throw new InvalidOperationException("Insufficient balance.");

            user.Balance -= dto.Amount;

            var bet = new Bet
            {
                UserId = userId,
                MatchId = dto.MatchId,
                BetType = dto.BetType,
                Pick = dto.Pick,
                ScoreHome = dto.ScoreHome,
                ScoreAway = dto.ScoreAway,
                BTTSPick = dto.BTTSPick,
                OULine = dto.OULine,
                OUPick = dto.OUPick,
                Amount = dto.Amount,
                OddsAtBetTime = oddsResult.Odds,
                PotentialPayout = Math.Round(dto.Amount * oddsResult.Odds, 2),
                Status = BetStatus.Pending
            };

            _db.Bets.Add(bet);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Bet placed: UserId={UserId} MatchId={MatchId} Type={Type} Desc={Desc} Amount={Amount} Odds={Odds}",
                userId, dto.MatchId, dto.BetType, oddsResult.Description, dto.Amount, oddsResult.Odds);

            return ToDTO(bet, match, oddsResult.Description);
        }

        public async Task<List<BetResponseDTO>> GetMyBetsAsync(int userId, CancellationToken ct = default)
        {
            var bets = await _db.Bets
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .Include(b => b.Match).ThenInclude(m => m.HomeTeam)
                .Include(b => b.Match).ThenInclude(m => m.AwayTeam)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync(ct);

            return bets.Select(b => ToDTO(b, b.Match, BuildDescription(b, b.Match))).ToList();
        }

        public async Task ResolveMatchBetsAsync(int matchId, int homeScore, int awayScore, CancellationToken ct = default)
        {
            var bets = await _db.Bets
                .Where(b => b.MatchId == matchId && b.Status == BetStatus.Pending)
                .Include(b => b.User)
                .ToListAsync(ct);

            if (bets.Count == 0) return;

            var actualWinner = homeScore > awayScore ? MatchWinner.Home
                : awayScore > homeScore ? MatchWinner.Away
                : MatchWinner.Draw;
            bool actualBTTS = homeScore > 0 && awayScore > 0;
            int totalGoals = homeScore + awayScore;

            foreach (var bet in bets)
            {
                bool won = bet.BetType switch
                {
                    BetType.Winner => bet.Pick == actualWinner,
                    BetType.ExactScore => bet.ScoreHome == homeScore && bet.ScoreAway == awayScore,
                    BetType.BTTS => bet.BTTSPick == actualBTTS,
                    BetType.OverUnder => bet.OULine != null && bet.OUPick != null &&
                        IsOUWin(totalGoals, bet.OULine.Value, bet.OUPick.Value),
                    _ => false
                };

                bet.Status = won ? BetStatus.Won : BetStatus.Lost;
                bet.ActualPayout = won ? bet.PotentialPayout : 0;
                if (won) bet.User.Balance += bet.PotentialPayout;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Resolved {Count} bets for match {MatchId}", bets.Count, matchId);
        }

        private static bool IsOUWin(int totalGoals, OverUnderLine line, OverUnderPick pick)
        {
            double threshold = line switch
            {
                OverUnderLine.Line15 => 1.5,
                OverUnderLine.Line25 => 2.5,
                OverUnderLine.Line35 => 3.5,
                _ => 2.5
            };
            return pick == OverUnderPick.Over ? totalGoals > threshold : totalGoals < threshold;
        }

        private static string BuildDescription(Bet bet, Match match) => bet.BetType switch
        {
            BetType.Winner => bet.Pick switch
            {
                MatchWinner.Home => $"1 — {match.HomeTeam?.Name ?? "Home"}",
                MatchWinner.Draw => "X — Draw",
                MatchWinner.Away => $"2 — {match.AwayTeam?.Name ?? "Away"}",
                _                => "Winner"
            },
            BetType.ExactScore => $"{bet.ScoreHome}–{bet.ScoreAway}",
            BetType.BTTS       => bet.BTTSPick == true ? "BTTS Yes" : "BTTS No",
            BetType.OverUnder  => $"{bet.OUPick} {OULabel(bet.OULine)}",
            _                  => "Bet"
        };

        private static int MaxPoints(BetType type) => type switch
        {
            BetType.ExactScore => 5,
            BetType.Winner     => 1,
            BetType.BTTS       => 1,
            BetType.OverUnder  => 1,
            _                  => 0
        };

        private static string OULabel(OverUnderLine? line) => line switch
        {
            OverUnderLine.Line15 => "1.5",
            OverUnderLine.Line25 => "2.5",
            OverUnderLine.Line35 => "3.5",
            _ => ""
        };

        private static BetResponseDTO ToDTO(Bet bet, Match match, string desc) => new()
        {
            Id = bet.Id,
            MatchId = bet.MatchId,
            HomeTeam = match.HomeTeam.Name,
            AwayTeam = match.AwayTeam.Name,
            MatchDate = match.MatchDate,
            BetType = bet.BetType,
            BetDescription = desc,
            Amount = bet.Amount,
            OddsAtBetTime = bet.OddsAtBetTime,
            PotentialPayout = bet.PotentialPayout,
            Status = bet.Status,
            ActualPayout = bet.ActualPayout,
            CreatedAt = bet.CreatedAt,
            MaxPoints = MaxPoints(bet.BetType)
        };
    }
}
