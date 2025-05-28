using System.Net;
using System.Text;
using System.Text.Json;
using Gatekeeper.Core.Models;
using Gatekeeper.Tests.ClassData;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Gatekeeper.Tests.Api;

public class RateLimitEndpointsTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        // Start Redis container
        await _redisContainer.StartAsync();

        // Create test web application factory
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing Redis registration
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add test Redis connection
                    services.AddSingleton<IConnectionMultiplexer>(_ =>
                        ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString()));
                });
            });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory!.DisposeAsync()!;
        await _redisContainer.DisposeAsync();
    }

    [Theory]
    [ClassData(typeof(ValidCheckRequestTestData))]
    public async Task Check_Should_ReturnAllowedTrue_When_ValidRequest(CheckRequest request)
    {
        // Arrange
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client!.PostAsync("/check", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<RateLimitResult>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        result.ShouldNotBeNull();
        result.Allowed.ShouldBeTrue();
        result.Remaining.ShouldBe(10);
        result.ResetIn.ShouldBeGreaterThan(TimeSpan.Zero);
        result.ResetIn.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(60));
    }

    [Theory]
    [ClassData(typeof(ConsumeEndpointTestData))]
    public async Task Consume_Should_DecrementRemainingCount_When_ValidRequest(CheckRequest request)
    {
        // Arrange
        var json = JsonSerializer.Serialize(request);
        var content1 = new StringContent(json, Encoding.UTF8, "application/json");
        var content2 = new StringContent(json, Encoding.UTF8, "application/json");

        // Act - First consume request
        var response1 = await _client!.PostAsync("/consume", content1);

        // Assert first response
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var responseContent1 = await response1.Content.ReadAsStringAsync();
        var result1 = JsonSerializer.Deserialize<RateLimitResult>(responseContent1, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        result1.ShouldNotBeNull();
        result1.Allowed.ShouldBeTrue();
        result1.Remaining.ShouldBe(9); // Should be decremented from 10 to 9

        // Act - Second consume request
        var response2 = await _client.PostAsync("/consume", content2);

        // Assert second response
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        var responseContent2 = await response2.Content.ReadAsStringAsync();
        var result2 = JsonSerializer.Deserialize<RateLimitResult>(responseContent2, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        result2.ShouldNotBeNull();
        result2.Allowed.ShouldBeTrue();
        result2.Remaining.ShouldBe(8); // Should be decremented from 9 to 8
    }

    [Theory]
    [ClassData(typeof(ValidCheckRequestTestData))]
    public async Task Check_Should_NotDecrementCount_When_CalledMultipleTimes(CheckRequest request)
    {
        // Arrange
        var json = JsonSerializer.Serialize(request);

        // Act - Check multiple times
        var checkContent1 = new StringContent(json, Encoding.UTF8, "application/json");
        var checkResponse1 = await _client!.PostAsync("/check", checkContent1);

        var checkContent2 = new StringContent(json, Encoding.UTF8, "application/json");
        var checkResponse2 = await _client.PostAsync("/check", checkContent2);

        // Assert both check responses should have same remaining count
        checkResponse1.StatusCode.ShouldBe(HttpStatusCode.OK);
        checkResponse2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseContent1 = await checkResponse1.Content.ReadAsStringAsync();
        var result1 = JsonSerializer.Deserialize<RateLimitResult>(responseContent1, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var responseContent2 = await checkResponse2.Content.ReadAsStringAsync();
        var result2 = JsonSerializer.Deserialize<RateLimitResult>(responseContent2, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        result1!.Remaining.ShouldBe(10);
        result2!.Remaining.ShouldBe(10); // Should not have decremented
    }

    [Fact]
    public async Task Consume_Should_ReturnNotAllowed_When_LimitExceeded()
    {
        // Arrange
        var request = new CheckRequest("rate-limit-test-user", "/api/heavy", "HEAVY-PER-MINUTE");
        var json = JsonSerializer.Serialize(request);

        // Act - Consume 10 requests (the limit)
        var tasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < 10; i++)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            tasks.Add(_client!.PostAsync("/consume", content));
        }

        var responses = await Task.WhenAll(tasks);

        // All 10 requests should be allowed
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Act - Try 11th request (should be denied)
        var extraContent = new StringContent(json, Encoding.UTF8, "application/json");
        var extraResponse = await _client!.PostAsync("/consume", extraContent);

        // Assert
        extraResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var extraResponseContent = await extraResponse.Content.ReadAsStringAsync();
        var extraResult = JsonSerializer.Deserialize<RateLimitResult>(extraResponseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        extraResult.ShouldNotBeNull();
        extraResult.Allowed.ShouldBeFalse();
        extraResult.Remaining.ShouldBe(0);
    }

    [Theory]
    [ClassData(typeof(UserIsolationTestData))]
    public async Task RateLimiter_Should_IsolateDifferentUsers_When_SameLimitId(CheckRequest user1Request, CheckRequest user2Request)
    {
        // Arrange
        var user1Json = JsonSerializer.Serialize(user1Request);
        var user2Json = JsonSerializer.Serialize(user2Request);

        // Act - Consume for user1
        var user1Content = new StringContent(user1Json, Encoding.UTF8, "application/json");
        var user1Response = await _client!.PostAsync("/consume", user1Content);

        // Act - Check for user2 (should still have full limit)
        var user2Content = new StringContent(user2Json, Encoding.UTF8, "application/json");
        var user2Response = await _client.PostAsync("/check", user2Content);

        // Assert
        user1Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        user2Response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var user1ResponseContent = await user1Response.Content.ReadAsStringAsync();
        var user1Result = JsonSerializer.Deserialize<RateLimitResult>(user1ResponseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var user2ResponseContent = await user2Response.Content.ReadAsStringAsync();
        var user2Result = JsonSerializer.Deserialize<RateLimitResult>(user2ResponseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        user1Result!.Remaining.ShouldBe(9); // user1 consumed 1 request
        user2Result!.Remaining.ShouldBe(10); // user2 hasn't consumed any
    }

    [Theory]
    [ClassData(typeof(LimitIdIsolationTestData))]
    public async Task RateLimiter_Should_IsolateDifferentLimitIds_When_SameUser(CheckRequest request1, CheckRequest request2)
    {
        // Arrange
        var json1 = JsonSerializer.Serialize(request1);
        var json2 = JsonSerializer.Serialize(request2);

        // Act - Consume for first limit
        var content1 = new StringContent(json1, Encoding.UTF8, "application/json");
        var response1 = await _client!.PostAsync("/consume", content1);

        // Act - Check for second limit (should still have full limit)
        var content2 = new StringContent(json2, Encoding.UTF8, "application/json");
        var response2 = await _client.PostAsync("/check", content2);

        // Assert
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseContent1 = await response1.Content.ReadAsStringAsync();
        var result1 = JsonSerializer.Deserialize<RateLimitResult>(responseContent1, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var responseContent2 = await response2.Content.ReadAsStringAsync();
        var result2 = JsonSerializer.Deserialize<RateLimitResult>(responseContent2, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        result1!.Remaining.ShouldBe(9); // First limit consumed 1 request
        result2!.Remaining.ShouldBe(10); // Second limit hasn't consumed any
    }
}
