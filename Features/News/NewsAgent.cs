using BPFL.API.Data;
using BPFL.API.Features.Predictions;
using BPFL.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BPFL.API.Features.News
{
    /// <summary>
    /// Builds a rich, data-backed prompt for each news type and calls OpenRouter.
    /// Returns a (Title, Body) tuple — no DB writes here, that is NewsService's job.
    /// </summary>
    public class NewsAgent
    {
        private readonly OpenRouterClient            _openRouter;
        private readonly BPFL_DBContext              _db;
        private readonly StabilityAIClient           _stabilityAI;
        private readonly CloudinaryUploader          _cloudinary;
        private readonly ILogger<NewsAgent>          _logger;

        private const string SystemPrompt = """
            You are a professional football journalist. Write concise, engaging football news articles.
            Always respond in this exact format (no markdown, no extra text):
            TITLE: <article title here>
            BODY: <article body here>

            Rules:
            - Title: max 12 words, punchy and informative
            - Body: 120-180 words, 2 short paragraphs
            - Write in Bulgarian language
            - No markdown formatting
            - Be factual, use the provided data
            """;

        public NewsAgent(
            OpenRouterClient   openRouter,
            BPFL_DBContext     db,
            StabilityAIClient  stabilityAI,
            CloudinaryUploader cloudinary,
            ILogger<NewsAgent> logger)
        {
            _openRouter  = openRouter;
            _db          = db;
            _stabilityAI = stabilityAI;
            _cloudinary  = cloudinary;
            _logger      = logger;
        }

        // ── Match Preview ────────────────────────────────────────────

        public async Task<(string Title, string Body)?> GenerateMatchPreviewAsync(
            Match match, CancellationToken ct = default)
        {
            var prompt = await BuildMatchPreviewPromptAsync(match, ct);
            return await CallAgentAsync(prompt, $"{match.HomeTeam?.Name} vs {match.AwayTeam?.Name} preview", ct);
        }

        // ── Match Report ─────────────────────────────────────────────

        public async Task<(string Title, string Body)?> GenerateMatchReportAsync(
            Match match, CancellationToken ct = default)
        {
            var prompt = BuildMatchReportPrompt(match);
            return await CallAgentAsync(prompt, $"{match.HomeTeam?.Name} vs {match.AwayTeam?.Name} report", ct);
        }

        // ── League Summary ───────────────────────────────────────────

        public async Task<(string Title, string Body)?> GenerateLeagueSummaryAsync(
            string leagueCode, CancellationToken ct = default)
        {
            var prompt = await BuildLeagueSummaryPromptAsync(leagueCode, ct);
            return await CallAgentAsync(prompt, $"League summary {leagueCode}", ct);
        }

        // ── Image generation ─────────────────────────────────────────

        /// <summary>
        /// Generates a cover image via Stability AI and uploads to Cloudinary.
        /// Returns a permanent URL, or null if Cloudinary upload fails.
        /// Throws if Stability AI returns an error (so callers can surface the message).
        /// </summary>
        public async Task<string?> GenerateCoverImageAsync(
            NewsType type, Match? match, string? leagueCode,
            string publicId, CancellationToken ct = default)
        {
            var imagePrompt = BuildImagePrompt(type, match, leagueCode);

            // Both throw on failure — exceptions bubble up to BackfillImagesAsync
            var bytes = await _stabilityAI.GenerateImageAsync(imagePrompt, "16:9", ct);
            if (bytes == null || bytes.Length == 0)
                throw new Exception("Image generator returned empty bytes.");

            return await _cloudinary.UploadAsync(bytes, publicId, ct);
        }

        private static string BuildImagePrompt(NewsType type, Match? match, string? leagueCode)
        {
            var home = match?.HomeTeam?.Name ?? "";
            var away = match?.AwayTeam?.Name ?? "";

            // Shared negative guidance to steer away from American football
            const string style = "European soccer, round ball, soccer players in jerseys and shorts, " +
                                  "green grass pitch, photorealistic, cinematic 16:9, " +
                                  "NOT american football, NOT rugby, NOT helmet, NOT oval ball";

            return type switch
            {
                NewsType.MatchPreview =>
                    $"European soccer match preview poster, {home} vs {away}, " +
                    "two teams facing each other on a floodlit green grass pitch, packed stadium crowd, " +
                    $"tense dramatic atmosphere before kickoff, dark moody cinematic lighting, {style}",

                NewsType.MatchReport when match?.HomeScore > match?.AwayScore =>
                    $"European soccer victory celebration, {home} players hugging and cheering on green pitch, " +
                    "fans going wild in packed stadium stands, confetti, golden hour lighting, " +
                    $"wide angle shot, {style}",

                NewsType.MatchReport when match?.AwayScore > match?.HomeScore =>
                    $"European soccer away victory, {away} players celebrating on the pitch, " +
                    "away fans section erupting in joy, stadium atmosphere at full time, " +
                    $"dramatic floodlight photography, {style}",

                NewsType.MatchReport =>
                    "European soccer match ends in a draw, players from both teams shaking hands " +
                    "and exchanging jerseys on green grass pitch, respectful atmosphere at full time, " +
                    $"stadium lights, wide angle, {style}",

                NewsType.LeagueSummary when leagueCode == "PL" =>
                    "English Premier League weekly highlights, iconic green soccer pitch, " +
                    "packed English stadium, dramatic floodlit evening match, soccer ball mid-flight, " +
                    $"vibrant colors, {style}",

                NewsType.LeagueSummary when leagueCode == "BL1" =>
                    "German Bundesliga weekly highlights, modern soccer stadium, green pitch, " +
                    "passionate crowd with yellow and black scarves, dramatic evening light, " +
                    $"soccer ball action, {style}",

                NewsType.LeagueSummary when leagueCode == "SA" =>
                    "Italian Serie A weekly highlights, classic European soccer stadium, green pitch, " +
                    "passionate Italian fans with colorful tifos, dramatic lighting, " +
                    $"soccer action photography, {style}",

                NewsType.LeagueSummary when leagueCode == "PD" =>
                    "Spanish La Liga weekly highlights, sunny Mediterranean soccer stadium, green pitch, " +
                    "passionate crowd, dramatic action shot, soccer ball in motion, " +
                    $"vibrant colors, {style}",

                NewsType.LeagueSummary when leagueCode == "BGL" =>
                    "Bulgarian soccer league weekly highlights, Eastern European stadium, green pitch, " +
                    "passionate local fans, dramatic evening floodlights, soccer ball action, " +
                    $"{style}",

                NewsType.LeagueSummary =>
                    $"European soccer league {leagueCode ?? "championship"} weekly highlights, " +
                    "green grass pitch, packed stadium, soccer ball in motion, floodlit evening, " +
                    $"dramatic sports photography, {style}",

                _ =>
                    $"European soccer match, green grass pitch, stadium crowd, dramatic lighting, {style}"
            };
        }

        // ── Prompt builders ──────────────────────────────────────────

        private async Task<string> BuildMatchPreviewPromptAsync(Match match, CancellationToken ct)
        {
            // Last 5 results for each team
            var cutoff = DateTime.UtcNow;
            var homeLast5 = await _db.Matches.AsNoTracking()
                .Where(m => m.Status == "FINISHED" && m.MatchDate < cutoff &&
                            (m.HomeTeamId == match.HomeTeamId || m.AwayTeamId == match.HomeTeamId))
                .OrderByDescending(m => m.MatchDate)
                .Take(5)
                .Select(m => new { m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore })
                .ToListAsync(ct);

            var awayLast5 = await _db.Matches.AsNoTracking()
                .Where(m => m.Status == "FINISHED" && m.MatchDate < cutoff &&
                            (m.HomeTeamId == match.AwayTeamId || m.AwayTeamId == match.AwayTeamId))
                .OrderByDescending(m => m.MatchDate)
                .Take(5)
                .Select(m => new { m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore })
                .ToListAsync(ct);

            static string FormLine(int teamId, IEnumerable<dynamic> results)
            {
                var symbols = results.Select(r =>
                {
                    if (r.HomeScore == null) return "?";
                    bool isHome = r.HomeTeamId == teamId;
                    int gs = isHome ? (r.HomeScore ?? 0) : (r.AwayScore ?? 0);
                    int gc = isHome ? (r.AwayScore ?? 0) : (r.HomeScore ?? 0);
                    return gs > gc ? "W" : gs < gc ? "L" : "D";
                });
                return string.Join(" ", symbols);
            }

            var homeForm = FormLine(match.HomeTeamId, homeLast5.Cast<dynamic>());
            var awayForm = FormLine(match.AwayTeamId, awayLast5.Cast<dynamic>());

            var sb = new StringBuilder();
            sb.AppendLine($"Match Preview: {match.HomeTeam?.Name} vs {match.AwayTeam?.Name}");
            sb.AppendLine($"Date: {match.MatchDate:dd MMM yyyy, HH:mm} UTC");
            sb.AppendLine();
            sb.AppendLine($"HOME — {match.HomeTeam?.Name}");
            sb.AppendLine($"  Last 5 results: {(homeForm.Length > 0 ? homeForm : "N/A")}");
            if (match.HomeOdds.HasValue) sb.AppendLine($"  Win odds: {match.HomeOdds:F2}");
            sb.AppendLine();
            sb.AppendLine($"AWAY — {match.AwayTeam?.Name}");
            sb.AppendLine($"  Last 5 results: {(awayForm.Length > 0 ? awayForm : "N/A")}");
            if (match.AwayOdds.HasValue) sb.AppendLine($"  Win odds: {match.AwayOdds:F2}");
            sb.AppendLine();
            if (match.DrawOdds.HasValue) sb.AppendLine($"Draw odds: {match.DrawOdds:F2}");
            if (match.ExpectedHomeGoals.HasValue)
                sb.AppendLine($"Expected goals: {match.HomeTeam?.Name} {match.ExpectedHomeGoals:F2} — {match.AwayTeam?.Name} {match.ExpectedAwayGoals:F2}");
            sb.AppendLine();
            sb.AppendLine("Write a match preview article in Bulgarian.");

            return sb.ToString();
        }

        private static string BuildMatchReportPrompt(Match match)
        {
            var homeScore = match.HomeScore ?? 0;
            var awayScore = match.AwayScore ?? 0;

            string resultLabel = homeScore > awayScore
                ? $"{match.HomeTeam?.Name} won"
                : awayScore > homeScore
                    ? $"{match.AwayTeam?.Name} won"
                    : "The match ended in a draw";

            var sb = new StringBuilder();
            sb.AppendLine($"Match Report: {match.HomeTeam?.Name} vs {match.AwayTeam?.Name}");
            sb.AppendLine($"Date: {match.MatchDate:dd MMM yyyy}");
            sb.AppendLine($"Final score: {match.HomeTeam?.Name} {homeScore} — {awayScore} {match.AwayTeam?.Name}");
            sb.AppendLine($"Result: {resultLabel}");
            if (match.TotalCorners.HasValue)    sb.AppendLine($"Total corners: {match.TotalCorners}");
            if (match.TotalYellowCards.HasValue) sb.AppendLine($"Yellow cards: {match.TotalYellowCards}");
            sb.AppendLine();
            sb.AppendLine("Write a match report article in Bulgarian.");

            return sb.ToString();
        }

        private async Task<string> BuildLeagueSummaryPromptAsync(string leagueCode, CancellationToken ct)
        {
            var recent = await _db.Matches.AsNoTracking()
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Where(m => m.Status == "FINISHED" &&
                            m.MatchDate >= DateTime.UtcNow.AddDays(-7) &&
                            (m.HomeTeam!.LeagueCode == leagueCode || m.AwayTeam!.LeagueCode == leagueCode))
                .OrderByDescending(m => m.MatchDate)
                .Take(8)
                .ToListAsync(ct);

            var upcoming = await _db.Matches.AsNoTracking()
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Where(m => m.Status != "FINISHED" &&
                            m.MatchDate > DateTime.UtcNow &&
                            m.MatchDate < DateTime.UtcNow.AddDays(7) &&
                            (m.HomeTeam!.LeagueCode == leagueCode || m.AwayTeam!.LeagueCode == leagueCode))
                .OrderBy(m => m.MatchDate)
                .Take(5)
                .ToListAsync(ct);

            var sb = new StringBuilder();
            sb.AppendLine($"Weekly League Summary — {leagueCode}");
            sb.AppendLine();
            sb.AppendLine("RECENT RESULTS (last 7 days):");
            foreach (var m in recent)
                sb.AppendLine($"  {m.HomeTeam?.Name} {m.HomeScore}–{m.AwayScore} {m.AwayTeam?.Name}");

            if (upcoming.Any())
            {
                sb.AppendLine();
                sb.AppendLine("UPCOMING MATCHES (next 7 days):");
                foreach (var m in upcoming)
                    sb.AppendLine($"  {m.HomeTeam?.Name} vs {m.AwayTeam?.Name} — {m.MatchDate:dd MMM}");
            }

            sb.AppendLine();
            sb.AppendLine("Write a weekly league summary article in Bulgarian.");

            return sb.ToString();
        }

        // ── Core call + parse ────────────────────────────────────────

        private async Task<(string Title, string Body)?> CallAgentAsync(
            string prompt, string context, CancellationToken ct)
        {
            try
            {
                var raw = await _openRouter.CompleteAsync(SystemPrompt, prompt, ct);
                if (string.IsNullOrWhiteSpace(raw)) return null;

                return ParseResponse(raw);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NewsAgent failed for: {Context}", context);
                return null;
            }
        }

        private static (string Title, string Body)? ParseResponse(string raw)
        {
            // Expected format:
            // TITLE: Some title here
            // BODY: Article text...
            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? title = null;
            var bodyLines = new List<string>();
            bool inBody = false;

            foreach (var line in lines)
            {
                if (!inBody && line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
                {
                    title = line["TITLE:".Length..].Trim();
                }
                else if (line.StartsWith("BODY:", StringComparison.OrdinalIgnoreCase))
                {
                    inBody = true;
                    var rest = line["BODY:".Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(rest)) bodyLines.Add(rest);
                }
                else if (inBody)
                {
                    bodyLines.Add(line.Trim());
                }
            }

            if (string.IsNullOrWhiteSpace(title) || bodyLines.Count == 0)
                return null;

            return (title, string.Join("\n", bodyLines).Trim());
        }
    }
}
