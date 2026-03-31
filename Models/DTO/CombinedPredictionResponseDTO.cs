namespace BPFL.API.Models.DTO
{
    public class CombinedPredictionResponseDTO
    {
        public AIPredictionResponseDTO AIPredictionResponseDTO { get; set; } = null!;

        public PredictionResponseDTO PredictionResponseDTO { get; set; } = null!;
    }
}
