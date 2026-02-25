using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class WakeValidateResponse
{
	[JsonPropertyName("valid")]
	public bool Valid { get; init; }

	[JsonPropertyName("home_id")]
	public string HomeId { get; init; } = string.Empty;

	[JsonPropertyName("owner_device_id")]
	public string? OwnerDeviceId { get; init; }

	[JsonPropertyName("expires_in_ms")]
	public int ExpiresInMs { get; init; }
}
