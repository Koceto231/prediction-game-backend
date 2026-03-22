namespace BPFL.API.Models.DTO
{
    public class PredictionResponseDTO
    {
        public int Id { get; set;  }

        public int MatchId { get; set; }

        public string HomeTeam { get; set; } = null!;

        public int? PredictedHomeScore { get; set; }

        public string AwayTeam { get; set; } = null!;

        public int? PredictedAwayScore { get; set; }

        public DateTime CreatedAt { get; set; }


    }
}
