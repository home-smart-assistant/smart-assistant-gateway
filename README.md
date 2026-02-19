# smart_assistant_gateway

Gateway API service (recovered from local build artifacts after ransomware incident).

## Exposed endpoints
- `GET /health`
- `POST /session/start`
- `POST /turn/text`
- `POST /tool/call`
- `GET/WS /turn/stream`

## Downstream services
- Agent: `Services:AgentBaseUrl` (default `http://localhost:8091`)
- HA Bridge: `Services:HomeAssistantBridgeBaseUrl` (default `http://localhost:8092`)

## Local run
```bash
dotnet run --project src/SmartAssistant.Gateway/SmartAssistant.Gateway.csproj
```

## Notes
- Source is reconstructed from compiled `dll` and may differ from pre-incident formatting.
