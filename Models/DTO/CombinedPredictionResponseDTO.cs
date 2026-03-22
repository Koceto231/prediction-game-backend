namespace BPFL.API.Models.DTO
{
    public class CombinedPredictionResponseDTO
    {
        public AIPredictionResponseDTO AIPredictionResponseDTO { get; set; }

        public PredictionResponseDTO PredictionResponseDTO { get; set; }
    }
}
