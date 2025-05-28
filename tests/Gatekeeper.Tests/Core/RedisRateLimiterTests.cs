using Gatekeeper.Core.Interfaces;
using Gatekeeper.Core.Models;
using Gatekeeper.Infrastructure.Redis;
using Gatekeeper.Tests.ClassData;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Gatekeeper.Tests.Core;

public class RedisRateLimiterTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisRateLimiter>> _mockLogger;
    private readonly IRateLimiter _rateLimiter;

    public RedisRateLimiterTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisRateLimiter>>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                  .Returns(_mockDatabase.Object);

        _rateLimiter = new RedisRateLimiter(_mockRedis.Object, _mockLogger.Object);
    }

    [Theory]
    [ClassData(typeof(ValidCheckRequestTestData))]
    public async Task CheckAsync_Should_ReturnAllowed_When_KeyDoesNotExist(CheckRequest request)
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                     .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _rateLimiter.CheckAsync(request);

        // Assert
        result.Allowed.ShouldBeTrue();
        result.Remaining.ShouldBe(10);
        result.ResetIn.ShouldBeGreaterThan(TimeSpan.Zero);
        result.ResetIn.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(60));
    }

    [Theory]
    [ClassData(typeof(RateLimitScenarioTestData))]
    public async Task CheckAsync_Should_ReturnCorrectResult_When_CountExists(
        CheckRequest request, int currentCount, bool expectedAllowed, int expectedRemaining)
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                     .ReturnsAsync(new RedisValue(currentCount.ToString()));

        // Act
        var result = await _rateLimiter.CheckAsync(request);

        // Assert
        result.Allowed.ShouldBe(expectedAllowed);
        result.Remaining.ShouldBe(expectedRemaining);
        result.ResetIn.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Theory]
    [ClassData(typeof(RedisExceptionTestData))]
    public async Task CheckAsync_Should_ReturnFallbackAllowed_When_RedisThrowsException(
        CheckRequest request, Exception exception, bool expectedAllowed, int expectedRemaining, TimeSpan expectedResetIn)
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                     .ThrowsAsync(exception);

        // Act
        var result = await _rateLimiter.CheckAsync(request);

        // Assert
        result.Allowed.ShouldBe(expectedAllowed);
        result.Remaining.ShouldBe(expectedRemaining);
        result.ResetIn.ShouldBe(expectedResetIn);
    }

    [Theory]
    [ClassData(typeof(ConsumeScriptTestData))]
    public async Task ConsumeAsync_Should_ReturnCorrectResult_When_ScriptExecutes(
        CheckRequest request, RedisValue[] scriptResult, bool expectedAllowed, int expectedRemaining)
    {
        // Arrange
        var redisResult = RedisResult.Create(scriptResult);
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisResult);

        // Act
        var result = await _rateLimiter.ConsumeAsync(request);

        // Assert
        result.Allowed.ShouldBe(expectedAllowed);
        result.Remaining.ShouldBe(expectedRemaining);
        result.ResetIn.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Theory]
    [ClassData(typeof(ConsumeExceptionTestData))]
    public async Task ConsumeAsync_Should_ReturnFallbackNotAllowed_When_RedisThrowsException(
        CheckRequest request, Exception exception, bool expectedAllowed, int expectedRemaining, TimeSpan expectedResetIn)
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _rateLimiter.ConsumeAsync(request);

        // Assert
        result.Allowed.ShouldBe(expectedAllowed);
        result.Remaining.ShouldBe(expectedRemaining);
        result.ResetIn.ShouldBe(expectedResetIn);
    }

    [Theory]
    [ClassData(typeof(RedisKeyTestData))]
    public async Task CheckAsync_Should_UseCorrectRedisKey_When_Called(CheckRequest request, string expectedKeyPrefix)
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                     .ReturnsAsync(RedisValue.Null);

        // Act
        await _rateLimiter.CheckAsync(request);

        // Assert
        _mockDatabase.Verify(db => db.StringGetAsync(
            It.Is<RedisKey>(key => key.ToString().StartsWith(expectedKeyPrefix)),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_RedisIsNull()
    {
        // Act & Assert
        var action = () => new RedisRateLimiter(null!, _mockLogger.Object);
        action.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("redis");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Act & Assert
        var action = () => new RedisRateLimiter(_mockRedis.Object, null!);
        action.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("logger");
    }
}
