using System.ComponentModel.DataAnnotations;

namespace BPFL.API.Models
{
    public class Match
    {
        public int Id { get; set; }

        public int ExternalId { get; set; }

        public int? ApiSportsFixtureId { get; set; }

        public string? LeagueCode { get; set; }

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

        public decimal? HomeOdds { get; set; }
        public decimal? DrawOdds { get; set; }
        public decimal? AwayOdds { get; set; }

        public double? ExpectedHomeGoals { get; set; }
        public double? ExpectedAwayGoals { get; set; }

        public List<Prediction?> Predictions { get; set; } = new List<Prediction?>();
        public List<Bet> Bets { get; set; } = new List<Bet>();

        [Required]
        public Team HomeTeam { get; set; } = null!;

        [Required]
        public Team AwayTeam { get; set; } = null!;

       

    }
}
