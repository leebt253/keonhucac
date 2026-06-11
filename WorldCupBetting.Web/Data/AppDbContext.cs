using Microsoft.EntityFrameworkCore;
using WorldCupBetting.Web.Models;

namespace WorldCupBetting.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<BetResult> BetResults => Set<BetResult>();
    public DbSet<GroupStanding> GroupStandings => Set<GroupStanding>();
    public DbSet<TournamentStage> TournamentStages => Set<TournamentStage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>()
            .HasIndex(x => x.UserName)
            .IsUnique();

        modelBuilder.Entity<Prediction>()
            .HasIndex(x => new { x.UserId, x.MatchId })
            .IsUnique();

        modelBuilder.Entity<Prediction>()
            .HasOne(x => x.User)
            .WithMany(x => x.Predictions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Prediction>()
            .HasOne(x => x.Match)
            .WithMany(x => x.Predictions)
            .HasForeignKey(x => x.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BetResult>()
            .HasIndex(x => new { x.UserId, x.MatchId })
            .IsUnique();

        modelBuilder.Entity<BetResult>()
            .HasOne(x => x.User)
            .WithMany(x => x.BetResults)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BetResult>()
            .HasOne(x => x.Match)
            .WithMany(x => x.BetResults)
            .HasForeignKey(x => x.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GroupStanding>()
            .HasIndex(x => new { x.GroupCode, x.TeamName })
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
