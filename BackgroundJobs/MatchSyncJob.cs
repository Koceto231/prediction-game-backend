
using BPFL.API.Data;
using BPFL.API.Services;
using BPFL.API.Services.FantasyServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BPFL.API.BackgroundJobs
{
    public class MatchSyncJob : BackgroundService
    {

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<MatchSyncJob> logger;
        private readonly IConfiguration configuration;

        private TimeSpan SyncInterval =>
          TimeSpan.FromMinutes(configuration.GetValue<double>("BackgroundJobs:MatchSyncIntervalMinutes", 15));
        public MatchSyncJob(IServiceScopeFactory _scopeFactory, ILogger<MatchSyncJob> _logger,
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
            logger.LogInformation("Match sync cycle started at {Time}", DateTime.UtcNow);

            using var scope = scopeFactory.CreateScope();

            var matchSync    = scope.ServiceProvider.GetRequiredService<MatchSyncService>();
            var teamSync     = scope.ServiceProvider.GetRequiredService<TeamSyncService>();
            var oddsService  = scope.ServiceProvider.GetRequiredService<OddsService>();
            var fantasySync  = scope.ServiceProvider.GetRequiredService<FantasyAutoSyncService>();
            var db           = scope.ServiceProvider.GetRequiredService<BPFL_DBContext>();

            var leagueCodes = configuration
              .GetSection("BackgroundJobs:LeagueCodes")
              .Get<List<string>>() ?? new List<string> { "PL" };

            foreach (var code in leagueCodes)
            {
                if (ct.IsCancellationRequested)
                    return;
                try
                {
                    var teamResult = await teamSync.ImportTeamAsync(code, ct);
                    logger.LogInformation(
                        "Team sync [{Code}] -> Added: {Added}, Updated: {Updated}",
                        code, teamResult.Added, teamResult.Updated);

                    var matchResult = await matchSync.ImportMatchesAsync(code, ct);
                    logger.LogInformation(
                        "Match sync [{Code}] -> Added: {Added}, Updated: {Updated}",
                        code, matchResult.Added, matchResult.Updated);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Match sync failed for league {Code}", code);
                }
            }

            // Ensure all upcoming matches have Poisson odds + expected goals
            try
            {
                await oddsService.EnsureOddsForUpcomingMatchesAsync(ct);
                logger.LogInformation("Odds recalculation complete.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Odds recalculation failed.");
            }

            // Auto-sync fantasy players if none exist yet
            try
            {
                var hasPlayers = await db.FantasyPlayers.AnyAsync(ct);
                if (!hasPlayers)
                {
                    logger.LogInformation("No fantasy players found — syncing from squad data.");
                    await fantasySync.SyncPlayersFromSquadsAsync(leagueCodes.ToArray(), ct);
                    logger.LogInformation("Fantasy player sync complete.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Fantasy player sync failed.");
            }

            logger.LogInformation("Match sync cycle completed at {Time}", DateTime.UtcNow);
        }
    }
}
