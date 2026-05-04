using BPFL.API.Data;
using BPFL.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.BackgroundJobs
{
    public class PredictionScoringJob : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<PredictionScoringJob> logger;
        private readonly IConfiguration configuration;

        private TimeSpan SyncInterval =>
            TimeSpan.FromMinutes(configuration.GetValue<double>("BackgroundJobs:ScoringIntervalMinutes", 2));

        public PredictionScoringJob(IServiceScopeFactory _scopeFactory, ILogger<PredictionScoringJob> _logger,
            IConfiguration _configuration)
        {
            scopeFactory  = _scopeFactory;
            logger        = _logger;
            configuration = _configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("PredictionScoringJob started. Interval: {Interval}", SyncInterval);
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunCycleAsync(stoppingToken); }
                catch (Exception ex) { logger.LogError(ex, "Unexpected error in PredictionScoringJob."); }

                await Task.Delay(SyncInterval, stoppingToken);
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            logger.LogInformation("Prediction scoring cycle started at {Time}", DateTime.UtcNow);

            using var scope = scopeFactory.CreateScope();

            var scoring         = scope.ServiceProvider.GetRequiredService<PredictionScoringService>();
            var betService      = scope.ServiceProvider.GetRequiredService<BetService>();
            var fantasyAutoSync = scope.ServiceProvider.GetRequiredService<FantasyAutoSyncService>();
            var db              = scope.ServiceProvider.GetRequiredService<BPFL_DBContext>();

            // Auto-create gameweeks from matchdays & finalise any completed ones
            try { await fantasyAutoSync.SyncGameweeksFromMatchdaysAsync(ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Fantasy gameweek sync failed."); }

            try { await fantasyAutoSync.TryFinaliseGameweekScoresAsync(ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Fantasy score finalisation failed."); }

            var matchIdsToScore = await db.Matches
                .Where(m => m.Status == "FINISHED"
                         && m.HomeScore != null
                         && m.AwayScore != null
                         && (db.Predictions.Any(p => p.MatchId == m.Id)
                             || db.Bets.Any(b => b.MatchId == m.Id && b.Status == BetStatus.Pending)
                             || db.FantasyTeamSelections.Any(s => s.FantasyGameweek.StartDate <= m.MatchDate
                                                               && s.FantasyGameweek.EndDate   >= m.MatchDate)))
                .Select(m => new { m.Id, m.ExternalId, m.HomeScore, m.AwayScore })
                .ToListAsync(ct);

            logger.LogInformation("Found match IDs to score: {MatchIds}", string.Join(", ", matchIdsToScore.Select(m => m.Id)));

            if (matchIdsToScore.Count == 0)
            {
                logger.LogInformation("No finished matches to process.");
                return;
            }

            int totalScored = 0;

            foreach (var match in matchIdsToScore)
            {
                logger.LogInformation("Scoring match {MatchId}", match.Id);

                var result = await scoring.ScoreMatchPredictionsAsync(match.Id, ct);
                logger.LogInformation("Scored {Count} predictions for match {MatchId}", result.ScoredPredictionsCount, match.Id);
                totalScored += result.ScoredPredictionsCount;

                if (match.HomeScore != null && match.AwayScore != null)
                    await betService.ResolveMatchBetsAsync(match.Id, match.HomeScore.Value, match.AwayScore.Value, ct);

                try { await fantasyAutoSync.SyncMatchStatsAsync(match.Id, match.ExternalId, ct); }
                catch (Exception ex) { logger.LogWarning(ex, "Fantasy stats sync failed for match {Id}", match.Id); }
            }

            logger.LogInformation("Prediction scoring cycle completed. Total predictions scored: {Total}", totalScored);
        }
    }
}
