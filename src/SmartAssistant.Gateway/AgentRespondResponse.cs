namespace SmartAssistant.Gateway;

public record AgentRespondResponse(string SessionId, string ReplyText, ToolCall? ToolCall, string Source);
