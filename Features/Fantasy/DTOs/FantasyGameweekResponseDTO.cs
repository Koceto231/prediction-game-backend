namespace BPFL.API.Features.Fantasy
{
    public class FantasyGameweekResponseDTO
    {
        public int Id { get; set; }

        public int GameWeek { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public DateTime Deadline { get; set; }

        public bool IsLocked { get; set; }

        public bool IsCompleted { get; set; }
    }
}
