using Gatekeeper.Core.Interfaces;
using Gatekeeper.Core.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Gatekeeper.Infrastructure.Redis;

public class RedisRateLimiter : IRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimiter> _logger;
    private readonly IDatabase _database;

    private const int WindowSizeSeconds = 60;
    private const int MaxRequests = 10;

    public RedisRateLimiter(IConnectionMultiplexer redis, ILogger<RedisRateLimiter> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = _redis.GetDatabase();
    }

    public async Task<RateLimitResult> CheckAsync(CheckRequest request, CancellationToken cancellationToken = default)
    {
        var key = BuildRedisKey(request);
        var currentWindow = GetCurrentWindow();
        var windowKey = $"{key}:{currentWindow}";

        try
        {
            var currentCount = await _database.StringGetAsync(windowKey);
            var count = currentCount.HasValue ? (int)currentCount : 0;

            var allowed = count < MaxRequests;
            var remaining = Math.Max(0, MaxRequests - count);
            var resetIn = GetTimeToNextWindow();

            _logger.LogDebug("Rate limit check for {Key}: {Count}/{MaxRequests}, Allowed: {Allowed}",
                windowKey, count, MaxRequests, allowed);

            return new RateLimitResult(allowed, remaining, resetIn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for key {Key}", windowKey);
            return new RateLimitResult(true, MaxRequests, TimeSpan.FromSeconds(WindowSizeSeconds));
        }
    }

    public async Task<RateLimitResult> ConsumeAsync(CheckRequest request, CancellationToken cancellationToken = default)
    {
        var key = BuildRedisKey(request);
        var currentWindow = GetCurrentWindow();
        var windowKey = $"{key}:{currentWindow}";

        try
        {
            const string Script = @"
                local key = KEYS[1]
                local window_size = tonumber(ARGV[1])
                local max_requests = tonumber(ARGV[2])
                
                local current = redis.call('GET', key)
                if current == false then
                    current = 0
                else
                    current = tonumber(current)
                end
                
                if current < max_requests then
                    local new_count = redis.call('INCR', key)
                    if new_count == 1 then
                        redis.call('EXPIRE', key, window_size)
                    end
                    return {1, max_requests - new_count, new_count}
                else
                    return {0, 0, current}
                end";

            var result = await _database.ScriptEvaluateAsync(Script,
                [windowKey],
                [WindowSizeSeconds, MaxRequests]);

            var resultArray = (RedisValue[])result!;
            var allowed = (int)resultArray[0] == 1;
            var remaining = (int)resultArray[1];
            var currentCount = (int)resultArray[2];
            var resetIn = GetTimeToNextWindow();

            _logger.LogDebug("Rate limit consume for {Key}: {Count}/{MaxRequests}, Allowed: {Allowed}",
                windowKey, currentCount, MaxRequests, allowed);

            return new RateLimitResult(allowed, remaining, resetIn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consuming rate limit for key {Key}", windowKey);
            return new RateLimitResult(false, 0, TimeSpan.FromSeconds(WindowSizeSeconds));
        }
    }

    private static string BuildRedisKey(CheckRequest request) => $"rate:{request.LimitId}:{request.UserId}";

    private static long GetCurrentWindow()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return now / WindowSizeSeconds;
    }

    private static TimeSpan GetTimeToNextWindow()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var currentWindow = now / WindowSizeSeconds;
        var nextWindowStart = (currentWindow + 1) * WindowSizeSeconds;
        var secondsToNext = nextWindowStart - now;
        return TimeSpan.FromSeconds(secondsToNext);
    }
}
