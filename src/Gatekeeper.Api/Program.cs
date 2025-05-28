using Gatekeeper.Core.Interfaces;
using Gatekeeper.Core.Models;
using Gatekeeper.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddRedisRateLimiting(redisConnectionString);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapGet("/ping", () => Results.Ok("pong"));

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
