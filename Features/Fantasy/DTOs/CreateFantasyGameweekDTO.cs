namespace BPFL.API.Features.Fantasy
{
    public class CreateFantasyGameweekDTO
    {
        public int GameWeek { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime Deadline { get; set; }
    }
}
