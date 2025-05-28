namespace Gatekeeper.Core.Models;

public record RateLimitResult(bool Allowed, int Remaining, TimeSpan ResetIn);
