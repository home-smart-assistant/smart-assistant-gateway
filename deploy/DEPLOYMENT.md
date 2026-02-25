# Smart Assistant 部署说明

`smart_assistant_gateway/deploy/` 目录部署编排（Docker Compose）。

## 目标
- 一台机器拉起首版后端：Gateway + Agent + HA Bridge + Redis + PostgreSQL
- 支持 Android 面板直接接入 `Gateway:8080`

## 目录
- `smart_assistant_gateway/deploy/docker-compose.yml`：主编排
- `smart_assistant_gateway/deploy/.env.example`：环境变量样例
- `smart_assistant_gateway/deploy/health-check.ps1`：启动后快速检查

## 启动步骤
```bash
cd smart_assistant_gateway/deploy
cp .env.example .env
# 按需修改 HA_TOKEN、端口等
docker compose up -d --build
```

## 核心端口
- `8080` Gateway
- `8091` Agent
- `8092` HA Bridge
- `6379` Redis
- `5432` PostgreSQL

## 关闭
```bash
cd smart_assistant_gateway/deploy
docker compose down
```
