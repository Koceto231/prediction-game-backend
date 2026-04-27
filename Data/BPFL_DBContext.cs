using BPFL.API.Models;
using BPFL.API.Models.FantasyModel;
using Microsoft.EntityFrameworkCore;

namespace BPFL.API.Data
{
    public class BPFL_DBContext :DbContext
    {

        public BPFL_DBContext(DbContextOptions<BPFL_DBContext> options) : base(options)
        {

        }

        public DbSet<User> Users { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<Prediction> Predictions { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Team> Teams { get; set; }

        public DbSet<League> Leagues { get; set; }

        public DbSet<LeagueMember> LeagueMembers { get; set; }

        public DbSet<FantasyPlayer> FantasyPlayers { get; set; }
        public DbSet<FantasyTeam> FantasyTeams { get; set; }
        public DbSet<FantasyGameweek> FantasyGameweeks { get; set; }
        public DbSet<FantasyTeamSelection> FantasyTeamSelections  { get; set; }
        public DbSet<PlayerMatchFantasyStat> PlayerMatchFantasyStats { get; set; }
        public DbSet<FantasyScore> FantasyScores { get; set; }
        public DbSet<Bet> Bets { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Bet>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Amount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(x => x.OddsAtBetTime)
                    .HasColumnType("decimal(18,2)");

                entity.Property(x => x.PotentialPayout)
                    .HasColumnType("decimal(18,2)");

                entity.Property(x => x.ActualPayout)
                    .HasColumnType("decimal(18,2)");

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Match)
                    .WithMany(m => m.Bets)
                    .HasForeignKey(x => x.MatchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.UserId, x.Status });
                entity.HasIndex(x => new { x.MatchId, x.Status });
            });



            modelBuilder.Entity<FantasyPlayer>()
                .HasIndex(p => p.ExternalPlayerId)
                .IsUnique();

            modelBuilder.Entity<FantasyTeam>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            modelBuilder.Entity<FantasyTeamSelection>()
                .HasIndex(p => new
                {
                    p.FantasyTeamId,
                    p.FantasyGameweekId,
                    p.FantasyPlayerId

                })
                .IsUnique();

            modelBuilder.Entity<PlayerMatchFantasyStat>()
                 .HasIndex(p => new
                 {
                     p.MatchId,
                     p.FantasyPlayerId

                 })
                .IsUnique();

            modelBuilder.Entity<FantasyScore>()
                 .HasIndex(p => new
                 {
                     p.FantasyTeamId,
                     p.FantasyGameweekId

                 })
                .IsUnique();

            modelBuilder.Entity<FantasyPlayer>()
    .HasOne(fp => fp.Team)
    .WithMany()
    .HasForeignKey(fp => fp.TeamId)
    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FantasyTeam>()
                .HasOne(ft => ft.User)
                .WithMany()
                .HasForeignKey(ft => ft.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FantasyTeamSelection>()
                .HasOne(s => s.FantasyTeam)
                .WithMany()
                .HasForeignKey(s => s.FantasyTeamId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FantasyTeamSelection>()
                .HasOne(s => s.FantasyPlayer)
                .WithMany()
                .HasForeignKey(s => s.FantasyPlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FantasyTeamSelection>()
                .HasOne(s => s.FantasyGameweek)
                .WithMany()
                .HasForeignKey(s => s.FantasyGameweekId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerMatchFantasyStat>()
                .HasOne(pms => pms.FantasyPlayer)
                .WithMany()
                .HasForeignKey(pms => pms.FantasyPlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerMatchFantasyStat>()
                .HasOne(pms => pms.Match)
                .WithMany()
                .HasForeignKey(pms => pms.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FantasyScore>()
                .HasOne(fs => fs.FantasyTeam)
                .WithMany()
                .HasForeignKey(fs => fs.FantasyTeamId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FantasyScore>()
                .HasOne(fs => fs.FantasyGameweek)
                .WithMany()
                .HasForeignKey(fs => fs.FantasyGameweekId)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<User>()
    .HasIndex(u => u.Email)
    .IsUnique();
            modelBuilder.Entity<Match>()
                .HasOne(m => m.HomeTeam)
                .WithMany()
                .HasForeignKey(m => m.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Match>()
               .HasOne(m => m.AwayTeam)
               .WithMany()
               .HasForeignKey(m => m.AwayTeamId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Prediction>()
                .HasOne(m => m.User)
                .WithMany(u => u.Predictions)
                .HasForeignKey(p => p.UserId);

            modelBuilder.Entity<Prediction>()
                .HasOne(m => m.Match)
                .WithMany(u => u.Predictions)
                .HasForeignKey(p => p.MatchId);

            modelBuilder.Entity<Prediction>()
                .HasIndex(p => new { p.UserId, p.MatchId })
                .IsUnique();

            modelBuilder.Entity<RefreshToken>()
             .HasIndex(rt => rt.TokenHash)
             .IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<League>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(e => e.InviteCode)
                      .IsRequired()
                      .HasMaxLength(8);

           
                entity.HasIndex(e => e.InviteCode)
                      .IsUnique();

                entity.HasOne(e => e.Owner)
                      .WithMany()
                      .HasForeignKey(e => e.OwnerId)
                      .OnDelete(DeleteBehavior.Restrict); 
            });

            modelBuilder.Entity<LeagueMember>(entity =>
            {
                entity.HasKey(e => e.Id);

           
                entity.HasIndex(e => new { e.LeagueId, e.UserId })
                      .IsUnique();

                entity.HasOne(e => e.League)
                      .WithMany(l => l.Members)
                      .HasForeignKey(e => e.LeagueId)
                      .OnDelete(DeleteBehavior.Cascade); 

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.GoogleId)
          .IsUnique()
          .HasFilter("\"GoogleId\" IS NOT NULL");
            });
        }

    }
}
