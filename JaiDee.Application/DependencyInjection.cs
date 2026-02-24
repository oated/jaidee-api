using JaiDee.Application.Interfaces;
using JaiDee.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JaiDee.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    services.AddScoped<ITransactionService, TransactionService>();
    return services;
  }
}
