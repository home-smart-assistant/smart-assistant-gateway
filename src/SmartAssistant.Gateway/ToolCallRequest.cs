using System.Collections.Generic;

namespace SmartAssistant.Gateway;

public record ToolCallRequest(string ToolName, Dictionary<string, object>? Arguments, string? TraceId);
