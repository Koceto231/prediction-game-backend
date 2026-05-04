using BPFL.API.Data;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.BackgroundJobs
{
    public class MatchSyncJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MatchSyncJob> _logger;
        private readonly IConfiguration _configuration;

        private TimeSpan SyncInterval =>
            TimeSpan.FromMinutes(_configuration.GetValue<double>("BackgroundJobs:MatchSyncIntervalMinutes", 15));

        public MatchSyncJob(IServiceScopeFactory scopeFactory, ILogger<MatchSyncJob> logger, IConfiguration configuration)
        {
            _scopeFactory  = scopeFactory;
            _logger        = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MatchSyncJob started. Interval: {Interval}", SyncInterval);
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunCycleAsync(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "Unexpected error in MatchSyncJob."); }

                await Task.Delay(SyncInterval, stoppingToken);
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            _logger.LogInformation("Match sync cycle started at {Time}", DateTime.UtcNow);

            using var scope = _scopeFactory.CreateScope();

            var sportmonksSync = scope.ServiceProvider.GetRequiredService<SportmonksMatchSyncService>();
            var oddsService    = scope.ServiceProvider.GetRequiredService<OddsService>();
            var fantasySync    = scope.ServiceProvider.GetRequiredService<FantasyAutoSyncService>();
            var db             = scope.ServiceProvider.GetRequiredService<BPFL_DBContext>();

            var leagueCodes = _configuration
                .GetSection("BackgroundJobs:LeagueCodes")
                .Get<List<string>>() ?? ["BGL"];

            // ── Sync matches via Sportmonks ───────────────────────────
            foreach (var code in leagueCodes)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    var (added, updated) = await sportmonksSync.SyncLeagueMatchesAsync(code, daysAhead: 30, ct);
                    _logger.LogInformation("Sportmonks match sync [{Code}] -> Added: {Added}, Updated: {Updated}", code, added, updated);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sportmonks match sync failed for league {Code}", code);
                }
            }

            // ── Recalculate odds for upcoming matches ─────────────────
            try
            {
                await oddsService.EnsureOddsForUpcomingMatchesAsync(ct);
                _logger.LogInformation("Odds recalculation complete.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Odds recalculation failed.");
            }

            // ── Auto-seed players on first run ────────────────────────
            try
            {
                var hasPlayers = await db.FantasyPlayers.AnyAsync(ct);
                if (!hasPlayers)
                {
                    _logger.LogInformation("No fantasy players found — syncing from Sportmonks squads.");
                    await fantasySync.SyncPlayersFromSportmonksAsync(ct);
                    _logger.LogInformation("Fantasy player sync complete.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fantasy player auto-sync failed.");
            }

            _logger.LogInformation("Match sync cycle completed at {Time}", DateTime.UtcNow);
        }
    }
}
