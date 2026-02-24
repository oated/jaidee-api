using JaiDee.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JaiDee.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
  public AppDbContext(DbContextOptions<AppDbContext> options)
      : base(options)
  {
  }

  public DbSet<User> Users => Set<User>();
  public DbSet<Transaction> Transactions => Set<Transaction>();
  public DbSet<ConversationState> ConversationStates => Set<ConversationState>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<User>(entity =>
    {
      entity.HasIndex(u => u.LineUserId).IsUnique();
    });

    modelBuilder.Entity<Transaction>(entity =>
    {
      entity.HasIndex(t => t.UserId);
      entity.HasIndex(t => t.TransactionDate);

      entity.Property(t => t.Amount)
                .HasColumnType("numeric(18,2)");
    });

    modelBuilder.Entity<ConversationState>(entity =>
    {
      entity.HasIndex(x => x.LineUserId).IsUnique();
      entity.Property(x => x.PendingAmount).HasColumnType("numeric(18,2)");
    });
  }
}
