using BPFL.API.Data;
using BPFL.API.Exceptions;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;
using static BPFL.API.Models.Predictionenums;
using Match = BPFL.API.Models.Match;

namespace BPFL.API.Services
{
    public enum ScoringErrorType
    {
        MatchNotFound,
        MatchNotFinished,
        ScoreNotAvailable
    }

    public class PredictionScoringService
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<PredictionScoringService> _logger;
        private readonly LeaderboardService leaderboardService;

        public PredictionScoringService(
            BPFL_DBContext _bPFL_DBContext,
            ILogger<PredictionScoringService> logger,
            LeaderboardService _leaderboardService)
        {
            bPFL_DBContext = _bPFL_DBContext;
            _logger = logger;
           leaderboardService = _leaderboardService;
        }

        public async Task<PredictionScoringResultDto> ScoreMatchPredictionsAsync(
            int matchId,
            CancellationToken ct = default)
        {
            if (matchId <= 0)
            {
                throw new ArgumentException("Invalid match ID", nameof(matchId));
            }

            var match = await bPFL_DBContext.Matches
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == matchId, ct);

            if (match == null)
            {
                throw new ScoringException(
                    $"Match with ID {matchId} not found.",
                    ScoringErrorType.MatchNotFound);
            }

            if (match.Status != "FINISHED")
            {
                throw new ScoringException(
                    $"Match {matchId} is not finished yet. Current status: {match.Status}",
                    ScoringErrorType.MatchNotFinished);
            }

            if (match.HomeScore == null || match.AwayScore == null)
            {
                throw new ScoringException(
                    $"Match {matchId} has no score recorded.",
                    ScoringErrorType.ScoreNotAvailable);
            }

            var predictionsForThisMatch = await bPFL_DBContext.Predictions
                .Where(x => x.MatchId == matchId)
                .ToListAsync(ct);

            if (predictionsForThisMatch.Count == 0)
            {
                _logger.LogInformation(
                    "No unscored predictions found for match {MatchId}",
                    matchId);

                return new PredictionScoringResultDto
                {
                    MatchId = matchId,
                    ScoredPredictionsCount = 0
                };
            }

            int totalGoals = match.HomeScore.Value + match.AwayScore.Value;
            bool actualBTTS = match.HomeScore.Value > 0 && match.AwayScore.Value > 0;
            MatchWinner actualWinner = DetermineWinner(match.HomeScore.Value, match.AwayScore.Value);

            foreach (var prediction in predictionsForThisMatch)
            {
                // informational fields for UI/debug
                prediction.ActualWinner = actualWinner;

                if (prediction.PredictionBTTS.HasValue)
                {
                    bool isCorrectBTTS = prediction.PredictionBTTS.Value == actualBTTS;
                    prediction.IsCorrectBTTS = isCorrectBTTS;
                    prediction.PointsFromBTTS = isCorrectBTTS ? 1 : 0;
                }
                else
                {
                    prediction.IsCorrectBTTS = null;
                    prediction.PointsFromBTTS = null;
                }

                if (prediction.PredictionOULine.HasValue && prediction.PredictionOUPick.HasValue)
                {
                    bool actualOver = IsOver(totalGoals, prediction.PredictionOULine.Value);
                    bool predictedOver = prediction.PredictionOUPick.Value == OverUnderPick.Over;

                    prediction.ActualOUResult = actualOver;
                    prediction.BonusPointsOU = predictedOver == actualOver ? 1 : 0;
                }
                else
                {
                    prediction.ActualOUResult = null;
                    prediction.BonusPointsOU = null;
                }

                if (prediction.PredictionWinner.HasValue)
                {
                    prediction.BonusPointsWinner =
                        prediction.PredictionWinner.Value == actualWinner ? 1 : 0;
                }
                else
                {
                    prediction.BonusPointsWinner = null;
                }

                // final points by new rules
                prediction.Points = CalculatePoints(match, prediction);
            }

            await bPFL_DBContext.SaveChangesAsync(ct);

            leaderboardService.InvalidateLeaderboardCache();

            var result = new PredictionScoringResultDto
            {
                MatchId = matchId,
                ScoredPredictionsCount = predictionsForThisMatch.Count
            };

            _logger.LogInformation(
                "Scored {Count} predictions for match {MatchId}",
                result.ScoredPredictionsCount,
                matchId);

            return result;
        }

        internal static MatchWinner DetermineWinner(int homeScore, int awayScore)
        {
            if (homeScore > awayScore)
            {
                return MatchWinner.Home;
            }

            if (awayScore > homeScore)
            {
                return MatchWinner.Away;
            }

            return MatchWinner.Draw;
        }

        internal static bool IsOver(int totalGoals, OverUnderLine line)
        {
            return line switch
            {
                OverUnderLine.Line15 => totalGoals >= 2,
                OverUnderLine.Line25 => totalGoals >= 3,
                OverUnderLine.Line35 => totalGoals >= 4,
                _ => throw new ArgumentOutOfRangeException(nameof(line), line, "Invalid Over/Under line.")
            };
        }

        private static int CalculatePoints(Match match, Prediction prediction)
        {
            ArgumentNullException.ThrowIfNull(match);
            ArgumentNullException.ThrowIfNull(prediction);

            bool hasExactScorePrediction =
                prediction.PredictionHomeScore.HasValue &&
                prediction.PredictionAwayScore.HasValue;

            if (hasExactScorePrediction)
            {
                return prediction.PredictionHomeScore == match.HomeScore &&
                       prediction.PredictionAwayScore == match.AwayScore
                    ? 5
                    : 0;
            }

            int points = 0;

            var actualWinner = DetermineWinner(match.HomeScore!.Value, match.AwayScore!.Value);
            bool actualBTTS = match.HomeScore.Value > 0 && match.AwayScore.Value > 0;
            int totalGoals = match.HomeScore.Value + match.AwayScore.Value;

            if (prediction.PredictionWinner.HasValue &&
                prediction.PredictionWinner.Value == actualWinner)
            {
                points += 1;
            }

            if (prediction.PredictionBTTS.HasValue &&
                prediction.PredictionBTTS.Value == actualBTTS)
            {
                points += 1;
            }

            if (prediction.PredictionOULine.HasValue && prediction.PredictionOUPick.HasValue)
            {
                bool actualOver = IsOver(totalGoals, prediction.PredictionOULine.Value);
                bool predictedOver = prediction.PredictionOUPick.Value == OverUnderPick.Over;

                if (actualOver == predictedOver)
                {
                    points += 1;
                }
            }

            return points;
        }
    }
}
