namespace BPFL.API.Models
{
    public class League
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string InviteCode { get; set; } = null!; 
        public int OwnerId { get; set; }
        public DateTime CreatedAt { get; set; }

    
        public User Owner { get; set; } = null!;
        public ICollection<LeagueMember> Members { get; set; } = new List<LeagueMember>();
    }

    public class LeagueMember
    {
        public int Id { get; set; }
        public int LeagueId { get; set; }
        public int UserId { get; set; }
        public DateTime JoinedAt { get; set; }

        public League League { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
