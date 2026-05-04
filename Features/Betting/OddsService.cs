using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.FantasyModel;
using BPFL.API.Shared;
using Microsoft.EntityFrameworkCore;
using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Features.Betting
{
    public class OddsService
    {
        private readonly BPFL_DBContext _db;
        private readonly MatchAnalysisService _analysisService;
        private readonly PredictionModelService _modelService;
        private readonly ILogger<OddsService> _logger;
        private readonly IAppCache _cache;

        private const double HouseEdge = 0.90;
        private const decimal MinOdds  = 1.05m;

        // Expected stats per match (used when we have no historic data)
        private const double AvgCorners     = 10.0;
        private const double AvgYellowCards = 3.8;

        private static readonly TimeSpan OddsTtl = TimeSpan.FromSeconds(30);

        public OddsService(
            BPFL_DBContext db,
            MatchAnalysisService analysisService,
            PredictionModelService modelService,
            ILogger<OddsService> logger,
            IAppCache cache)
        {
            _db              = db;
            _analysisService = analysisService;
            _modelService    = modelService;
            _logger          = logger;
            _cache           = cache;
        }

        public async Task EnsureOddsForUpcomingMatchesAsync(CancellationToken ct = default)
        {
            var matches = await _db.Matches
                .Where(m => m.Status != "FINISHED"
                         && m.MatchDate >= DateTime.UtcNow
                         && (m.HomeOdds == null
                             || m.ExpectedHomeGoals == null
                             || m.ExpectedHomeGoals < 0.7   // recalc if old bad value
                             || m.ExpectedAwayGoals < 0.5))
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .ToListAsync(ct);

            if (matches.Count == 0) return;

            foreach (var match in matches)
            {
                try
                {
                    var analysis = await _analysisService.AnalyzeMatch(match, ct);
                    var model = _modelService.BuildModel(analysis);

                    match.HomeOdds = ToOdds(model.HomeWinProbavility);
                    match.DrawOdds = ToOdds(model.DrawProbability);
                    match.AwayOdds = ToOdds(model.AwayWinProbability);
                    match.ExpectedHomeGoals = model.ExpectedHomeGoals;
                    match.ExpectedAwayGoals = model.ExpectedAwayGoals;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to calculate odds for match {MatchId}", match.Id);
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        /// <summary>Returns odds + description for any supported bet type on a given match.</summary>
        public async Task<BetOddsDTO?> GetDynamicOddsAsync(
            int matchId,
            BetType betType,
            MatchWinner?      winnerPick    = null,
            int?              scoreHome     = null,
            int?              scoreAway     = null,
            bool?             bttsPick      = null,
            OverUnderLine?    ouLine        = null,
            OverUnderPick?    ouPick        = null,
            int?              goalscorerId  = null,
            decimal?          lineValue     = null,
            DoubleChancePick? dcPick        = null,
            CancellationToken ct            = default)
        {
            // Build a deterministic cache key from all parameters
            var cacheKey = $"odds:{matchId}:{betType}:{winnerPick}:{scoreHome}:{scoreAway}:{bttsPick}:{ouLine}:{ouPick}:{goalscorerId}:{lineValue}:{dcPick}";
            var cached = await _cache.GetAsync<BetOddsDTO>(cacheKey, ct);
            if (cached != null) return cached;

            var match = await _db.Matches.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

            if (match?.ExpectedHomeGoals == null || match.ExpectedAwayGoals == null)
                return null;

            double lH = match.ExpectedHomeGoals.Value;
            double lA = match.ExpectedAwayGoals.Value;

            BetOddsDTO? result = betType switch
            {
                // ── 1 / X / 2 ──────────────────────────────────────────────
                BetType.Winner when winnerPick != null => new BetOddsDTO
                {
                    Odds = winnerPick switch
                    {
                        MatchWinner.Home => match.HomeOdds ?? ToOdds(WinnerProb(lH, lA, MatchWinner.Home)),
                        MatchWinner.Draw => match.DrawOdds ?? ToOdds(WinnerProb(lH, lA, MatchWinner.Draw)),
                        MatchWinner.Away => match.AwayOdds ?? ToOdds(WinnerProb(lH, lA, MatchWinner.Away)),
                        _ => MinOdds
                    },
                    Description = winnerPick.ToString()!
                },

                // ── Exact Score ─────────────────────────────────────────────
                BetType.ExactScore when scoreHome != null && scoreAway != null => new BetOddsDTO
                {
                    Odds        = ExactScoreOdds(lH, lA, scoreHome.Value, scoreAway.Value),
                    Description = $"{scoreHome}-{scoreAway}"
                },

                // ── BTTS ────────────────────────────────────────────────────
                BetType.BTTS when bttsPick != null => new BetOddsDTO
                {
                    Odds        = BTTSOdds(lH, lA, bttsPick.Value),
                    Description = bttsPick.Value ? "BTTS Yes" : "BTTS No"
                },

                // ── Over / Under Goals ──────────────────────────────────────
                BetType.OverUnder when ouLine != null && ouPick != null => new BetOddsDTO
                {
                    Odds        = OUOdds(lH, lA, ouLine.Value, ouPick.Value),
                    Description = $"{ouPick} {OULineValue(ouLine.Value)}"
                },

                // ── Corners O/U ─────────────────────────────────────────────
                BetType.Corners when lineValue != null && ouPick != null => new BetOddsDTO
                {
                    Odds        = ToOdds(ouPick == OverUnderPick.Over
                                    ? PoissonCdfOver(AvgCorners, (double)lineValue.Value)
                                    : 1 - PoissonCdfOver(AvgCorners, (double)lineValue.Value)),
                    Description = $"Corners {ouPick} {lineValue}"
                },

                // ── Yellow Cards O/U ────────────────────────────────────────
                BetType.YellowCards when lineValue != null && ouPick != null => new BetOddsDTO
                {
                    Odds        = ToOdds(ouPick == OverUnderPick.Over
                                    ? PoissonCdfOver(AvgYellowCards, (double)lineValue.Value)
                                    : 1 - PoissonCdfOver(AvgYellowCards, (double)lineValue.Value)),
                    Description = $"Yellow Cards {ouPick} {lineValue}"
                },

                // ── Double Chance ───────────────────────────────────────────
                BetType.DoubleChance when dcPick != null => new BetOddsDTO
                {
                    Odds = ToOdds(dcPick switch
                    {
                        DoubleChancePick.HomeOrDraw => WinnerProb(lH, lA, MatchWinner.Home) + WinnerProb(lH, lA, MatchWinner.Draw),
                        DoubleChancePick.HomeOrAway => WinnerProb(lH, lA, MatchWinner.Home) + WinnerProb(lH, lA, MatchWinner.Away),
                        DoubleChancePick.DrawOrAway => WinnerProb(lH, lA, MatchWinner.Draw) + WinnerProb(lH, lA, MatchWinner.Away),
                        _ => 0
                    }),
                    Description = $"Double Chance {dcPick switch { DoubleChancePick.HomeOrDraw => "1X", DoubleChancePick.HomeOrAway => "12", _ => "X2" }}"
                },

                _ => null
            };

            // Goalscorer requires async so handle separately
            if (result == null && betType == BetType.Goalscorer && goalscorerId != null)
                result = await GoalscorerOddsAsync(match, lH, lA, goalscorerId.Value, ct);

            if (result != null)
                await _cache.SetAsync(cacheKey, result, OddsTtl, ct);

            return result;
        }

        // ── Goalscorer odds ───────────────────────────────────────────────

        /// <summary>
        /// Returns a list of all players for both teams in a match with their goalscorer odds.
        /// Used by GET /api/Match/{matchId}/players.
        /// </summary>
        public async Task<List<MatchPlayerDTO>> GetMatchPlayersWithOddsAsync(
            int matchId, CancellationToken ct = default)
        {
            var match = await _db.Matches.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

            if (match?.ExpectedHomeGoals == null || match.ExpectedAwayGoals == null)
                return [];

            double lH = match.ExpectedHomeGoals.Value;
            double lA = match.ExpectedAwayGoals.Value;

            var homePlayers = await _db.FantasyPlayers.AsNoTracking()
                .Include(p => p.Team)
                .Where(p => p.TeamId == match.HomeTeamId && p.IsActive)
                .ToListAsync(ct);

            var awayPlayers = await _db.FantasyPlayers.AsNoTracking()
                .Include(p => p.Team)
                .Where(p => p.TeamId == match.AwayTeamId && p.IsActive)
                .ToListAsync(ct);

            var result = new List<MatchPlayerDTO>();

            foreach (var (players, lambda, isHome) in new[]
            {
                (homePlayers, lH, true),
                (awayPlayers, lA, false)
            })
            {
                var byPos = players.GroupBy(p => p.Position).ToDictionary(g => g.Key, g => g.Count());

                foreach (var p in players)
                {
                    int count = byPos.GetValueOrDefault(p.Position, 1);
                    decimal odds = GoalscorerPlayerOdds(lambda, p.Position, count);
                    result.Add(new MatchPlayerDTO
                    {
                        PlayerId = p.Id,
                        Name     = p.Name,
                        Position = p.Position.ToString(),
                        TeamName = p.Team.Name,
                        IsHome   = isHome,
                        Odds     = odds
                    });
                }
            }

            return [.. result.OrderBy(p => p.IsHome ? 0 : 1)
                              .ThenBy(p => PosOrder(p.Position))
                              .ThenBy(p => p.Name)];
        }

        private async Task<BetOddsDTO?> GoalscorerOddsAsync(
            Match match, double lH, double lA, int goalscorerId, CancellationToken ct)
        {
            var player = await _db.FantasyPlayers.AsNoTracking()
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.Id == goalscorerId, ct);

            if (player == null) return null;

            bool isHome  = player.TeamId == match.HomeTeamId;
            double lambda = isHome ? lH : lA;

            int countSamePos = await _db.FantasyPlayers.AsNoTracking()
                .CountAsync(p => p.TeamId == player.TeamId && p.Position == player.Position && p.IsActive, ct);
            if (countSamePos == 0) countSamePos = 1;

            decimal odds = GoalscorerPlayerOdds(lambda, player.Position, countSamePos);
            return new BetOddsDTO
            {
                Odds        = odds,
                Description = $"{player.Name} to score"
            };
        }

        private static decimal GoalscorerPlayerOdds(double teamLambda,
            FantasyPlayer.FantasyPosition pos, int countInPos)
        {
            double share = pos switch
            {
                FantasyPlayer.FantasyPosition.FWD => 0.50,
                FantasyPlayer.FantasyPosition.MID => 0.28,
                FantasyPlayer.FantasyPosition.DEF => 0.10,
                FantasyPlayer.FantasyPosition.GK  => 0.02,
                _                                 => 0.15
            };
            double lambdaPlayer = teamLambda * share / countInPos;
            double pScores      = 1 - Math.Exp(-lambdaPlayer);  // P(≥1 goal)
            return ToOdds(pScores);
        }

        private static int PosOrder(string pos) => pos switch
        {
            "GK"  => 0, "DEF" => 1, "MID" => 2, "FWD" => 3, _ => 9
        };

        // ── Poisson helpers ───────────────────────────────────────────────

        /// <summary>P(X > threshold) where X ~ Poisson(lambda) and threshold is a half-integer line.</summary>
        private static double PoissonCdfOver(double lambda, double threshold)
        {
            int floor = (int)Math.Floor(threshold);   // e.g. 9.5 → 9
            double pUnder = 0;
            for (int k = 0; k <= floor; k++)
                pUnder += Poisson(lambda, k);
            return 1 - pUnder;                        // P(X ≥ floor+1) = P(X > threshold)
        }

        private decimal ExactScoreOdds(double lH, double lA, int h, int a)
        {
            double prob = Poisson(lH, h) * Poisson(lA, a);
            return ToOdds(prob);
        }

        private decimal BTTSOdds(double lH, double lA, bool yes)
        {
            double pBTTS = (1 - Poisson(lH, 0)) * (1 - Poisson(lA, 0));
            return ToOdds(yes ? pBTTS : 1 - pBTTS);
        }

        private decimal OUOdds(double lH, double lA, OverUnderLine line, OverUnderPick pick)
        {
            double threshold = OULineValue(line);
            double pUnder = 0;
            int maxGoals = (int)threshold + 1;
            for (int h = 0; h <= maxGoals; h++)
                for (int a = 0; a <= maxGoals; a++)
                    if (h + a <= (int)threshold) pUnder += Poisson(lH, h) * Poisson(lA, a);

            double prob = pick == OverUnderPick.Under ? pUnder : 1 - pUnder;
            return ToOdds(prob);
        }

        private static double WinnerProb(double lH, double lA, MatchWinner winner)
        {
            double home = 0, draw = 0, away = 0;
            for (int h = 0; h <= 8; h++)
                for (int a = 0; a <= 8; a++)
                {
                    double p = Poisson(lH, h) * Poisson(lA, a);
                    if (h > a)      home += p;
                    else if (h == a) draw += p;
                    else             away += p;
                }
            return winner switch { MatchWinner.Home => home, MatchWinner.Draw => draw, _ => away };
        }

        private static double OULineValue(OverUnderLine line) => line switch
        {
            OverUnderLine.Line15 => 1.5,
            OverUnderLine.Line25 => 2.5,
            OverUnderLine.Line35 => 3.5,
            _                    => 2.5
        };

        private static double Poisson(double lambda, int k)
        {
            if (lambda <= 0) return k == 0 ? 1 : 0;
            return Math.Exp(-lambda) * Math.Pow(lambda, k) / Factorial(k);
        }

        private static double Factorial(int n)
        {
            double r = 1;
            for (int i = 2; i <= n; i++) r *= i;
            return r;
        }

        public static decimal ToOdds(double probability)
        {
            if (probability <= 0) return MinOdds;
            return Math.Max(Math.Round((decimal)(HouseEdge / probability), 2), MinOdds);
        }
    }
}
