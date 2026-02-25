using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class ToolCallResult
{
	[JsonPropertyName("success")]
	public bool Success { get; init; }

	[JsonPropertyName("message")]
	public string Message { get; init; } = string.Empty;

	[JsonPropertyName("data")]
	public object? Data { get; init; }

	[JsonPropertyName("trace_id")]
	public string? TraceId { get; init; }
}
