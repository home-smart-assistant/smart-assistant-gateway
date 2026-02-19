namespace SmartAssistant.Backend.Gateway;

public record ToolCallResult(bool Success, string Message, object? Data, string? TraceId);
