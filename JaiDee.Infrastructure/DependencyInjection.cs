using JaiDee.Application.Interfaces.Repositories;
using JaiDee.Infrastructure.Persistence;
using JaiDee.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JaiDee.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("DefaultConnection")
      ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

    services.AddDbContext<AppDbContext>(options =>
      options.UseNpgsql(connectionString));

    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<ITransactionRepository, TransactionRepository>();
    services.AddScoped<IConversationStateRepository, ConversationStateRepository>();

    return services;
  }
}
