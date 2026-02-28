# smart_assistant_gateway

## 技术栈
- .NET 8 / ASP.NET Core Minimal API
- Swagger / OpenAPI
- `HttpClientFactory`（下游 Agent、HA Bridge）
- 内置仲裁服务 `WakeArbitrationService`

## 架构定位
- 作为 Android 面板的统一接入网关
- 负责转发对话请求到 Agent
- 负责转发工具调用到 HA Bridge
- 负责多设备唤醒仲裁（已并入 Gateway，非独立服务）

详细说明见 `docs/ARCHITECTURE.md`。

## 关键配置
配置文件：`src/SmartAssistant.Gateway/appsettings.json`

- `Services:AgentBaseUrl`
- `Services:HomeAssistantBridgeBaseUrl`
- `WakeArbitration:LockTtlMs`

环境变量覆盖：
- `Services__AgentBaseUrl`
- `Services__HomeAssistantBridgeBaseUrl`
- `WakeArbitration__LockTtlMs`

## 本地运行
```powershell
dotnet run --project src/SmartAssistant.Gateway/SmartAssistant.Gateway.csproj --urls http://0.0.0.0:8080
```

## 接口文档
- OpenAPI：`docs/openapi/gateway.openapi.yaml`

## 接口说明（简版）
1. `POST /api/assistant/turn`
- 用途：Android 语音对话入口（multipart）。
- 行为：网关将请求转换后转发给 Agent。

2. `POST /api/assistant/text-turn`
- 用途：文本调试入口（不走音频上传）。

3. `POST /v1/wake/claim`
- 用途：多设备唤醒仲裁抢占响应权。

4. `POST /v1/wake/heartbeat`
- 用途：持有方心跳续租唤醒令牌。

5. `POST /v1/wake/release`
- 用途：当前响应设备主动释放唤醒令牌。

下游接口说明：
- Agent：`../smart_assistant_agent/docs/openapi/agent.openapi.yaml`
- HA Bridge：`../smart_assistant_ha_bridge/docs/API.md`
