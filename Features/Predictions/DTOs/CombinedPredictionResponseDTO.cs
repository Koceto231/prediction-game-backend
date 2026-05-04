namespace BPFL.API.Features.Predictions
{
    public class CombinedPredictionResponseDTO
    {
        public AIPredictionResponseDTO AIPredictionResponseDTO { get; set; } = null!;

        public PredictionResponseDTO PredictionResponseDTO { get; set; } = null!;
    }
}
