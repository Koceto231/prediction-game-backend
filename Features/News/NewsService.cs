using BPFL.API.Data;
using BPFL.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Features.News
{
    public class NewsService
    {
        private readonly BPFL_DBContext     _db;
        private readonly NewsAgent          _agent;
        private readonly ILogger<NewsService> _logger;

        public NewsService(BPFL_DBContext db, NewsAgent agent, ILogger<NewsService> logger)
        {
            _db     = db;
            _agent  = agent;
            _logger = logger;
        }

        // ── Generate ─────────────────────────────────────────────────

        /// <summary>
        /// Generates a match preview article.
        /// Skips generation if one already exists for this match.
        /// </summary>
        public async Task<NewsArticleDTO?> GenerateMatchPreviewAsync(int matchId, CancellationToken ct = default)
        {
            // Guard: already generated?
            if (await _db.NewsArticles.AnyAsync(n => n.MatchId == matchId && n.Type == NewsType.MatchPreview, ct))
            {
                _logger.LogInformation("Preview already exists for match {MatchId}, skipping.", matchId);
                return null;
            }

            var match = await LoadMatchAsync(matchId, ct)
                ?? throw new KeyNotFoundException($"Match {matchId} not found.");

            var result = await _agent.GenerateMatchPreviewAsync(match, ct);
            if (result == null) return null;

            var imageUrl = await _agent.GenerateCoverImageAsync(
                NewsType.MatchPreview, match, null,
                $"preview-{matchId}-{DateTime.UtcNow:yyyyMMddHHmm}", ct);

            return await SaveAsync(new NewsArticle
            {
                Type     = NewsType.MatchPreview,
                MatchId  = matchId,
                Title    = result.Value.Title,
                Body     = result.Value.Body,
                ImageUrl = imageUrl,
            }, ct);
        }

        /// <summary>
        /// Generates a match report. Match must be finished.
        /// </summary>
        public async Task<NewsArticleDTO?> GenerateMatchReportAsync(int matchId, CancellationToken ct = default)
        {
            var match = await LoadMatchAsync(matchId, ct)
                ?? throw new KeyNotFoundException($"Match {matchId} not found.");

            if (match.Status != "FINISHED")
                throw new InvalidOperationException("Cannot generate a report — match is not finished yet.");

            // Guard: already generated?
            if (await _db.NewsArticles.AnyAsync(n => n.MatchId == matchId && n.Type == NewsType.MatchReport, ct))
            {
                _logger.LogInformation("Report already exists for match {MatchId}, skipping.", matchId);
                return null;
            }

            var result = await _agent.GenerateMatchReportAsync(match, ct);
            if (result == null) return null;

            var imageUrl = await _agent.GenerateCoverImageAsync(
                NewsType.MatchReport, match, null,
                $"report-{matchId}-{DateTime.UtcNow:yyyyMMddHHmm}", ct);

            return await SaveAsync(new NewsArticle
            {
                Type     = NewsType.MatchReport,
                MatchId  = matchId,
                Title    = result.Value.Title,
                Body     = result.Value.Body,
                ImageUrl = imageUrl,
            }, ct);
        }

        /// <summary>
        /// Generates a weekly league summary.
        /// Only one per league per day.
        /// </summary>
        public async Task<NewsArticleDTO?> GenerateLeagueSummaryAsync(string leagueCode, CancellationToken ct = default)
        {
            var today = DateTime.UtcNow.Date;
            if (await _db.NewsArticles.AnyAsync(
                    n => n.Type == NewsType.LeagueSummary &&
                         n.LeagueCode == leagueCode &&
                         n.GeneratedAt >= today, ct))
            {
                _logger.LogInformation("League summary already generated today for {Code}, skipping.", leagueCode);
                return null;
            }

            var result = await _agent.GenerateLeagueSummaryAsync(leagueCode, ct);
            if (result == null) return null;

            var imageUrl = await _agent.GenerateCoverImageAsync(
                NewsType.LeagueSummary, null, leagueCode,
                $"summary-{leagueCode}-{DateTime.UtcNow:yyyyMMdd}", ct);

            return await SaveAsync(new NewsArticle
            {
                Type       = NewsType.LeagueSummary,
                LeagueCode = leagueCode,
                Title      = result.Value.Title,
                Body       = result.Value.Body,
                ImageUrl   = imageUrl,
            }, ct);
        }

        // ── Backfill images ───────────────────────────────────────────

        public record BackfillResult(int Updated, int Total, List<BackfillItem> Items);
        public record BackfillItem(int Id, string Title, bool Ok, string Detail);

        /// <summary>
        /// Generates cover images for all articles that currently have ImageUrl == null.
        /// Returns a detailed result so the caller can diagnose failures.
        /// </summary>
        public async Task<BackfillResult> BackfillImagesAsync(CancellationToken ct = default)
        {
            var articles = await _db.NewsArticles
                .Include(n => n.Match).ThenInclude(m => m!.HomeTeam)
                .Include(n => n.Match).ThenInclude(m => m!.AwayTeam)
                .Where(n => n.ImageUrl == null)
                .ToListAsync(ct);

            var items   = new List<BackfillItem>();
            int updated = 0;

            foreach (var article in articles)
            {
                try
                {
                    var publicId = article.Type switch
                    {
                        NewsType.MatchPreview  => $"preview-{article.MatchId}-{article.GeneratedAt:yyyyMMddHHmm}",
                        NewsType.MatchReport   => $"report-{article.MatchId}-{article.GeneratedAt:yyyyMMddHHmm}",
                        NewsType.LeagueSummary => $"summary-{article.LeagueCode}-{article.GeneratedAt:yyyyMMdd}",
                        _                      => $"news-{article.Id}"
                    };

                    var url = await _agent.GenerateCoverImageAsync(
                        article.Type, article.Match, article.LeagueCode, publicId, ct);

                    if (url != null)
                    {
                        article.ImageUrl = url;
                        updated++;
                        items.Add(new BackfillItem(article.Id, article.Title, true, url));
                    }
                    else
                    {
                        items.Add(new BackfillItem(article.Id, article.Title, false,
                            "GenerateCoverImageAsync returned null — check Stability AI / Cloudinary keys in Render logs."));
                    }

                    await Task.Delay(3_000, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Backfill image failed for article {Id}", article.Id);
                    items.Add(new BackfillItem(article.Id, article.Title, false, ex.Message));
                }
            }

            if (updated > 0)
                await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Image backfill complete: {Count}/{Total} articles updated.", updated, articles.Count);
            return new BackfillResult(updated, articles.Count, items);
        }

        // ── Read ──────────────────────────────────────────────────────

        public async Task<List<NewsArticleDTO>> GetLatestAsync(
            NewsType? type = null, int take = 20, CancellationToken ct = default)
        {
            var query = _db.NewsArticles
                .AsNoTracking()
                .Include(n => n.Match).ThenInclude(m => m!.HomeTeam)
                .Include(n => n.Match).ThenInclude(m => m!.AwayTeam)
                .AsQueryable();

            if (type.HasValue)
                query = query.Where(n => n.Type == type.Value);

            var articles = await query
                .OrderByDescending(n => n.GeneratedAt)
                .Take(take)
                .ToListAsync(ct);

            return articles.Select(ToDTO).ToList();
        }

        public async Task<NewsArticleDTO?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var article = await _db.NewsArticles
                .AsNoTracking()
                .Include(n => n.Match).ThenInclude(m => m!.HomeTeam)
                .Include(n => n.Match).ThenInclude(m => m!.AwayTeam)
                .FirstOrDefaultAsync(n => n.Id == id, ct);

            return article == null ? null : ToDTO(article);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private async Task<Match?> LoadMatchAsync(int matchId, CancellationToken ct) =>
            await _db.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);

        private async Task<NewsArticleDTO> SaveAsync(NewsArticle article, CancellationToken ct)
        {
            _db.NewsArticles.Add(article);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("News article saved: [{Type}] {Title}", article.Type, article.Title);

            // Re-load with navigation properties for the DTO
            return ToDTO(await _db.NewsArticles
                .Include(n => n.Match).ThenInclude(m => m!.HomeTeam)
                .Include(n => n.Match).ThenInclude(m => m!.AwayTeam)
                .FirstAsync(n => n.Id == article.Id, ct));
        }

        private static NewsArticleDTO ToDTO(NewsArticle a) => new()
        {
            Id          = a.Id,
            Type        = a.Type,
            TypeLabel   = a.Type switch
            {
                NewsType.MatchPreview   => "Match Preview",
                NewsType.MatchReport    => "Match Report",
                NewsType.LeagueSummary  => "League Summary",
                _                       => a.Type.ToString()
            },
            Title       = a.Title,
            Body        = a.Body,
            MatchId     = a.MatchId,
            HomeTeam    = a.Match?.HomeTeam?.Name,
            AwayTeam    = a.Match?.AwayTeam?.Name,
            LeagueCode  = a.LeagueCode,
            ImageUrl    = a.ImageUrl,
            GeneratedAt = a.GeneratedAt,
        };
    }
}
