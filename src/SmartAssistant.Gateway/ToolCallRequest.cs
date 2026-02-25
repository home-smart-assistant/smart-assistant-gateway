using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class ToolCallRequest
{
	[JsonPropertyName("tool_name")]
	public string ToolName { get; init; } = string.Empty;

	[JsonPropertyName("arguments")]
	public Dictionary<string, object>? Arguments { get; init; }

	[JsonPropertyName("trace_id")]
	public string? TraceId { get; init; }
}
