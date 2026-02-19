using System;

namespace SmartAssistant.Backend.Gateway;

public record SessionStartResponse(string SessionId, DateTimeOffset CreatedAt, string Mode);
