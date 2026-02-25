using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class AgentRespondResponse
{
	[JsonPropertyName("session_id")]
	public string SessionId { get; init; } = string.Empty;

	[JsonPropertyName("reply_text")]
	public string ReplyText { get; init; } = string.Empty;

	[JsonPropertyName("tool_call")]
	public ToolCall? ToolCall { get; init; }

	[JsonPropertyName("source")]
	public string Source { get; init; } = "rule_chat";
}
