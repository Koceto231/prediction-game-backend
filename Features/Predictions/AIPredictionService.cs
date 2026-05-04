using BPFL.API.Features.Predictions;


namespace BPFL.API.Features.Predictions
{
    public class AIPredictionService
    {
        private readonly ILogger<AIPredictionService> _logger;
        private readonly MatchPredictionAgent _agent;

        public AIPredictionService(ILogger<AIPredictionService> logger, MatchPredictionAgent agent)
        {
            _logger = logger;
            _agent = agent;
        }

        public async Task<AIPredictionResponseDTO> AIBuildPredictionAsync(
            MatchAnalysisDTO matchAnalysisDTO,
            PredictionModelDTO predictionModelDTO,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(matchAnalysisDTO);
            ArgumentNullException.ThrowIfNull(predictionModelDTO);

            _logger.LogInformation(
                "Building AI prediction for match {HomeTeam} vs {AwayTeam}",
                matchAnalysisDTO.HomeTeam,
                matchAnalysisDTO.AwayTeam);

            var agentResult = await _agent.PredictAsync(matchAnalysisDTO, predictionModelDTO, ct);

            if (agentResult != null)
            {
                _logger.LogInformation(
                    "AI agent prediction for {Home} vs {Away}: {Pick} ({Confidence}%)",
                    matchAnalysisDTO.HomeTeam, matchAnalysisDTO.AwayTeam,
                    agentResult.Pick, agentResult.Confidence);

                return new AIPredictionResponseDTO
                {
                    PredictedHomeScore = agentResult.PredictedHomeScore,
                    PredictedAwayScore = agentResult.PredictedAwayScore,
                    Pick = agentResult.Pick,
                    Confidence = agentResult.Confidence,
                    HomeWinProbability = agentResult.HomeWinProbability,
                    DrawProbability = agentResult.DrawProbability,
                    AwayWinProbability = agentResult.AwayWinProbability,
                    AIAnalysis = agentResult.Analysis
                };
            }

            _logger.LogInformation(
                "Falling back to statistical model for {Home} vs {Away}",
                matchAnalysisDTO.HomeTeam, matchAnalysisDTO.AwayTeam);

            return BuildFallbackPrediction(predictionModelDTO);
        }

        private static AIPredictionResponseDTO BuildFallbackPrediction(PredictionModelDTO predictionModelDTO)
        {
            double homeWinProb = predictionModelDTO.HomeWinProbavility;
            double drawProb = predictionModelDTO.DrawProbability;
            double awayWinProb = predictionModelDTO.AwayWinProbability;

            ValidateProbabilities(homeWinProb, drawProb, awayWinProb);
            var (pick, confidence) = DeterminePick(homeWinProb, drawProb, awayWinProb);

            return new AIPredictionResponseDTO
            {
                PredictedHomeScore = Math.Round(predictionModelDTO.ExpectedHomeGoals),
                PredictedAwayScore = Math.Round(predictionModelDTO.ExpectedAwayGoals),
                Pick = pick,
                Confidence = confidence,
                HomeWinProbability = homeWinProb * 100,
                DrawProbability = drawProb * 100,
                AwayWinProbability = awayWinProb * 100
            };
        }

        private static (string Pick, double Confidence) DeterminePick(
            double homeWinProb,
            double drawProb,
            double awayWinProb)
        {
            var maxProb = Math.Max(homeWinProb, Math.Max(drawProb, awayWinProb));

            if (maxProb == homeWinProb)
                return ("Home", Math.Round(homeWinProb * 100));
            if (maxProb == awayWinProb)
                return ("Away", Math.Round(awayWinProb * 100));
            return ("Draw", Math.Round(drawProb * 100));
        }

        private static void ValidateProbabilities(double homeWinProb, double drawProb, double awayWinProb)
        {
            if (homeWinProb < 0 || drawProb < 0 || awayWinProb < 0)
                throw new ArgumentException("Probabilities cannot be negative.");
            if (homeWinProb > 1 || drawProb > 1 || awayWinProb > 1)
                throw new ArgumentException("Probabilities cannot be greater than 1.");
            if (Math.Abs(homeWinProb + drawProb + awayWinProb - 1.0) > 0.05)
                throw new ArgumentException("Probabilities must sum approximately to 1.");
        }
    }
}
