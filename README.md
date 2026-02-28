# smart_assistant_gateway

## 技术栈
- .NET 8 / ASP.NET Core Minimal API
- Swagger / OpenAPI
- `HttpClientFactory`（下游 Agent、HA Bridge）
- 内置 `WakeArbitrationService`

## 职责
- Android/客户端统一入口
- 转发对话请求到 Agent
- 转发工具调用到 HA Bridge
- 管理多设备唤醒仲裁

## 关键配置
配置文件：`src/SmartAssistant.Gateway/appsettings.json`

- `Services:AgentBaseUrl`
- `Services:HomeAssistantBridgeBaseUrl`
- `WakeArbitration:LockTtlMs`
- `TextEncoding:Strict`（默认 `true`，不可修复乱码直接 400）

环境变量覆盖：
- `Services__AgentBaseUrl`
- `Services__HomeAssistantBridgeBaseUrl`
- `WakeArbitration__LockTtlMs`
- `TextEncoding__Strict`

## 编码策略
- 所有源码与文档使用 UTF-8。
- `turn/text`、`api/assistant/text-turn`、`api/assistant/turn`、`turn/stream`、`tool/call` 在调用下游前统一做文本规范化。
- 文本无法可靠修复时，返回 `400`，错误体包含 `error_code=invalid_text_encoding`、`field`、`sample`。

## 本地运行
```powershell
dotnet run --project src/SmartAssistant.Gateway/SmartAssistant.Gateway.csproj --urls http://0.0.0.0:8080
```

## 接口文档
- OpenAPI：`docs/openapi/gateway.openapi.yaml`
- 架构说明：`docs/ARCHITECTURE.md`
