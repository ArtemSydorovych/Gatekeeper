namespace Gatekeeper.Core.Models;

public record CheckRequest(string UserId, string Route, string LimitId);
