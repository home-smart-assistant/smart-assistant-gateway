using System;

namespace SmartAssistant.Gateway;

public record SessionState(string SessionId, string? DeviceId, DateTimeOffset CreatedAt)
{
	public DateTimeOffset LastTurnAt { get; init; }

	public SessionState(string SessionId, string? DeviceId, DateTimeOffset CreatedAt)
	{
		LastTurnAt = CreatedAt;
		base._002Ector();
	}
}
