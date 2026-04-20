using BPFL.API.Data;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;
using static BPFL.API.Models.Predictionenums;

namespace BPFL.API.Services
{
    public class OddsService
    {
        private readonly BPFL_DBContext _db;
        private readonly MatchAnalysisService _analysisService;
        private readonly PredictionModelService _modelService;
        private readonly ILogger<OddsService> _logger;

        private const double HouseEdge = 0.90;
        private const decimal MinOdds = 1.05m;

        public OddsService(
            BPFL_DBContext db,
            MatchAnalysisService analysisService,
            PredictionModelService modelService,
            ILogger<OddsService> logger)
        {
            _db = db;
            _analysisService = analysisService;
            _modelService = modelService;
            _logger = logger;
        }

        public async Task EnsureOddsForUpcomingMatchesAsync(CancellationToken ct = default)
        {
            var matches = await _db.Matches
                .Where(m => m.Status != "FINISHED"
                         && m.MatchDate >= DateTime.UtcNow
                         && m.HomeOdds == null)
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

        public async Task<BetOddsDTO?> GetDynamicOddsAsync(
            int matchId,
            BetType betType,
            MatchWinner? winnerPick = null,
            int? scoreHome = null,
            int? scoreAway = null,
            bool? bttsPick = null,
            OverUnderLine? ouLine = null,
            OverUnderPick? ouPick = null,
            CancellationToken ct = default)
        {
            var match = await _db.Matches.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

            if (match?.ExpectedHomeGoals == null || match.ExpectedAwayGoals == null)
                return null;

            double lambdaH = match.ExpectedHomeGoals.Value;
            double lambdaA = match.ExpectedAwayGoals.Value;

            return betType switch
            {
                BetType.Winner => winnerPick == null ? null : new BetOddsDTO
                {
                    Odds = winnerPick switch
                    {
                        MatchWinner.Home => match.HomeOdds ?? ToOdds(WinnerProb(lambdaH, lambdaA, MatchWinner.Home)),
                        MatchWinner.Draw => match.DrawOdds ?? ToOdds(WinnerProb(lambdaH, lambdaA, MatchWinner.Draw)),
                        MatchWinner.Away => match.AwayOdds ?? ToOdds(WinnerProb(lambdaH, lambdaA, MatchWinner.Away)),
                        _ => MinOdds
                    },
                    Description = winnerPick.ToString()!
                },

                BetType.ExactScore when scoreHome != null && scoreAway != null => new BetOddsDTO
                {
                    Odds = ExactScoreOdds(lambdaH, lambdaA, scoreHome.Value, scoreAway.Value),
                    Description = $"{scoreHome}-{scoreAway}"
                },

                BetType.BTTS when bttsPick != null => new BetOddsDTO
                {
                    Odds = BTTSOdds(lambdaH, lambdaA, bttsPick.Value),
                    Description = bttsPick.Value ? "BTTS Yes" : "BTTS No"
                },

                BetType.OverUnder when ouLine != null && ouPick != null => new BetOddsDTO
                {
                    Odds = OUOdds(lambdaH, lambdaA, ouLine.Value, ouPick.Value),
                    Description = $"{ouPick} {OULineValue(ouLine.Value)}"
                },

                _ => null
            };
        }

        // ── Poisson helpers ──────────────────────────────────────

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
            int maxGoals = (int)threshold + 1; // sum up to threshold (inclusive for under)
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
                    if (h > a) home += p;
                    else if (h == a) draw += p;
                    else away += p;
                }
            return winner switch { MatchWinner.Home => home, MatchWinner.Draw => draw, _ => away };
        }

        private static double OULineValue(OverUnderLine line) => line switch
        {
            OverUnderLine.Line15 => 1.5,
            OverUnderLine.Line25 => 2.5,
            OverUnderLine.Line35 => 3.5,
            _ => 2.5
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
