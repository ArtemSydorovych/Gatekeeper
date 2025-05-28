using Gatekeeper.Core.Models;

namespace Gatekeeper.Core.Interfaces;

public interface IRateLimiter
{
    Task<RateLimitResult> CheckAsync(CheckRequest request, CancellationToken cancellationToken = default);
    Task<RateLimitResult> ConsumeAsync(CheckRequest request, CancellationToken cancellationToken = default);
}