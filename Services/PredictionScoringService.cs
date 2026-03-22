using BPFL.API.Data;
using BPFL.API.Exceptions;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Match = BPFL.API.Models.Match;

namespace BPFL.API.Services
{
    
    public enum ScoringErrorType
    {
        MatchNotFound,
        MatchNotFinshed,
        ScoreNotAvailable
    }
    public class PredictionScoringService
    {
        private readonly BPFL_DBContext bPFL_DBContext;
        private readonly ILogger<PredictionScoringService> _logger;

        public PredictionScoringService(BPFL_DBContext _bPFL_DBContext, ILogger<PredictionScoringService> logger)
        {
            bPFL_DBContext = _bPFL_DBContext;
            _logger = logger;
        }

        public async Task<PredictionScoringResultDto> ScoreMatchPredictionsAsync(int matchId, CancellationToken ct = default)
        {
            if(matchId <= 0)
            {
                throw new ArgumentException("Invalid match ID", nameof(matchId));
            }

            var match = await bPFL_DBContext.Matches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == matchId,ct);

            if (match == null)
            {
                throw new ScoringException($"Match with ID {matchId} not found.",ScoringErrorType.MatchNotFound);
            }

            if (match.Status != "FINISHED")
            {
                throw new ScoringException($"Match {matchId} is not finished yet. Current status: {match.Status}",ScoringErrorType.MatchNotFinshed);
            }

            if (match.HomeScore == null || match.AwayScore == null)
            {
                throw new ScoringException($"Match {matchId} has no score recorded.",ScoringErrorType.ScoreNotAvailable);
            }

            List<Prediction> predictionsForThisMatch = await bPFL_DBContext.Predictions.Where(x => x.MatchId == matchId && x.Points == null).ToListAsync(ct);

            if (predictionsForThisMatch.Count == 0)
            {
                _logger.LogInformation("No unscored predictions found for match {MatchId}", matchId);
                return new PredictionScoringResultDto { MatchId = matchId };
            }

            var result = new PredictionScoringResultDto
            {
                MatchId = matchId,
                ScoredOredictionsCount = predictionsForThisMatch.Count,
            };


            foreach (var prediction in predictionsForThisMatch)
            {
                prediction.Points = CalculatePoints(match, prediction);
            }

            await bPFL_DBContext.SaveChangesAsync(ct);
            _logger.LogInformation("Scored {Count} predictions for match {MatchId}",
                result.ScoredOredictionsCount, matchId);

            return result;

        }

        private static int CalculatePoints(Match match, Prediction prediction)
        {
            ArgumentNullException.ThrowIfNull(match);
            ArgumentNullException.ThrowIfNull(prediction);


            if (prediction.PredictionHomeScore == match.HomeScore && prediction.PredictionAwayScore == match.AwayScore )
            {
                return 3;
            }else if (prediction.PredictionHomeScore > prediction.PredictionAwayScore && match.HomeScore > match.AwayScore)
            {
                return 1;
            }
            else if (prediction.PredictionHomeScore < prediction.PredictionAwayScore && match.HomeScore < match.AwayScore)
            {
                return 1;
            }
            else if (prediction.PredictionHomeScore == prediction.PredictionAwayScore && match.HomeScore == match.AwayScore)
            {
                return 1;
            }

            return 0;
        }
    }
}
