using System.Collections.Generic;

namespace SmartAssistant.Backend.Gateway;

public record AgentRespondRequest(string SessionId, string Text, Dictionary<string, string>? Metadata);
