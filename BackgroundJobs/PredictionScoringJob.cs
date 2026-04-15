using BPFL.API.Data;
using BPFL.API.Services;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.BackgroundJobs
{
    public class PredictionScoringJob : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<PredictionScoringJob> logger;
        private readonly IConfiguration configuration;

        private TimeSpan SyncInterval =>
     TimeSpan.FromMinutes(configuration.GetValue<double>("BackgroundJobs:MatchSyncIntervalMinutes", 2));
        public PredictionScoringJob(IServiceScopeFactory _scopeFactory, ILogger<PredictionScoringJob> _logger,
            IConfiguration _configuration)
        {
            scopeFactory = _scopeFactory;
            logger = _logger;
            configuration = _configuration;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("MatchSyncJob started. Interval: {Interval}", SyncInterval);

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error in MatchSyncJob.");
                }

                await Task.Delay(SyncInterval, stoppingToken);
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            logger.LogInformation("Prediction scoring cycle started at {Time}", DateTime.UtcNow);

            using var scope = scopeFactory.CreateScope();

            var scoring = scope.ServiceProvider.GetRequiredService<PredictionScoringService>();
            var db = scope.ServiceProvider.GetRequiredService<BPFL_DBContext>();

            var matchIdsToScore = await db.Matches
                .Where(m => m.Status == "FINISHED"
                         && m.HomeScore != null
                         && m.AwayScore != null
                         && db.Predictions.Any(p => p.MatchId == m.Id))
                .Select(m => m.Id)
                .ToListAsync(ct);

            logger.LogInformation("Found match IDs to score: {MatchIds}", string.Join(", ", matchIdsToScore));

            if (matchIdsToScore.Count == 0)
            {
                logger.LogInformation("No finished matches with predictions found.");
                return;
            }

            int totalScored = 0;

            foreach (var matchId in matchIdsToScore)
            {
                logger.LogInformation("Scoring match {MatchId}", matchId);

                var result = await scoring.ScoreMatchPredictionsAsync(matchId, ct);

                logger.LogInformation(
                    "Finished scoring match {MatchId}. Scored predictions: {Count}",
                    matchId,
                    result.ScoredPredictionsCount);

                totalScored += result.ScoredPredictionsCount;
            }

            logger.LogInformation(
                "Prediction scoring cycle completed. Total predictions scored: {Total}",
                totalScored);
        }
    }
}
