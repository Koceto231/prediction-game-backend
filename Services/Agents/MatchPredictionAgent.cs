using BPFL.API.Models.DTO;
using BPFL.API.Models.MatchAnalysis;
using System.Text;
using System.Text.Json;

namespace BPFL.API.Services.Agents
{
    public class MatchPredictionAgent
    {
        private readonly OpenRouterClient _openRouterClient;
        private readonly ILogger<MatchPredictionAgent> _logger;

        private const string SystemPrompt = """
            You are a football match prediction expert. Analyze the given statistics and provide a structured prediction.
            Respond ONLY with valid JSON (no markdown, no code blocks) in this exact format:
            {
              "pick": "Home|Draw|Away",
              "confidence": <number 0-100>,
              "homeWinProbability": <number 0-100>,
              "drawProbability": <number 0-100>,
              "awayWinProbability": <number 0-100>,
              "predictedHomeScore": <integer>,
              "predictedAwayScore": <integer>,
              "analysis": "<2-3 sentence explanation of your prediction>"
            }
            The three probability values must sum to exactly 100.
            """;

        public MatchPredictionAgent(OpenRouterClient openRouterClient, ILogger<MatchPredictionAgent> logger)
        {
            _openRouterClient = openRouterClient;
            _logger = logger;
        }

        public async Task<AgentPredictionResult?> PredictAsync(
            MatchAnalysisDTO analysis,
            PredictionModelDTO model,
            CancellationToken ct = default)
        {
            var prompt = BuildPrompt(analysis, model);

            try
            {
                var raw = await _openRouterClient.CompleteAsync(SystemPrompt, prompt, ct);
                if (string.IsNullOrWhiteSpace(raw))
                    return null;

                var result = JsonSerializer.Deserialize<AgentPredictionResult>(raw, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null || string.IsNullOrWhiteSpace(result.Pick))
                    return null;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI agent failed for {Home} vs {Away}, falling back to statistical model",
                    analysis.HomeTeam, analysis.AwayTeam);
                return null;
            }
        }

        private static string BuildPrompt(MatchAnalysisDTO analysis, PredictionModelDTO model)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Match: {analysis.HomeTeam} vs {analysis.AwayTeam} on {analysis.MatchDate:yyyy-MM-dd}");
            sb.AppendLine();
            sb.AppendLine($"HOME TEAM — {analysis.HomeTeam}:");
            sb.AppendLine($"  Recent form (last 5 matches): {analysis.HomeRecentFromPoints}/15 pts");
            sb.AppendLine($"  Avg goals scored (overall):   {analysis.HomeAverageGoalsScored}");
            sb.AppendLine($"  Avg goals scored (at home):   {analysis.HomeAverageGoalsAtHome}");
            sb.AppendLine($"  Avg goals conceded:           {analysis.HomeAverageGoalsConceded}");
            sb.AppendLine($"  Matches analyzed:             {analysis.HomeMatchesAnalyzed}");
            sb.AppendLine();
            sb.AppendLine($"AWAY TEAM — {analysis.AwayTeam}:");
            sb.AppendLine($"  Recent form (last 5 matches): {analysis.AwayRecentFromPoints}/15 pts");
            sb.AppendLine($"  Avg goals scored (overall):   {analysis.AwayAverageGoalsScored}");
            sb.AppendLine($"  Avg goals scored (away):      {analysis.AwayAverageGoalsAtAway}");
            sb.AppendLine($"  Avg goals conceded:           {analysis.AwayAverageGoalsConceded}");
            sb.AppendLine($"  Matches analyzed:             {analysis.AwayMatchesAnalyzed}");
            sb.AppendLine();
            sb.AppendLine("STATISTICAL MODEL ESTIMATES:");
            sb.AppendLine($"  Expected home goals: {model.ExpectedHomeGoals}");
            sb.AppendLine($"  Expected away goals: {model.ExpectedAwayGoals}");
            sb.AppendLine($"  Model home win %:    {model.HomeWinProbavility * 100:F1}");
            sb.AppendLine($"  Model draw %:        {model.DrawProbability * 100:F1}");
            sb.AppendLine($"  Model away win %:    {model.AwayWinProbability * 100:F1}");

            return sb.ToString();
        }
    }

    public class AgentPredictionResult
    {
        public string Pick { get; set; } = null!;
        public double Confidence { get; set; }
        public double HomeWinProbability { get; set; }
        public double DrawProbability { get; set; }
        public double AwayWinProbability { get; set; }
        public double PredictedHomeScore { get; set; }
        public double PredictedAwayScore { get; set; }
        public string Analysis { get; set; } = null!;
    }
}
