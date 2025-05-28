using Gatekeeper.Core.Models;

namespace Gatekeeper.Tests.ClassData;

public class ValidCheckRequestTestData : TheoryData<CheckRequest>
{
    public ValidCheckRequestTestData()
    {
        Add(new CheckRequest("user123", "/api/orders", "ORDERS-PER-MINUTE"));
        Add(new CheckRequest("user456", "/api/products", "PRODUCTS-PER-MINUTE"));
        Add(new CheckRequest("user789", "/api/analytics", "ANALYTICS-PER-MINUTE"));
        Add(new CheckRequest("test-user", "/api/test", "TEST-LIMIT"));
    }
}

public class RedisKeyTestData : TheoryData<CheckRequest, string>
{
    public RedisKeyTestData()
    {
        Add(new CheckRequest("user123", "/api/orders", "ORDERS-PER-MINUTE"), "rate:ORDERS-PER-MINUTE:user123");
        Add(new CheckRequest("user456", "/api/products", "PRODUCTS-LIMIT"), "rate:PRODUCTS-LIMIT:user456");
        Add(new CheckRequest("admin", "/api/system", "SYSTEM-CALLS"), "rate:SYSTEM-CALLS:admin");
    }
}

public class RateLimitScenarioTestData : TheoryData<CheckRequest, int, bool, int>
{
    public RateLimitScenarioTestData()
    {
        var request = new CheckRequest("user123", "/api/orders", "ORDERS-PER-MINUTE");

        // currentCount, expectedAllowed, expectedRemaining
        Add(request, 0, true, 10);   // No requests yet
        Add(request, 5, true, 5);    // Below limit
        Add(request, 9, true, 1);    // At limit boundary
        Add(request, 10, false, 0);  // At limit
        Add(request, 15, false, 0);  // Over limit
    }
}