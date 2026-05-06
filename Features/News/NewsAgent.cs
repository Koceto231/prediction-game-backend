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

        private const string ImagePromptSystem = """
            You are an expert at writing image generation prompts for AI art models (Stable Diffusion).
            Given a football news article, write ONE concise image generation prompt in English (max 80 words).

            Rules:
            - Always include: association football, round white ball, green grass pitch, players in jerseys and shorts, photorealistic, cinematic 16:9
            - Describe a specific vivid scene that matches the article theme
            - Include team names and match context when relevant
            - No markdown, no explanations — output the prompt text only
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
        /// Uses the article title+body to ask OpenRouter for a context-aware image prompt.
        /// Falls back to a rule-based prompt if OpenRouter fails.
        /// </summary>
        public async Task<string?> GenerateCoverImageAsync(
            NewsType type, Match? match, string? leagueCode,
            string publicId,
            string? articleTitle = null, string? articleBody = null,
            CancellationToken ct = default)
        {
            var imagePrompt = await BuildContextualImagePromptAsync(
                type, match, leagueCode, articleTitle, articleBody, ct);

            // Both throw on failure — exceptions bubble up to BackfillImagesAsync
            var bytes = await _stabilityAI.GenerateImageAsync(imagePrompt, "16:9", ct);
            if (bytes == null || bytes.Length == 0)
                throw new Exception("Image generator returned empty bytes.");

            return await _cloudinary.UploadAsync(bytes, publicId, ct);
        }

        private async Task<string> BuildContextualImagePromptAsync(
            NewsType type, Match? match, string? leagueCode,
            string? title, string? body, CancellationToken ct)
        {
            // If we have article text, ask OpenRouter to craft a specific prompt
            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var snippet  = body.Length > 300 ? body[..300] : body;
                    var userMsg  = $"Article title: {title}\n\nArticle excerpt: {snippet}";
                    var aiPrompt = await _openRouter.CompleteAsync(ImagePromptSystem, userMsg, ct);

                    if (!string.IsNullOrWhiteSpace(aiPrompt))
                    {
                        _logger.LogInformation("AI image prompt: {Prompt}", aiPrompt[..Math.Min(100, aiPrompt.Length)]);
                        return aiPrompt.Trim();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OpenRouter image prompt generation failed, using fallback.");
                }
            }

            // Fallback: rule-based prompt from match/league data
            return BuildImagePrompt(type, match, leagueCode);
        }

        private static string BuildImagePrompt(NewsType type, Match? match, string? leagueCode)
        {
            var home = match?.HomeTeam?.Name ?? "";
            var away = match?.AwayTeam?.Name ?? "";

            // Crystal-clear visual anchors so the AI renders association football, not gridiron
            const string style = "association football, round white ball, players in shorts and football jerseys, " +
                                  "green grass pitch with white lines and goalposts, no helmets no padding, " +
                                  "photorealistic, cinematic 16:9";

            return type switch
            {
                NewsType.MatchPreview =>
                    $"Association football match preview, {home} versus {away}, " +
                    "eleven players in coloured jerseys facing each other on a green grass pitch, " +
                    "packed stadium under floodlights, round white ball on the centre spot, " +
                    $"tense atmosphere before kickoff, dark moody cinematic lighting, {style}",

                NewsType.MatchReport when match?.HomeScore > match?.AwayScore =>
                    $"Association football victory celebration, {home} players in jerseys hugging and " +
                    "raising fists on a green grass pitch, fans in the stands erupting with joy, " +
                    $"confetti falling, dramatic floodlight photography, wide angle shot, {style}",

                NewsType.MatchReport when match?.AwayScore > match?.HomeScore =>
                    $"Association football away victory, {away} players in jerseys celebrating on the pitch, " +
                    "round ball on the grass, away fans section cheering, stadium at full time, " +
                    $"dramatic lighting, {style}",

                NewsType.MatchReport =>
                    "Association football match ends in a draw, players in jerseys and shorts shaking hands " +
                    "on a green grass pitch with white markings, goalposts visible, stadium lights, " +
                    $"respectful atmosphere at full time, wide angle, {style}",

                NewsType.LeagueSummary when leagueCode == "PL" =>
                    "English Premier League football highlights, iconic green pitch with white lines, " +
                    "packed English stadium under floodlights, round white ball in motion, " +
                    $"players in coloured jerseys and shorts, dramatic evening atmosphere, {style}",

                NewsType.LeagueSummary when leagueCode == "BL1" =>
                    "German Bundesliga football highlights, modern stadium, green pitch, " +
                    "passionate crowd with scarves, round ball action near the goal, " +
                    $"players in jerseys and shorts, dramatic evening light, {style}",

                NewsType.LeagueSummary when leagueCode == "SA" =>
                    "Italian Serie A football highlights, classic European stadium, green pitch, " +
                    "passionate fans with colourful tifos in the stands, round ball action, " +
                    $"players in jerseys and shorts, dramatic floodlit photography, {style}",

                NewsType.LeagueSummary when leagueCode == "PD" =>
                    "Spanish La Liga football highlights, Mediterranean stadium, sunny green pitch, " +
                    "passionate crowd, round ball mid-flight near goal, " +
                    $"players in jerseys and shorts, vibrant colours, {style}",

                NewsType.LeagueSummary when leagueCode == "BGL" =>
                    "Bulgarian football league highlights, Eastern European stadium, green grass pitch, " +
                    "passionate local fans, round white ball in action under floodlights, " +
                    $"players in jerseys and shorts, {style}",

                NewsType.LeagueSummary =>
                    $"European football league {leagueCode ?? "championship"} weekly highlights, " +
                    "green grass pitch with white lines and goalposts, packed stadium, " +
                    $"round white ball in motion, players in jerseys and shorts, floodlit evening, {style}",

                _ =>
                    $"Association football match, green grass pitch, round white ball, " +
                    $"players in jerseys and shorts, stadium crowd, dramatic floodlights, {style}"
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
