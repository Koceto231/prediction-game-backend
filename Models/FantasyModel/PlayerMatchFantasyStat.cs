
namespace BPFL.API.Models.FantasyModel
{
    public class PlayerMatchFantasyStat
    {
        public int Id { get; set; }

        public int FantasyPlayerId { get; set; }

        public FantasyPlayer FantasyPlayer { get; set; } = null!;

        public int MatchId { get; set; }

        public Match Match { get; set; } = null!;

        public bool IsHeAppeard { get; set; } = true;

        public int Goals { get; set; }

        public int Assists { get; set; }

        public int YellowCards { get; set; }

        public int RedCard { get; set; }

        public int FantasyPoints { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastUpdatedAt { get; set; }

    }
}
