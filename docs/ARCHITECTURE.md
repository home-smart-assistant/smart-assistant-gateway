# smart_assistant_gateway 架构说明

## 核心组件
- API 网关：统一接收文本/语音调试请求
- 下游路由：Agent（对话与规划）、HA Bridge（工具执行）
- 唤醒仲裁：`WakeArbitrationService`（多设备 claim/heartbeat/release）
- WebSocket 流式会话：`/turn/stream`

## 请求链路
1. 文本对话：`/turn/text` 或 `/api/assistant/text-turn` -> Agent `/v1/agent/respond`
2. Android multipart 调试：`/api/assistant/turn` -> 转换为文本请求 -> Agent
3. 工具调用：`/tool/call` -> HA Bridge `/v1/tools/call`
4. 唤醒仲裁：`/v1/wake/*` -> Gateway 内部服务

## 编码与转码
- 统一 UTF-8 源码与文档。
- 在 Gateway 入站和下游转发前进行文本规范化（支持常见 mojibake 修复）。
- 无法可靠修复时拒绝请求，返回：
  - `error_code=invalid_text_encoding`
  - `field`
  - `sample`
- 该行为由 `TextEncoding:Strict`（或 `TextEncoding__Strict`）控制，默认 `true`。

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
