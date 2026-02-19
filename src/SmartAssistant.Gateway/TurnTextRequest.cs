using System.Collections.Generic;

namespace SmartAssistant.Gateway;

public record TurnTextRequest(string? SessionId, string Text, string? DeviceId, Dictionary<string, string>? Metadata);
