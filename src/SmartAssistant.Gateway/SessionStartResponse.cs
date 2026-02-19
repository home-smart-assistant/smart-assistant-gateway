using System;

namespace SmartAssistant.Gateway;

public record SessionStartResponse(string SessionId, DateTimeOffset CreatedAt, string Mode);
