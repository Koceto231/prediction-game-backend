using BPFL.API.Data;
using BPFL.API.Models;
using Microsoft.EntityFrameworkCore;
using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Features.Betting
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
                dto.BTTSPick, dto.OULine, dto.OUPick,
                dto.GoalscorerId, dto.LineValue, dto.DCPick, ct)
                ?? throw new InvalidOperationException("Odds not available for this bet type.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                ?? throw new KeyNotFoundException("User not found.");

            if (user.Balance < dto.Amount)
                throw new InvalidOperationException("Insufficient balance.");

            user.Balance -= dto.Amount;

            var bet = new Bet
            {
                UserId          = userId,
                MatchId         = dto.MatchId,
                BetType         = dto.BetType,
                Pick            = dto.Pick,
                ScoreHome       = dto.ScoreHome,
                ScoreAway       = dto.ScoreAway,
                BTTSPick        = dto.BTTSPick,
                OULine          = dto.OULine,
                OUPick          = dto.OUPick,
                GoalscorerId    = dto.GoalscorerId,
                LineValue       = dto.LineValue,
                DCPick          = dto.DCPick,
                Amount          = dto.Amount,
                OddsAtBetTime   = oddsResult.Odds,
                PotentialPayout = Math.Round(dto.Amount * oddsResult.Odds, 2),
                Status          = BetStatus.Pending
            };

            _db.Bets.Add(bet);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Bet placed: UserId={UserId} MatchId={MatchId} Type={Type} Desc={Desc} Amount={Amount} Odds={Odds}",
                userId, dto.MatchId, dto.BetType, oddsResult.Description, dto.Amount, oddsResult.Odds);

            string? scorerName = null;
            if (dto.GoalscorerId != null)
                scorerName = (await _db.FantasyPlayers.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == dto.GoalscorerId, ct))?.Name;

            return ToDTO(bet, match, oddsResult.Description, scorerName);
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

            // Resolve goalscorer names in one query
            var scorerIds = bets.Where(b => b.GoalscorerId != null)
                                .Select(b => b.GoalscorerId!.Value).Distinct().ToList();
            var scorerNames = scorerIds.Count > 0
                ? await _db.FantasyPlayers.AsNoTracking()
                    .Where(p => scorerIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Name, ct)
                : [];

            return bets.Select(b =>
            {
                string? sName = b.GoalscorerId != null && scorerNames.TryGetValue(b.GoalscorerId.Value, out var n) ? n : null;
                return ToDTO(b, b.Match, BuildDescription(b, b.Match, sName), sName);
            }).ToList();
        }

        public async Task ResolveMatchBetsAsync(int matchId, int homeScore, int awayScore, CancellationToken ct = default)
        {
            var bets = await _db.Bets
                .Where(b => b.MatchId == matchId && b.Status == BetStatus.Pending)
                .Include(b => b.User)
                .ToListAsync(ct);

            if (bets.Count == 0) return;

            var actualWinner = homeScore > awayScore ? MatchWinner.Home
                : awayScore > homeScore ? MatchWinner.Away : MatchWinner.Draw;
            bool actualBTTS = homeScore > 0 && awayScore > 0;
            int  totalGoals = homeScore + awayScore;

            // Goalscorer — which players scored in this match
            var scorerBets = bets.Where(b => b.BetType == BetType.Goalscorer && b.GoalscorerId != null).ToList();
            HashSet<int> scorers = [];
            if (scorerBets.Count > 0)
            {
                var goalStats = await _db.PlayerMatchFantasyStats.AsNoTracking()
                    .Where(s => s.MatchId == matchId && s.Goals > 0)
                    .Select(s => s.FantasyPlayerId)
                    .ToListAsync(ct);
                scorers = [.. goalStats];
            }

            // Corners / Yellow cards — read from Match
            var match = await _db.Matches.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

            foreach (var bet in bets)
            {
                bool won = bet.BetType switch
                {
                    BetType.Winner      => bet.Pick == actualWinner,
                    BetType.ExactScore  => bet.ScoreHome == homeScore && bet.ScoreAway == awayScore,
                    BetType.BTTS        => bet.BTTSPick == actualBTTS,
                    BetType.OverUnder   => bet.OULine != null && bet.OUPick != null &&
                                          IsOUWin(totalGoals, bet.OULine.Value, bet.OUPick.Value),
                    BetType.Goalscorer  => bet.GoalscorerId != null && scorers.Contains(bet.GoalscorerId.Value),
                    BetType.Corners     => bet.LineValue != null && bet.OUPick != null && match?.TotalCorners != null &&
                                          IsSpecialOUWin(match.TotalCorners.Value, (double)bet.LineValue.Value, bet.OUPick.Value),
                    BetType.YellowCards => bet.LineValue != null && bet.OUPick != null && match?.TotalYellowCards != null &&
                                          IsSpecialOUWin(match.TotalYellowCards.Value, (double)bet.LineValue.Value, bet.OUPick.Value),
                    BetType.DoubleChance => bet.DCPick != null && IsDCWin(actualWinner, bet.DCPick.Value),
                    _                   => false
                };

                bet.Status      = won ? BetStatus.Won : BetStatus.Lost;
                bet.ActualPayout = won ? bet.PotentialPayout : 0;
                if (won) bet.User.Balance += bet.PotentialPayout;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Resolved {Count} bets for match {MatchId}", bets.Count, matchId);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static bool IsOUWin(int total, OverUnderLine line, OverUnderPick pick)
        {
            double thr = line switch
            {
                OverUnderLine.Line15 => 1.5, OverUnderLine.Line25 => 2.5, OverUnderLine.Line35 => 3.5, _ => 2.5
            };
            return pick == OverUnderPick.Over ? total > thr : total < thr;
        }

        private static bool IsSpecialOUWin(int total, double line, OverUnderPick pick) =>
            pick == OverUnderPick.Over ? total > line : total < line;

        private static bool IsDCWin(MatchWinner actual, DoubleChancePick dc) => dc switch
        {
            DoubleChancePick.HomeOrDraw => actual is MatchWinner.Home or MatchWinner.Draw,
            DoubleChancePick.HomeOrAway => actual is MatchWinner.Home or MatchWinner.Away,
            DoubleChancePick.DrawOrAway => actual is MatchWinner.Draw or MatchWinner.Away,
            _                           => false
        };

        private static string BuildDescription(Bet bet, Match match, string? scorerName = null) => bet.BetType switch
        {
            BetType.Winner       => bet.Pick switch
            {
                MatchWinner.Home => $"1 — {match.HomeTeam?.Name ?? "Home"}",
                MatchWinner.Draw => "X — Draw",
                MatchWinner.Away => $"2 — {match.AwayTeam?.Name ?? "Away"}",
                _                => "Winner"
            },
            BetType.ExactScore   => $"{bet.ScoreHome}–{bet.ScoreAway}",
            BetType.BTTS         => bet.BTTSPick == true ? "BTTS Yes" : "BTTS No",
            BetType.OverUnder    => $"{bet.OUPick} {OULabel(bet.OULine)}",
            BetType.Goalscorer   => $"{scorerName ?? "Player"} to score",
            BetType.Corners      => $"Corners {bet.OUPick} {bet.LineValue}",
            BetType.YellowCards  => $"Yellow Cards {bet.OUPick} {bet.LineValue}",
            BetType.DoubleChance => bet.DCPick switch
            {
                DoubleChancePick.HomeOrDraw => $"1X ({match.HomeTeam?.Name ?? "Home"} or Draw)",
                DoubleChancePick.HomeOrAway => $"12 ({match.HomeTeam?.Name ?? "Home"} or {match.AwayTeam?.Name ?? "Away"})",
                DoubleChancePick.DrawOrAway => $"X2 (Draw or {match.AwayTeam?.Name ?? "Away"})",
                _                           => "Double Chance"
            },
            _ => "Bet"
        };

        private static int MaxPoints(BetType type) => type switch
        {
            BetType.ExactScore   => 5,
            BetType.Winner       => 1,
            BetType.BTTS         => 1,
            BetType.OverUnder    => 1,
            BetType.Goalscorer   => 2,
            BetType.DoubleChance => 1,
            BetType.Corners      => 0,
            BetType.YellowCards  => 0,
            _                    => 0
        };

        private static string OULabel(OverUnderLine? line) => line switch
        {
            OverUnderLine.Line15 => "1.5", OverUnderLine.Line25 => "2.5",
            OverUnderLine.Line35 => "3.5", _                    => ""
        };

        private static BetResponseDTO ToDTO(Bet bet, Match match, string desc, string? scorerName = null) => new()
        {
            Id              = bet.Id,
            MatchId         = bet.MatchId,
            HomeTeam        = match.HomeTeam.Name,
            AwayTeam        = match.AwayTeam.Name,
            MatchDate       = match.MatchDate,
            BetType         = bet.BetType,
            BetDescription  = desc,
            Amount          = bet.Amount,
            OddsAtBetTime   = bet.OddsAtBetTime,
            PotentialPayout = bet.PotentialPayout,
            Status          = bet.Status,
            ActualPayout    = bet.ActualPayout,
            CreatedAt       = bet.CreatedAt,
            MaxPoints       = MaxPoints(bet.BetType),
            GoalscorerName  = scorerName
        };
    }
}
