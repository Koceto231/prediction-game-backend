namespace BPFL.API.Models.DTO
{
    public class CreatePredictionDTO
    {
        public int MatchId { get; set; }

        public int PredictionHomeScore { get; set; }

        public int PredictionAwayScore { get; set; }
    }
}
