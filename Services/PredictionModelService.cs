using BPFL.API.Models.DTO;
using BPFL.API.Models.MatchAnalysis;

namespace BPFL.API.Services
{
    public class PredictionModelService
    {
        private readonly ILogger<PredictionModelService> _logger;

        public PredictionModelService(ILogger<PredictionModelService> logger)
        {
            _logger = logger;
        }

        public PredictionModelDTO BuildModel(MatchAnalysisDTO matchAnalysisDTO)
        {
            ArgumentNullException.ThrowIfNull(matchAnalysisDTO);

            _logger.LogInformation(
                "Building prediction model for {HomeTeam} vs {AwayTeam}",
                matchAnalysisDTO.HomeTeam,
                matchAnalysisDTO.AwayTeam);

            double homeAttackStrength =
                (matchAnalysisDTO.HomeAverageGoalsScored * 0.6) +
                (matchAnalysisDTO.HomeAverageGoalsAtHome * 0.4);

            double awayAttackStrength =
                (matchAnalysisDTO.AwayAverageGoalsScored * 0.6) +
                (matchAnalysisDTO.AwayAverageGoalsAtAway * 0.4);

            double homeDefenseWeakness = matchAnalysisDTO.HomeAverageGoalsConceded;
            double awayDefenseWeakness = matchAnalysisDTO.AwayAverageGoalsConceded;

            double homeFormFactor = matchAnalysisDTO.HomeRecentFromPoints / 15.0;
            double awayFormFactor = matchAnalysisDTO.AwayRecentFromPoints / 15.0;

            double homeAdvantage = 0.25;

            double expectedHomeGoals =
                (homeAttackStrength * 0.55) +
                (awayDefenseWeakness * 0.30) +
                (homeFormFactor * 0.35) +
                homeAdvantage;

            double expectedAwayGoals =
                (awayAttackStrength * 0.55) +
                (homeDefenseWeakness * 0.30) +
                (awayFormFactor * 0.35);

            expectedHomeGoals = Math.Clamp(expectedHomeGoals, 0.2, 4.5);
            expectedAwayGoals = Math.Clamp(expectedAwayGoals, 0.2, 4.5);

            double goalDifference = expectedHomeGoals - expectedAwayGoals;

            double homeWinProbability;
            double drawProbability;
            double awayWinProbability;

            if (goalDifference > 1.2)
            {
                homeWinProbability = 0.65;
                drawProbability = 0.20;
                awayWinProbability = 0.15;
            }
            else if (goalDifference > 0.6)
            {
                homeWinProbability = 0.52;
                drawProbability = 0.27;
                awayWinProbability = 0.21;
            }
            else if (goalDifference > 0.2)
            {
                homeWinProbability = 0.45;
                drawProbability = 0.30;
                awayWinProbability = 0.25;
            }
            else if (goalDifference >= -0.2)
            {
                homeWinProbability = 0.34;
                drawProbability = 0.33;
                awayWinProbability = 0.33;
            }
            else if (goalDifference >= -0.6)
            {
                homeWinProbability = 0.25;
                drawProbability = 0.30;
                awayWinProbability = 0.45;
            }
            else if (goalDifference >= -1.2)
            {
                homeWinProbability = 0.21;
                drawProbability = 0.27;
                awayWinProbability = 0.52;
            }
            else
            {
                homeWinProbability = 0.15;
                drawProbability = 0.20;
                awayWinProbability = 0.65;
            }

            var total = homeWinProbability + drawProbability + awayWinProbability;

            homeWinProbability /= total;
            drawProbability /= total;
            awayWinProbability /= total;

            return new PredictionModelDTO
            {
                ExpectedHomeGoals = Math.Round(expectedHomeGoals, 2),
                ExpectedAwayGoals = Math.Round(expectedAwayGoals, 2),
                HomeWinProbavility = Math.Round(homeWinProbability, 4),
                DrawProbability = Math.Round(drawProbability, 4),
                AwayWinProbability = Math.Round(awayWinProbability, 4)
            };
        }
    }
}