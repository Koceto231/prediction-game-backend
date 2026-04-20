namespace BPFL.API.Models.FantasyDTO
{
    public class SubmitPlayerStatsDTO
    {
        public int FantasyPlayerId { get; set; }
        public int MatchId { get; set; }
        public bool Appeared { get; set; } = true;
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
    }
}
