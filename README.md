# smart_assistant_gateway

Gateway API service.

## Exposed endpoints
- `GET /health`
- `POST /session/start`
- `POST /turn/text`
- `POST /api/assistant/text-turn` (Android compatibility)
- `POST /api/assistant/turn` (Android compatibility, multipart)
- `POST /tool/call`
- `GET/WS /turn/stream`
- `POST /v1/wake/claim`
- `POST /v1/wake/heartbeat`
- `POST /v1/wake/validate`
- `POST /v1/wake/release`

## API contract
- `docs/openapi/gateway.openapi.yaml`

## Downstream services
- Agent: `Services:AgentBaseUrl` (default `http://localhost:8091`)
- HA Bridge: `Services:HomeAssistantBridgeBaseUrl` (default `http://localhost:8092`)
- Wake Coordinator: `Services:WakeCoordinatorBaseUrl` (default `http://localhost:8093`)

## Local run
```bash
dotnet run --project src/SmartAssistant.Gateway/SmartAssistant.Gateway.csproj
```

## Notes
- `/api/assistant/turn` currently routes audio multipart requests into text-turn debug bridge mode.
