using System.ComponentModel.DataAnnotations;

namespace BPFL.API.Models
{
    public class Match
    {
        public int Id { get; set; }

        public int ExternalId { get; set; }


        [Required]
        public int HomeTeamId { get; set; }

        [Required]
        public int AwayTeamId { get; set; } 

        [Required]
        public DateTime MatchDate { get; set; }

        public int? HomeScore { get; set; }

        public int? AwayScore { get; set; }

        public int? MatchDay { get; set; }

        public string Status { get; set; } = null!;

        /// <summary>League code this match belongs to (BGL, PL, BL1, SA, PD). Set during sync.</summary>
        public string? LeagueCode { get; set; }

        public decimal? HomeOdds { get; set; }
        public decimal? DrawOdds { get; set; }
        public decimal? AwayOdds { get; set; }

        public double? ExpectedHomeGoals { get; set; }
        public double? ExpectedAwayGoals { get; set; }

        // Populated by Sportmonks stats sync — used to resolve Corners / YellowCards bets
        public int? TotalCorners { get; set; }
        public int? TotalYellowCards { get; set; }

        public List<Prediction?> Predictions { get; set; } = new List<Prediction?>();
        public List<Bet> Bets { get; set; } = new List<Bet>();

        [Required]
        public Team HomeTeam { get; set; } = null!;

        [Required]
        public Team AwayTeam { get; set; } = null!;

       

    }
}
