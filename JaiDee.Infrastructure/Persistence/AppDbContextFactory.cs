using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JaiDee.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
  public AppDbContext CreateDbContext(string[] args)
  {
    var connectionString =
      Environment.GetEnvironmentVariable("JAIDEE_DB_CONNECTION")
      ?? "Host=localhost;Port=5432;Database=jaidee_db;Username=postgres;Password=postgres;";

    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
    optionsBuilder.UseNpgsql(connectionString);

    return new AppDbContext(optionsBuilder.Options);
  }
}
