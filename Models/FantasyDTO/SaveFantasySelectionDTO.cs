namespace BPFL.API.Models.FantasyDTO
{
    public class SaveFantasySelectionDTO
    {
        public int FantasyGameweekId { get; set; }

        public List<int> SelectedPlayerIds { get; set; } = new List<int>();

        public int CaptainPlayerId { get; set; }

    }
}
