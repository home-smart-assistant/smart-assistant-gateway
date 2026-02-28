# smart_assistant_gateway 架构说明

## 技术栈
- .NET 8
- ASP.NET Core Minimal API
- `HttpClientFactory`（调用 Agent / HA Bridge）
- Swagger / OpenAPI
- WebSocket（文本流式会话）

## 架构定位
- Android 客户端统一入口
- 编排 Agent 与 HA Bridge 两个下游服务
- 内置多设备唤醒仲裁（`WakeArbitrationService`）

## 请求链路
- 文本对话：`/turn/text` 或 `/api/assistant/text-turn` -> Agent `/v1/agent/respond`
- Android 对话：`/api/assistant/turn`（multipart，当前用于网关调试桥接）
- 工具调用：`/tool/call` -> HA Bridge `/v1/tools/call`
- 唤醒仲裁：`/v1/wake/*` -> Gateway 内部仲裁服务
- 健康检查：`/health` 聚合下游连通性与仲裁状态

## 主要接口
- `GET /health`
- `POST /session/start`
- `POST /turn/text`
- `POST /api/assistant/text-turn`
- `POST /api/assistant/turn`
- `POST /tool/call`
- `GET/WS /turn/stream`
- `POST /v1/wake/claim`
- `POST /v1/wake/heartbeat`
- `POST /v1/wake/validate`
- `POST /v1/wake/release`

完整契约见 `docs/openapi/gateway.openapi.yaml`。

## 关键配置
配置文件：`src/SmartAssistant.Gateway/appsettings.json`

- `Services:AgentBaseUrl`，默认 `http://localhost:8091`
- `Services:HomeAssistantBridgeBaseUrl`，默认 `http://localhost:8092`
- `WakeArbitration:LockTtlMs`，默认 `8000`

环境变量覆盖：
- `Services__AgentBaseUrl`
- `Services__HomeAssistantBridgeBaseUrl`
- `WakeArbitration__LockTtlMs`

监听地址配置：
- 启动参数可指定 `--urls http://0.0.0.0:8080`
- 也可在 `src/SmartAssistant.Gateway/Properties/launchSettings.json` 配置

## 本地运行
```powershell
dotnet run --project src/SmartAssistant.Gateway/SmartAssistant.Gateway.csproj --urls http://0.0.0.0:8080
```

验证：
- `GET http://<gateway-ip>:8080/health`
