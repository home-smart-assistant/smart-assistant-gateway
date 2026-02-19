namespace SmartAssistant.Backend.Gateway;

public record AgentRespondResponse(string SessionId, string ReplyText, ToolCall? ToolCall, string Source);
