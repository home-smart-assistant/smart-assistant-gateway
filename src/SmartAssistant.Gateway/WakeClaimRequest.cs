using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class WakeClaimRequest
{
	[JsonPropertyName("home_id")]
	public string HomeId { get; init; } = string.Empty;

	[JsonPropertyName("device_id")]
	public string DeviceId { get; init; } = string.Empty;

	[JsonPropertyName("wake_id")]
	public string? WakeId { get; init; }

	[JsonPropertyName("confidence")]
	public double? Confidence { get; init; }
}
