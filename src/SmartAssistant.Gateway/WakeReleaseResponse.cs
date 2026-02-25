using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class WakeReleaseResponse
{
	[JsonPropertyName("released")]
	public bool Released { get; init; }

	[JsonPropertyName("reason")]
	public string Reason { get; init; } = string.Empty;

	[JsonPropertyName("owner_device_id")]
	public string? OwnerDeviceId { get; init; }
}
