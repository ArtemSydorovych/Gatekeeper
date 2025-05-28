using Gatekeeper.Core.Interfaces;
using Gatekeeper.Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Gatekeeper.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedisRateLimiting(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IConnectionMultiplexer>(provider =>
            ConnectionMultiplexer.Connect(connectionString));

        services.AddScoped<IRateLimiter, RedisRateLimiter>();

        return services;
    }
}
