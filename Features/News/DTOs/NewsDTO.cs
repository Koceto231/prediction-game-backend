using BPFL.API.Models;

namespace BPFL.API.Features.News
{
    public class NewsArticleDTO
    {
        public int       Id          { get; set; }
        public NewsType  Type        { get; set; }
        public string    TypeLabel   { get; set; } = null!;
        public string    Title       { get; set; } = null!;
        public string    Body        { get; set; } = null!;
        public int?      MatchId     { get; set; }
        public string?   HomeTeam    { get; set; }
        public string?   AwayTeam    { get; set; }
        public string?   LeagueCode  { get; set; }
        public string?   ImageUrl    { get; set; }
        public DateTime  GeneratedAt { get; set; }
    }
}
