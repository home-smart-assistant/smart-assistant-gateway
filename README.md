# smart_assistant_gateway

## 技术栈
- .NET 8 / ASP.NET Core Minimal API
- Swagger/OpenAPI
- HttpClient 下游聚合

## 架构定位
- 统一接入层：承接 Android/其他客户端请求
- 下游编排层：转发到 Agent、HA Bridge、Wake Coordinator
- 兼容层：提供 Android 兼容接口（`/api/assistant/*`）

详细说明见：`docs/ARCHITECTURE.md`

## 关键配置
配置文件：`src/SmartAssistant.Gateway/appsettings.json`

- `Services:AgentBaseUrl`
- `Services:HomeAssistantBridgeBaseUrl`
- `Services:WakeCoordinatorBaseUrl`

环境变量覆盖：
- `Services__AgentBaseUrl`
- `Services__HomeAssistantBridgeBaseUrl`
- `Services__WakeCoordinatorBaseUrl`

## 启动
```powershell
dotnet run --project src/SmartAssistant.Gateway/SmartAssistant.Gateway.csproj --urls http://0.0.0.0:8080
```

## 接口文档
- OpenAPI：`docs/openapi/gateway.openapi.yaml`
