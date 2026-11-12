using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PlayerProfileRow> PlayerProfiles => Set<PlayerProfileRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var profile = modelBuilder.Entity<PlayerProfileRow>();
        profile.ToTable("PlayerProfiles");
        profile.HasKey(row => row.Id);
        profile.Property(row => row.Id).ValueGeneratedNever();
        profile.HasData(new PlayerProfileRow
        {
            Id = 1,
            TotalExperience = 0,
            CoinBalance = 0,
        });
    }
}
