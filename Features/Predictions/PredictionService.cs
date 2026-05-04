using BPFL.API.Data;
using BPFL.API.Exceptions;
using BPFL.API.Models;
using BPFL.API.Features.Predictions;
using BPFL.API.Features.Matches;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Features.Predictions
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

        public async Task<CombinedPredictionResponseDTO> CreatePredictionAsync(int userId,CreatePredictionDTO createPredictionDTO,
                         CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(createPredictionDTO);

            ValidateUserId(userId);
            ValidatePredictionInput(createPredictionDTO);

            var match = await GetMatchWithTeamsAsync(createPredictionDTO.MatchId, ct);
            ValidateMatchNotStarted(match);
            await ValidateNoPreviousPredictionAsync(userId, match.Id, ct);

            var newPrediction = new Prediction
            {
                UserId = userId,
                MatchId = createPredictionDTO.MatchId,
                PredictionHomeScore = createPredictionDTO.PredictionHomeScore,
                PredictionAwayScore = createPredictionDTO.PredictionAwayScore,
                PredictionWinner = createPredictionDTO.PredictionWinner,
                PredictionBTTS = createPredictionDTO.PredictionBTTS,
                PredictionOULine = createPredictionDTO.PredictionOULine,
                PredictionOUPick = createPredictionDTO.PredictionOUPick,
                CreatedAt = DateTime.UtcNow
            };

            bPFL_DBContext.Predictions.Add(newPrediction);
            await bPFL_DBContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Prediction created: UserId={UserId}, MatchId={MatchId}, Score={Home}-{Away}",
                userId, match.Id, newPrediction.PredictionHomeScore, newPrediction.PredictionAwayScore);

            return await BuildCombinedPredictionResponseAsync(newPrediction, match, ct);
        }

        public async Task<CombinedPredictionResponseDTO> UpdatePredictionAsync(
            int matchId,
            int userId,
            CreatePredictionDTO createPredictionDTO,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(createPredictionDTO);

            ValidateUserId(userId);
            ValidatePredictionInput(createPredictionDTO);

            var prediction = await bPFL_DBContext.Predictions
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MatchId == matchId, ct);

            if (prediction == null)
            {
                throw new PredictionException(
                    "Prediction not found",
                    PredictionErrorType.PredictionNotFound);
            }

            var match = await GetMatchWithTeamsAsync(matchId, ct);
            ValidateMatchNotStarted(match);

            prediction.PredictionHomeScore = createPredictionDTO.PredictionHomeScore;
            prediction.PredictionAwayScore = createPredictionDTO.PredictionAwayScore;
            prediction.PredictionWinner = createPredictionDTO.PredictionWinner;
            prediction.PredictionBTTS = createPredictionDTO.PredictionBTTS;
            prediction.PredictionOULine = createPredictionDTO.PredictionOULine;
            prediction.PredictionOUPick = createPredictionDTO.PredictionOUPick;

            await bPFL_DBContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Prediction updated: UserId={UserId}, MatchId={MatchId}, Score={Home}-{Away}",
                userId, matchId, prediction.PredictionHomeScore, prediction.PredictionAwayScore);

            return await BuildCombinedPredictionResponseAsync(prediction, match, ct);
        }

        private async Task<CombinedPredictionResponseDTO> BuildCombinedPredictionResponseAsync(
            Prediction prediction,
            Match match,
            CancellationToken ct = default)
        {
            var matchAnalysis = await matchAnalysisService.AnalyzeMatch(match, ct);
            var predictionModel = predictionModelService.BuildModel(matchAnalysis);
            var aiPrediction = await aIPredictionService.AIBuildPredictionAsync(matchAnalysis, predictionModel, ct);

            var userPrediction = new PredictionResponseDTO
            {
                Id = prediction.Id,
                MatchId = prediction.MatchId,
                HomeTeam = match.HomeTeam.Name,
                AwayTeam = match.AwayTeam.Name,
                PredictedHomeScore = prediction.PredictionHomeScore,
                PredictedAwayScore = prediction.PredictionAwayScore,
                CreatedAt = prediction.CreatedAt
            };

            return new CombinedPredictionResponseDTO
            {
                PredictionResponseDTO = userPrediction,
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

                    PredictionWinner = k.PredictionWinner,
                    PredictionBTTS = k.PredictionBTTS,
                    PredictionOULine = k.PredictionOULine,
                    PredictionOUPick = k.PredictionOUPick,

                    Points = k.Points,
                    CreatedAt = k.CreatedAt
                }).
                ToListAsync(ct);

            return myMathes;
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
        private static void ValidatePredictionInput(CreatePredictionDTO createPredictionDTO)
        {
            bool hasScore = createPredictionDTO.PredictionHomeScore.HasValue && createPredictionDTO.PredictionAwayScore.HasValue;
            bool hasWinner = createPredictionDTO.PredictionWinner.HasValue;

            if (!hasScore && !hasWinner)
            {
                throw new PredictionException(
            "You must provide either a score prediction or a match winner.",
            PredictionErrorType.InvalidInput);
            }

            if (createPredictionDTO.PredictionHomeScore.HasValue != createPredictionDTO.PredictionAwayScore.HasValue)
                throw new PredictionException(
                    "Both home and away scores must be provided together.",
                    PredictionErrorType.InvalidInput);

            if (hasScore)
            {
                if (createPredictionDTO.PredictionHomeScore < 0 || createPredictionDTO.PredictionAwayScore < 0)
                    throw new PredictionException(
                        "Scores cannot be negative.",
                        PredictionErrorType.InvalidInput);

                if (createPredictionDTO.PredictionHomeScore > 20 || createPredictionDTO.PredictionAwayScore > 20)
                    throw new PredictionException(
                        "Scores seem unrealistic.",
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

        public async Task<Match?> GetMatchForAnalysisAsync(int matchId, CancellationToken ct = default)
        {
            return await bPFL_DBContext.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);
        }
    }
}
