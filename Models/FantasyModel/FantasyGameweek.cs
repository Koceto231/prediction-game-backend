namespace BPFL.API.Models.FantasyModel
{
    public class FantasyGameweek
    {

        public int Id { get; set; }

        public int GameWeek { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public DateTime Deadline { get; set; }

        public bool IsLocked { get; set; } = false;

        public bool IsCompleted { get; set; } = false;

        public DateTime CreatedAt { get; set; }

        public DateTime LastUpdatedAt { get; set; }


    }
}
