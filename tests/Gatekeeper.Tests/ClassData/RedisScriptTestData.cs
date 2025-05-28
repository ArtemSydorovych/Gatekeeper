using Gatekeeper.Core.Models;
using StackExchange.Redis;

namespace Gatekeeper.Tests.ClassData;

public class ConsumeScriptTestData : TheoryData<CheckRequest, RedisValue[], bool, int>
{
    public ConsumeScriptTestData()
    {
        var request = new CheckRequest("user123", "/api/orders", "ORDERS-PER-MINUTE");

        // scriptResult, expectedAllowed, expectedRemaining
        Add(request, new RedisValue[] { 1, 9, 1 }, true, 9);   // First request allowed
        Add(request, new RedisValue[] { 1, 7, 3 }, true, 7);   // Mid-window request allowed
        Add(request, new RedisValue[] { 1, 0, 10 }, true, 0);  // Last allowed request
        Add(request, new RedisValue[] { 0, 0, 10 }, false, 0); // Request denied - limit reached
        Add(request, new RedisValue[] { 0, 0, 15 }, false, 0); // Request denied - over limit
    }
}

public class RedisExceptionTestData : TheoryData<CheckRequest, Exception, bool, int, TimeSpan>
{
    public RedisExceptionTestData()
    {
        var request = new CheckRequest("user123", "/api/orders", "ORDERS-PER-MINUTE");

        // exception, expectedAllowedForCheck, expectedRemainingForCheck, expectedResetIn
        Add(request, new RedisException("Connection failed"), true, 10, TimeSpan.FromSeconds(60));
        Add(request, new TimeoutException("Redis timeout"), true, 10, TimeSpan.FromSeconds(60));
        Add(request, new RedisConnectionException(ConnectionFailureType.InternalFailure, "Internal error"), true, 10, TimeSpan.FromSeconds(60));
    }
}

public class ConsumeExceptionTestData : TheoryData<CheckRequest, Exception, bool, int, TimeSpan>
{
    public ConsumeExceptionTestData()
    {
        var request = new CheckRequest("user123", "/api/orders", "ORDERS-PER-MINUTE");

        // exception, expectedAllowedForConsume, expectedRemainingForConsume, expectedResetIn
        Add(request, new RedisException("Connection failed"), false, 0, TimeSpan.FromSeconds(60));
        Add(request, new TimeoutException("Redis timeout"), false, 0, TimeSpan.FromSeconds(60));
        Add(request, new RedisConnectionException(ConnectionFailureType.InternalFailure, "Internal error"), false, 0, TimeSpan.FromSeconds(60));
    }
}
