# Smart Assistant 架构

## 1) 项目布局

| 项目 | 职责 | 技术栈 | 主入口 |
|---|---|---|---|
| `smart_assistant_android` | 语音面板客户端（离线 KWS 唤醒、录音、播放、后台模式） | Android/Kotlin | `smart_assistant_android/app/src/main/java/com/example/smart_assistant_android/MainActivity.kt` |
| `smart_assistant_gateway` | 边缘 API 网关、会话入口、Android 兼容接口、下游代理 | ASP.NET Core (.NET 8) | `smart_assistant_gateway/src/SmartAssistant.Gateway/Program.cs` |
| `smart_assistant_agent` | Agent 编排、规则意图识别、可选工具自动执行 | FastAPI/Python | `smart_assistant_agent/app/main.py` |
| `smart_assistant_ha_bridge` | Home Assistant 桥接层（白名单 + Mock/真实 HA） | FastAPI/Python | `smart_assistant_ha_bridge/app/main.py` |
| `smart_assistant_wake_coordinator` | 多设备唤醒集中仲裁（claim/heartbeat/release） | FastAPI/Python | `smart_assistant_wake_coordinator/app/main.py` |
| `smart_assistant_gateway/deploy` | 一体化部署编排与健康检查 | Docker Compose + PowerShell | `smart_assistant_gateway/deploy/docker-compose.yml` |

## 2) 运行拓扑

```text
Android App
    -> Gateway (8080)
        -> Agent (8091)
            -> HA Bridge (8092)
                -> Home Assistant
        -> Wake Coordinator (8093)

Infra:
- Redis (6379)
- PostgreSQL (5432)
```

Compose 连接关系定义在 `smart_assistant_gateway/deploy/docker-compose.yml`。

## 3) 服务边界与 API

### Gateway
- 健康检查：`GET /health`
- 会话创建：`POST /session/start`
- 标准文本轮次：`POST /turn/text`
- Android 文本兼容：`POST /api/assistant/text-turn`
- Android 音频兼容：`POST /api/assistant/turn`
- WebSocket 轮次：`GET/WS /turn/stream`
- 工具代理：`POST /tool/call`

唤醒仲裁聚合接口：
- `POST /v1/wake/claim`
- `POST /v1/wake/heartbeat`
- `POST /v1/wake/validate`
- `POST /v1/wake/release`

实现文件：`smart_assistant_gateway/src/SmartAssistant.Gateway/Program.cs`

### Wake Coordinator
- `GET /health`
- `POST /v1/wake/claim`
- `POST /v1/wake/heartbeat`
- `POST /v1/wake/validate`
- `POST /v1/wake/release`

核心行为：
- 按 `home_id` 保证同一时刻仅一个设备持有唤醒 token
- token TTL + heartbeat 续期
- 支持 Redis（优先）/内存（兜底）锁存储

实现文件：`smart_assistant_wake_coordinator/app/main.py`

### Agent / HA Bridge
- Agent：`smart_assistant_agent/app/main.py`
- HA Bridge：`smart_assistant_ha_bridge/app/main.py`

## 4) Android 架构与对接

### 主要模块
- `MainActivity`：前台 UI 模式流程与唤醒链路
- `AssistantBackgroundService`：后台常驻模式流程
- `WakeWord`：本地 sherpa-onnx 离线唤醒监听（仅有限词库）
- `WakeWordKeywordCatalog`：离线词库读取与唤醒词校验
- `ServerApi`：统一网关接口调用
- `WakeWordPreferences`：本地唤醒词缓存
- `DeviceIdentity`：设备唯一标识生成与持久化

### 与 Gateway 调试对接
- Android 音频轮次调用：`/api/assistant/turn`
- Android 文本轮次调用：`/api/assistant/text-turn`
- 唤醒仲裁：`/v1/wake/claim`、`/v1/wake/release`

### 多设备防重响应机制
- 设备检测到唤醒后先向网关发起 `wake claim`
- 只有 claim 成功设备继续进入录音与轮次流程
- 回合结束后设备主动 `wake release`

### 唤醒词策略（当前）
- 仅支持模型内置有限唤醒词
- UI 设置时会校验，若有不支持词则拒绝保存并提示
- 不再依赖云端唤醒词配置服务

## 5) 部署与环境变量

Compose 服务与端口：
- Gateway `8080`
- Agent `8091`
- HA Bridge `8092`
- Wake Coordinator `8093`
- Redis `6379`
- PostgreSQL `5432`

部署文档：`smart_assistant_gateway/deploy/DEPLOYMENT.md`

关键变量：
- Gateway：
  - `Services__AgentBaseUrl`
  - `Services__HomeAssistantBridgeBaseUrl`
  - `Services__WakeCoordinatorBaseUrl`
- Wake Coordinator：
  - `WAKE_LOCK_TTL_MS`
  - `REDIS_URL`

## 6) 当前架构注意点

1. `POST /api/assistant/turn` 当前是网关调试桥接路径（将音频轮次映射为文本处理），并非完整 ASR 管道。
2. Wake Coordinator 当前实现为 MVP（可运行）；生产可用需要补充鉴权、审计、持久化与灰度发布。
3. 自定义唤醒词当前限定为离线词库内词项，超出词库的文本不会生效。
