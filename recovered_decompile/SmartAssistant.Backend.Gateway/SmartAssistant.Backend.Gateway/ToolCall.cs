using System.Collections.Generic;

namespace SmartAssistant.Backend.Gateway;

public record ToolCall(string ToolName, Dictionary<string, object>? Arguments);
