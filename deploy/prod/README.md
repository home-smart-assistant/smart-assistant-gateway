# Production Deployment (Builder 192.168.3.11 + Deploy 192.168.3.103)

This directory is the production deployment source of truth for:

- `smart_assistant_gateway`
- `smart_assistant_agent`
- `smart_assistant_ha_bridge`

## Topology

- Build server: `192.168.3.11` (WSL2 Ubuntu on Windows, self-hosted runner label: `builder-linux-11`)
- Deploy server: `192.168.3.103` (Linux, self-hosted runner label: `deploy-linux`)

Each CI/CD workflow:
1. builds/tests/pushes image on `builder-linux-11`
2. pulls and restarts only its own service on `deploy-linux`

## Deploy server layout (192.168.3.103)

- Compose dir: `/opt/smart-assistant`
- Compose file: `/opt/smart-assistant/docker-compose.yml`
- Runtime env: `/opt/smart-assistant/.env`

First-time setup:

```bash
sudo mkdir -p /opt/smart-assistant
cd /opt/smart-assistant
curl -fsSL https://raw.githubusercontent.com/home-smart-assistant/smart-assistant-gateway/main/deploy/prod/docker-compose.yml -o docker-compose.yml
curl -fsSL https://raw.githubusercontent.com/home-smart-assistant/smart-assistant-gateway/main/deploy/prod/.env.example -o .env
```

Then edit `.env` with real values (especially `HA_TOKEN` and HA entity mappings).

## GitHub configuration required

Repository variable (optional, in each repo):

- `DEPLOY_PATH` (default `/opt/smart-assistant`)

Secrets:

- No SSH secrets required.
- `GHCR_USERNAME` + `GHCR_TOKEN` only if your package visibility/policy requires explicit login.

## Runner prerequisites

On `192.168.3.11` (Windows builder):

- install WSL2 + Ubuntu
- register self-hosted runner inside Ubuntu with label `builder-linux-11`
- install Docker inside Ubuntu and ensure Linux image build/push works
- install Python 3.11 (agent/bridge tests)
- install .NET 8 SDK (gateway build)

On `192.168.3.103` (Linux deploy):

- register self-hosted runner with label `deploy-linux`
- install Docker Engine + Docker Compose plugin
- runner user must have permission to run `docker` and write `DEPLOY_PATH`

## Deploy behavior

Each repo deploys only its own service:

- gateway repo -> `smart_assistant_gateway`
- agent repo -> `smart_assistant_agent`
- ha_bridge repo -> `smart_assistant_ha_bridge`

Workflow updates `*_IMAGE_TAG` in server `.env` to current commit SHA, then executes:

```bash
docker compose pull <service>
docker compose up -d --no-deps <service>
```
