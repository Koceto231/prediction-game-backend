namespace BPFL.API.Models.ExternalDto
{
    public class ExternalSquadPlayerDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        /// <summary>Position string as returned by football-data.org (e.g. "Goalkeeper", "Centre-Back", "Right Winger")</summary>
        public string? Position { get; set; }
    }
}
