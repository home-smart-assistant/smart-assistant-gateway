using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class TurnTextRequest
{
	[JsonPropertyName("session_id")]
	public string? SessionId { get; init; }

	[JsonPropertyName("text")]
	public string Text { get; init; } = string.Empty;

	[JsonPropertyName("device_id")]
	public string? DeviceId { get; init; }

	[JsonPropertyName("home_id")]
	public string? HomeId { get; init; }

	[JsonPropertyName("wake_token")]
	public string? WakeToken { get; init; }

	[JsonPropertyName("metadata")]
	public Dictionary<string, string>? Metadata { get; init; }
}
