using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartAssistant.Gateway;

public sealed class AgentRespondRequest
{
	[JsonPropertyName("session_id")]
	public string SessionId { get; }

	[JsonPropertyName("text")]
	public string Text { get; }

	[JsonPropertyName("metadata")]
	public Dictionary<string, string> Metadata { get; }

	public AgentRespondRequest(string sessionId, string text, Dictionary<string, string>? metadata)
	{
		SessionId = sessionId;
		Text = text;
		Metadata = metadata ?? new Dictionary<string, string>();
	}
}
