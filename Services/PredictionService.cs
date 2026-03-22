using BPFL.API.Data;
using BPFL.API.Exceptions;
using BPFL.API.Models;
using BPFL.API.Models.DTO;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Services
{
   
    public enum PredictionErrorType
    {
        MatchNotFound,
        PredictionNotFound,
        MatchAlreadyStarted,
        PredictionAlreadyExists,
        InvalidInput
    }


    public class PredictionService
    {
        private readonly BPFL_DBContext bPFL_DBContext;

        private readonly MatchAnalysisService matchAnalysisService;
        private readonly AIPredictionService aIPredictionService;
        private readonly PredictionModelService predictionModelService;
        private readonly ILogger<PredictionService> _logger;
    
        public PredictionService(BPFL_DBContext _bPFL_DBContext, MatchAnalysisService _matchAnalysisService,
            AIPredictionService _aIPredictionService, PredictionModelService _predictionModelService,
            ILogger<PredictionService> logger)

        {
            bPFL_DBContext = _bPFL_DBContext;
            matchAnalysisService = _matchAnalysisService;
            aIPredictionService = _aIPredictionService;
            predictionModelService = _predictionModelService;
            _logger = logger;
        }

        public async Task<CombinedPredictionResponseDTO> CreatePredictionAsync(int id, CreatePredictionDTO createPredictionDTO, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(createPredictionDTO);

            ValidateUserId(id);
            ValidateScores(createPredictionDTO.PredictionHomeScore, createPredictionDTO.PredictionAwayScore);
            var match = await GetMatchWithTeamsAsync(createPredictionDTO.MatchId, ct);
            ValidateMatchNotStarted(match);
            await ValidateNoPreviousPredictionAsync(id, match.Id, ct);



                var newPrediction = new Prediction
                {
                    UserId = id,
                    MatchId = createPredictionDTO.MatchId,
                    PredictionHomeScore = createPredictionDTO.PredictionHomeScore,
                    PredictionAwayScore = createPredictionDTO.PredictionAwayScore,
                    CreatedAt = DateTime.UtcNow,

                };

                bPFL_DBContext.Add(newPrediction);
                await bPFL_DBContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Prediction created: UserId={UserId}, MatchId={MatchId}, Score={Home}-{Away}",
                id, match.Id, createPredictionDTO.PredictionHomeScore, createPredictionDTO.PredictionAwayScore);


            var matchAnalyse = await matchAnalysisService.AnalyzeMatch(match,ct);

            var predictionModel = predictionModelService.BuildModel(matchAnalyse);

            var aiPrediction = aIPredictionService.AIBuildPrediction(matchAnalyse, predictionModel);


            var HumanPrediction = new PredictionResponseDTO
            {
                Id = newPrediction.Id,
                MatchId = newPrediction.MatchId,
                HomeTeam = match.HomeTeam.Name,
                AwayTeam = match.AwayTeam.Name,
                PredictedHomeScore = newPrediction.PredictionHomeScore,
                PredictedAwayScore = newPrediction.PredictionAwayScore,
                CreatedAt = newPrediction.CreatedAt,
            };



            return new CombinedPredictionResponseDTO
            {
                PredictionResponseDTO = HumanPrediction,
                AIPredictionResponseDTO = aiPrediction

            };
           
        }

        public async Task<List<PredictionResponseDTO>> GetMyPredictionsAsync(int userId, CancellationToken ct = default)
        {
            ValidateUserId(userId);
            var myMathes = await bPFL_DBContext.Predictions.AsNoTracking().Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(k => new PredictionResponseDTO
                {
                   Id = k.Id,
                   MatchId = k.MatchId,
                   HomeTeam = k.Match.HomeTeam.Name,
                   AwayTeam = k.Match.AwayTeam.Name,
                   PredictedHomeScore = k.PredictionHomeScore,
                   PredictedAwayScore = k.PredictionAwayScore,
                   CreatedAt = k.CreatedAt
                }).
                ToListAsync(ct);

            return myMathes;
        }

        
        public async Task<CombinedPredictionResponseDTO> UpdatePrediction(int matchId, int userId, CreatePredictionDTO createPredictionDTO, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(createPredictionDTO);
            ValidateUserId(userId);
            ValidateScores(createPredictionDTO.PredictionHomeScore, createPredictionDTO.PredictionAwayScore);

            var prediction = await bPFL_DBContext.Predictions.FirstOrDefaultAsync(x => x.UserId == userId && x.MatchId == matchId,ct);

            if (prediction == null)
            {
                throw new PredictionException(
                    "Prediction not found",
                    PredictionErrorType.PredictionNotFound);
            }

            var match = await GetMatchWithTeamsAsync(matchId, ct);
            ValidateMatchNotStarted(match);

            int newHomeScorePrediction = createPredictionDTO.PredictionHomeScore;
            int newAwayScorePrediction = createPredictionDTO.PredictionAwayScore;

   

            prediction.PredictionHomeScore = newHomeScorePrediction;
            prediction.PredictionAwayScore = newAwayScorePrediction;

            await bPFL_DBContext.SaveChangesAsync(ct);

            var matchAnalyse = await matchAnalysisService.AnalyzeMatch(match, ct);

            var predictionModel = predictionModelService.BuildModel(matchAnalyse);

            var aiPrediction = aIPredictionService.AIBuildPrediction(matchAnalyse, predictionModel);

            var userPrediction = new PredictionResponseDTO
            {
                Id = prediction.Id,
                MatchId = matchId,
                HomeTeam = match.HomeTeam.Name,
                AwayTeam = match.AwayTeam.Name,
                PredictedHomeScore = newHomeScorePrediction,
                PredictedAwayScore = newAwayScorePrediction,
                CreatedAt = prediction.CreatedAt,


            };
          
            var result = new CombinedPredictionResponseDTO 
            { 
                PredictionResponseDTO = userPrediction,
                AIPredictionResponseDTO = aiPrediction
            };

            return result;
        }

        private async Task<Match> GetMatchWithTeamsAsync(int matchId, CancellationToken ct)
        {
            if (matchId <= 0)
            {
                throw new PredictionException(
                    "Invalid match ID",
                    PredictionErrorType.InvalidInput);
            }
            var match = await bPFL_DBContext.Matches
                .AsNoTracking()
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);
            if (match == null)
            {
                throw new PredictionException(
                    $"Match with ID {matchId} not found",
                    PredictionErrorType.MatchNotFound);
            }
            return match;
        }

        private async Task ValidateNoPreviousPredictionAsync(
           int userId,
           int matchId,
           CancellationToken ct)
        {
            var exists = await bPFL_DBContext.Predictions
                .AsNoTracking()
                .AnyAsync(p => p.UserId == userId && p.MatchId == matchId, ct);
            if (exists)
            {
                throw new PredictionException(
                    "You have already predicted this match",
                    PredictionErrorType.PredictionAlreadyExists);
            }
        }
        private static void ValidateUserId(int userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("Invalid user ID", nameof(userId));
            }
        }
        private static void ValidateScores(int homeScore, int awayScore)
        {
            if (homeScore < 0 || awayScore < 0)
            {
                throw new PredictionException(
                    "Scores cannot be negative",
                    PredictionErrorType.InvalidInput);
            }
            if (homeScore > 20 || awayScore > 20)
            {
                throw new PredictionException(
                    "Scores seem unrealistic",
                    PredictionErrorType.InvalidInput);
            }
        }
        private static void ValidateMatchNotStarted(Match match)
        {
            if (match.MatchDate <= DateTime.UtcNow)
            {
                throw new PredictionException(
                    "Cannot create or update prediction after match has started",
                    PredictionErrorType.MatchAlreadyStarted);
            }
        }
    }
}
