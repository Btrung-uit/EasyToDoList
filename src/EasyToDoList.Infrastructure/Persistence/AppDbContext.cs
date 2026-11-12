using Microsoft.EntityFrameworkCore;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Todos;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PlayerProfileRow> PlayerProfiles => Set<PlayerProfileRow>();

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    public DbSet<RewardTransaction> RewardTransactions => Set<RewardTransaction>();

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

        var todo = modelBuilder.Entity<TodoItem>();
        todo.ToTable("TodoItems");
        todo.HasKey(item => item.Id);
        todo.Property(item => item.Id).ValueGeneratedNever();
        todo.Property(item => item.Title).HasMaxLength(300).IsRequired();
        todo.Property(item => item.RowVersion).IsConcurrencyToken();
        todo.HasIndex(item => new { item.Status, item.StartDate, item.Deadline });
        todo.HasIndex(item => item.CompletedAtUtc);

        var reward = modelBuilder.Entity<RewardTransaction>();
        reward.ToTable("RewardTransactions");
        reward.HasKey(transaction => transaction.Id);
        reward.Property(transaction => transaction.Id).ValueGeneratedNever();
        reward.Property(transaction => transaction.IdempotencyKey).HasMaxLength(200).IsRequired();
        reward.HasIndex(transaction => transaction.IdempotencyKey).IsUnique();
        reward.HasIndex(transaction => transaction.CreatedAtUtc);
    }
}
