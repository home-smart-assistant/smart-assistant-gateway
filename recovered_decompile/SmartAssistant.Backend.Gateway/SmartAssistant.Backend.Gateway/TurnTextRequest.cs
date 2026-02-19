using System.Collections.Generic;

namespace SmartAssistant.Backend.Gateway;

public record TurnTextRequest(string? SessionId, string Text, string? DeviceId, Dictionary<string, string>? Metadata);
