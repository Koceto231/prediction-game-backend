namespace BPFL.API.Models
{
    public enum NewsType { MatchPreview = 1, MatchReport = 2, LeagueSummary = 3 }

    public class NewsArticle
    {
        public int      Id          { get; set; }
        public NewsType Type        { get; set; }
        public string   Title       { get; set; } = null!;
        public string   Body        { get; set; } = null!;

        /// <summary>Set for MatchPreview / MatchReport, null for LeagueSummary.</summary>
        public int?     MatchId     { get; set; }
        public Match?   Match       { get; set; }

        /// <summary>Set for LeagueSummary (e.g. "BGL", "PL").</summary>
        public string?  LeagueCode  { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
