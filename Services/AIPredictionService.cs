using BPFL.API.Models.DTO;
using BPFL.API.Models.MatchAnalysis;

namespace BPFL.API.Services
{
    public class AIPredictionService
    {
        private readonly ILogger<AIPredictionService> _logger;
        

        public AIPredictionService(ILogger<AIPredictionService> logger)
        {
            _logger = logger;
        }

        public AIPredictionResponseDTO AIBuildPrediction(MatchAnalysisDTO matchAnalysisDTO, PredictionModelDTO predictionModelDTO)
        {
            
            ArgumentNullException.ThrowIfNull(matchAnalysisDTO);
            ArgumentNullException.ThrowIfNull(predictionModelDTO);

            _logger.LogInformation(
          "Building AI prediction for match {HomeTeam} vs {AwayTeam}",
          matchAnalysisDTO.HomeTeam,
          matchAnalysisDTO.AwayTeam);

            double PredictedHomeGoals = Math.Round(predictionModelDTO.ExpectedHomeGoals);
            double PredictedAwayGoals = Math.Round(predictionModelDTO.ExpectedAwayGoals);

            double HomeWinProb = predictionModelDTO.HomeWinProbavility;
            double DrawProb = predictionModelDTO.DrawProbability;
            double AwayWinProb = predictionModelDTO.AwayWinProbability;

            ValidateProbabilities(HomeWinProb, DrawProb, AwayWinProb);

            var (pick, confidence) = DeterminePick(HomeWinProb, DrawProb, AwayWinProb);

            _logger.LogInformation(
                "Probabilities for {HomeTeam} vs {AwayTeam}: Home={HomeWinProb}, Draw={DrawProb}, Away={AwayWinProb}",
                matchAnalysisDTO.HomeTeam,
                matchAnalysisDTO.AwayTeam,
                HomeWinProb,
                DrawProb,
                AwayWinProb);


            var result = new AIPredictionResponseDTO
            {
                PredictedHomeScore = PredictedHomeGoals,
                PredictedAwayScore = PredictedAwayGoals,
                Pick = pick,
                Confidence = confidence,
                HomeWinProbability = HomeWinProb * 100,
                DrawProbability = DrawProb * 100,
                AwayWinProbability = AwayWinProb * 100
            };

            return result;
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

        private static void ValidateProbabilities(
        double homeWinProb,
        double drawProb,
        double awayWinProb)
        {
            if (homeWinProb < 0 || drawProb < 0 || awayWinProb < 0)
            {
                throw new ArgumentException("Probabilities cannot be negative.");
            }

            if (homeWinProb > 1 || drawProb > 1 || awayWinProb > 1)
            {
                throw new ArgumentException("Probabilities cannot be greater than 1.");
            }

            double total = homeWinProb + drawProb + awayWinProb;

            if (Math.Abs(total - 1.0) > 0.05)
            {
                throw new ArgumentException("Probabilities must sum approximately to 1.");
            }
        }

    }
}
