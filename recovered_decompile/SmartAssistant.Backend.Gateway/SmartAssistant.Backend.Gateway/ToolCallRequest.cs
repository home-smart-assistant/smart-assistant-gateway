using System.Collections.Generic;

namespace SmartAssistant.Backend.Gateway;

public record ToolCallRequest(string ToolName, Dictionary<string, object>? Arguments, string? TraceId);
