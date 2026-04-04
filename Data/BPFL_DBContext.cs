using BPFL.API.Models;
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
        public DbSet<News> News { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Prediction> Predictions { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Team> Teams { get; set; }

        public DbSet<League> Leagues { get; set; }

        public DbSet<LeagueMember> LeagueMembers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
