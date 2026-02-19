using System.Collections.Generic;

namespace SmartAssistant.Gateway;

public record TurnStreamMessage(string? SessionId, string Text, Dictionary<string, string>? Metadata);
