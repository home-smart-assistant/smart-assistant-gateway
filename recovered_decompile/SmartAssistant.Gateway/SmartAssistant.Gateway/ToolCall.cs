using System.Collections.Generic;

namespace SmartAssistant.Gateway;

public record ToolCall(string ToolName, Dictionary<string, object>? Arguments);
