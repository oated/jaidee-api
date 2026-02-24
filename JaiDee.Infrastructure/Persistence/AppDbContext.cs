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
  }
}