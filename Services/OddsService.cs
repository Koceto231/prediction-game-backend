using BPFL.API.Data;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services
{
    public class OddsService
    {
        private readonly BPFL_DBContext _db;
        private readonly MatchAnalysisService _analysisService;
        private readonly PredictionModelService _modelService;
        private readonly ILogger<OddsService> _logger;

        // 10% house edge applied to raw probabilities
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
            var matchesWithoutOdds = await _db.Matches
                .Where(m => m.Status != "FINISHED"
                         && m.MatchDate >= DateTime.UtcNow
                         && m.HomeOdds == null)
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .ToListAsync(ct);

            if (matchesWithoutOdds.Count == 0) return;

            foreach (var match in matchesWithoutOdds)
            {
                try
                {
                    var analysis = await _analysisService.AnalyzeMatch(match, ct);
                    var model = _modelService.BuildModel(analysis);

                    match.HomeOdds = ToOdds(model.HomeWinProbavility);
                    match.DrawOdds = ToOdds(model.DrawProbability);
                    match.AwayOdds = ToOdds(model.AwayWinProbability);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to calculate odds for match {MatchId}", match.Id);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Odds calculated for {Count} matches", matchesWithoutOdds.Count);
        }

        private static decimal ToOdds(double probability)
        {
            if (probability <= 0) return MinOdds;
            var odds = (decimal)(HouseEdge / probability);
            return Math.Max(Math.Round(odds, 2), MinOdds);
        }
    }
}
