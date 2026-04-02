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
            logger.LogInformation("Prediction sync cycle started at {Time}", DateTime.UtcNow);

            using var scope = scopeFactory.CreateScope();

            var scoring = scope.ServiceProvider.GetRequiredService<PredictionScoringService>();
            var db = scope.ServiceProvider.GetRequiredService<BPFL_DBContext>();

            var matchIdsToScore = await db.Predictions
                            .Where(p => p.Points == null
                                     && p.Match.Status == "FINISHED"
                                     && p.Match.HomeScore != null
                                     && p.Match.AwayScore != null)
                            .Select(p => p.MatchId)
                            .Distinct()
                            .ToListAsync(ct);

            if (matchIdsToScore.Count == 0)
            {
                logger.LogInformation("No unscored finished matches found.");
                return;
            }

            logger.LogInformation(
                "Found {Count} finished matches with unscored predictions.",
                matchIdsToScore.Count);

            int totalScored = 0;

            var semaphore = new SemaphoreSlim(5); 

            var tasks = matchIdsToScore.Select(async matchId =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var result = await scoring.ScoreMatchPredictionsAsync(matchId, ct);
                    return result.ScoredPredictionsCount;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            logger.LogInformation(
                "Prediction scoring cycle completed. Total predictions scored: {Total}",
                totalScored);
        }
    }
}
