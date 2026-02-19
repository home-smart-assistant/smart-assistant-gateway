namespace SmartAssistant.Backend.Gateway;

public record TurnTextResponse(string SessionId, string ReplyText, ToolCall? ToolCall, string Source);
