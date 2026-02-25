# smart_assistant_gateway 架构与配置

## 技术栈
- .NET 8
- ASP.NET Core Minimal API
- `HttpClientFactory`（下游服务调用）
- Swagger/OpenAPI
- WebSocket（流式对话入口）

## 服务定位
- 对客户端提供统一入口
- 对下游（Agent / HA Bridge / Wake Coordinator）做聚合与转发
- 负责会话入口、文本轮次、Android 兼容接口、唤醒仲裁代理

## 请求链路
- 文本对话：`/turn/text` 或 `/api/assistant/text-turn` -> Agent `/v1/agent/respond`
- Android 音频兼容：`/api/assistant/turn`（当前为调试桥接，映射到文本轮次）
- 工具调用：`/tool/call` -> HA Bridge `/v1/tools/call`
- 唤醒仲裁：`/v1/wake/*` -> Wake Coordinator `/v1/wake/*`
- 健康探测：`/health` 汇总下游可达性

## 对外接口
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

接口契约：`docs/openapi/gateway.openapi.yaml`

## 关键配置
配置文件：`src/SmartAssistant.Gateway/appsettings.json`

- `Services:AgentBaseUrl`（默认 `http://localhost:8091`）
- `Services:HomeAssistantBridgeBaseUrl`（默认 `http://localhost:8092`）
- `Services:WakeCoordinatorBaseUrl`（默认 `http://localhost:8093`）

环境变量覆盖：
- `Services__AgentBaseUrl`
- `Services__HomeAssistantBridgeBaseUrl`
- `Services__WakeCoordinatorBaseUrl`

监听地址：
- 开发默认来自 `src/SmartAssistant.Gateway/Properties/launchSettings.json`
- 建议显式使用 `http://0.0.0.0:8080`，便于手机/模拟器访问

## 本地调试
```powershell
dotnet run --project src/SmartAssistant.Gateway/SmartAssistant.Gateway.csproj --urls http://0.0.0.0:8080
```

验证：
- `GET http://<gateway-ip>:8080/health`
