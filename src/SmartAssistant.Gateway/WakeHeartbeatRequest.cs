using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class WakeHeartbeatRequest
{
	[JsonPropertyName("home_id")]
	public string HomeId { get; init; } = string.Empty;

	[JsonPropertyName("device_id")]
	public string DeviceId { get; init; } = string.Empty;

	[JsonPropertyName("wake_token")]
	public string WakeToken { get; init; } = string.Empty;
}
