using Gatekeeper.Core.Models;

namespace Gatekeeper.Tests.ClassData;

public class ApiEndpointTestData : TheoryData<string, CheckRequest>
{
    public ApiEndpointTestData()
    {
        Add("/check", new CheckRequest("user123", "/api/orders", "ORDERS-PER-MINUTE"));
        Add("/consume", new CheckRequest("user456", "/api/products", "PRODUCTS-PER-MINUTE"));
        Add("/check", new CheckRequest("user789", "/api/analytics", "ANALYTICS-PER-MINUTE"));
        Add("/consume", new CheckRequest("test-user", "/api/test", "TEST-LIMIT"));
    }
}

public class ConsumeEndpointTestData : TheoryData<CheckRequest>
{
    public ConsumeEndpointTestData()
    {
        Add(new CheckRequest("user1", "/api/orders", "ORDERS-PER-MINUTE"));
        Add(new CheckRequest("user2", "/api/products", "PRODUCTS-PER-MINUTE"));
        Add(new CheckRequest("user3", "/api/analytics", "ANALYTICS-PER-MINUTE"));
    }
}

public class UserIsolationTestData : TheoryData<CheckRequest, CheckRequest>
{
    public UserIsolationTestData()
    {
        Add(
            new CheckRequest("user1", "/api/isolated", "ISOLATED-PER-MINUTE"),
            new CheckRequest("user2", "/api/isolated", "ISOLATED-PER-MINUTE")
        );
        Add(
            new CheckRequest("alice", "/api/shared", "SHARED-LIMIT"),
            new CheckRequest("bob", "/api/shared", "SHARED-LIMIT")
        );
    }
}

public class LimitIdIsolationTestData : TheoryData<CheckRequest, CheckRequest>
{
    public LimitIdIsolationTestData()
    {
        Add(
            new CheckRequest("shared-user", "/api/orders", "ORDERS-PER-MINUTE"),
            new CheckRequest("shared-user", "/api/products", "PRODUCTS-PER-MINUTE")
        );
        Add(
            new CheckRequest("test-user", "/api/read", "READ-LIMIT"),
            new CheckRequest("test-user", "/api/write", "WRITE-LIMIT")
        );
    }
}