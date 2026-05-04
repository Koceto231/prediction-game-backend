namespace BPFL.API.Features.Fantasy
{
    public class FantasyTeamResponseDTO
    {
        public int FantasyTeamId { get; set; }

        public string TeamName { get; set; } = null!;

        public decimal Budget { get; set; }

        public decimal RemainingBudget { get; set; }

        public int FantasyGameweekId { get; set; }

        public int GameWeek { get; set; }

        public bool IsLocked { get; set; }

        public int WeeklyPoints { get; set; }

        public List<FantasySelectedPlayerResponseDTO> Players { get; set; } = new List<FantasySelectedPlayerResponseDTO>();
    }
}
