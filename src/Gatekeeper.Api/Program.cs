using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Gatekeeper.Core.Interfaces;
using Gatekeeper.Core.Models;
using Gatekeeper.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault configuration if in production
if (builder.Environment.IsProduction())
{
    var keyVaultEndpoint = builder.Configuration["KeyVault:Endpoint"];
    if (!string.IsNullOrEmpty(keyVaultEndpoint))
    {
        var secretClient = new SecretClient(new Uri(keyVaultEndpoint), new DefaultAzureCredential());
        builder.Configuration.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());
    }
}

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddRedisRateLimiting(redisConnectionString);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapPost("/check", async (CheckRequest request, IRateLimiter limiter) =>
{
    var result = await limiter.CheckAsync(request);
    return Results.Ok(result);
});

app.MapPost("/consume", async (CheckRequest request, IRateLimiter limiter) =>
{
    var result = await limiter.ConsumeAsync(request);
    return Results.Ok(result);
});

app.Run();

public partial class Program;
