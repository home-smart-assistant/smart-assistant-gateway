using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class WakeClaimResponse
{
	[JsonPropertyName("granted")]
	public bool Granted { get; init; }

	[JsonPropertyName("home_id")]
	public string HomeId { get; init; } = string.Empty;

	[JsonPropertyName("device_id")]
	public string DeviceId { get; init; } = string.Empty;

	[JsonPropertyName("wake_token")]
	public string? WakeToken { get; init; }

	[JsonPropertyName("owner_device_id")]
	public string? OwnerDeviceId { get; init; }

	[JsonPropertyName("wake_id")]
	public string? WakeId { get; init; }

	[JsonPropertyName("reason")]
	public string Reason { get; init; } = string.Empty;

	[JsonPropertyName("expires_in_ms")]
	public int ExpiresInMs { get; init; }
}
