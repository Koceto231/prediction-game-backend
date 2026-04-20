namespace BPFL.API.Models.ExternalDto
{
    public class ExternalMatchDetailDTO
    {
        public int Id { get; set; }
        public string Status { get; set; } = null!;
        public List<ExternalGoalDTO> Goals { get; set; } = new();
        public List<ExternalBookingDTO> Bookings { get; set; } = new();
    }

    public class ExternalGoalDTO
    {
        public ExternalPersonRefDTO? Scorer { get; set; }
        public ExternalPersonRefDTO? Assist { get; set; }
    }

    public class ExternalBookingDTO
    {
        public ExternalPersonRefDTO? Player { get; set; }
        /// <summary>YELLOW_CARD or RED_CARD</summary>
        public string Card { get; set; } = null!;
    }

    public class ExternalPersonRefDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
