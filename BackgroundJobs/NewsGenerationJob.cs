using BPFL.API.Data;
using BPFL.API.Features.News;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.BackgroundJobs
{
    /// <summary>
    /// Runs every 6 hours.
    /// - Generates Match Previews for matches starting in 12–36 hours (if not already done).
    /// - Generates Match Reports for matches finished in the last 6 hours (if not already done).
    /// - Generates one League Summary per league per day (runs at first cycle after 07:00 UTC).
    /// </summary>
    public class NewsGenerationJob : BackgroundService
    {
        private readonly IServiceScopeFactory   _scopeFactory;
        private readonly ILogger<NewsGenerationJob> _logger;
        private readonly IConfiguration         _configuration;

        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        public NewsGenerationJob(
            IServiceScopeFactory scopeFactory,
            ILogger<NewsGenerationJob> logger,
            IConfiguration configuration)
        {
            _scopeFactory  = scopeFactory;
            _logger        = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NewsGenerationJob started.");

            // Wait a bit so the app finishes startup before making AI calls
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try   { await RunCycleAsync(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "Unexpected error in NewsGenerationJob."); }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            _logger.LogInformation("NewsGenerationJob cycle started at {Time}", DateTime.UtcNow);

            using var scope = _scopeFactory.CreateScope();
            var newsService = scope.ServiceProvider.GetRequiredService<NewsService>();
            var db          = scope.ServiceProvider.GetRequiredService<BPFL_DBContext>();

            var now = DateTime.UtcNow;

            // ── Match Previews ─────────────────────────────────────────
            // Find matches starting in 12–36 hours that don't have a preview yet
            var previewCandidates = await db.Matches.AsNoTracking()
                .Where(m => m.Status != "FINISHED" &&
                            m.MatchDate > now.AddHours(12) &&
                            m.MatchDate < now.AddHours(36) &&
                            !db.NewsArticles.Any(n => n.MatchId == m.Id &&
                                                      n.Type == Models.NewsType.MatchPreview))
                .Select(m => m.Id)
                .ToListAsync(ct);

            foreach (var matchId in previewCandidates)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    await newsService.GenerateMatchPreviewAsync(matchId, ct);
                    _logger.LogInformation("Generated preview for match {MatchId}", matchId);
                    // Small delay between AI calls to avoid rate limits
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate preview for match {MatchId}", matchId);
                }
            }

            // ── Match Reports ──────────────────────────────────────────
            // Find matches finished in the last 6 hours without a report
            var reportCandidates = await db.Matches.AsNoTracking()
                .Where(m => m.Status == "FINISHED" &&
                            m.MatchDate > now.AddHours(-6) &&
                            m.HomeScore != null &&
                            !db.NewsArticles.Any(n => n.MatchId == m.Id &&
                                                      n.Type == Models.NewsType.MatchReport))
                .Select(m => m.Id)
                .ToListAsync(ct);

            foreach (var matchId in reportCandidates)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    await newsService.GenerateMatchReportAsync(matchId, ct);
                    _logger.LogInformation("Generated report for match {MatchId}", matchId);
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate report for match {MatchId}", matchId);
                }
            }

            // ── League Summaries ───────────────────────────────────────
            // Only at the first cycle after 07:00 UTC each day
            if (now.Hour >= 7)
            {
                var leagueCodes = _configuration
                    .GetSection("BackgroundJobs:LeagueCodes")
                    .Get<List<string>>() ?? ["BGL"];

                foreach (var code in leagueCodes)
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        await newsService.GenerateLeagueSummaryAsync(code, ct);
                        await Task.Delay(TimeSpan.FromSeconds(3), ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate summary for league {Code}", code);
                    }
                }
            }

            _logger.LogInformation("NewsGenerationJob cycle completed at {Time}", DateTime.UtcNow);
        }
    }
}
