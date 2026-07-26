#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../deploy"
docker compose --env-file .env pull postgres
docker compose --env-file .env build --pull
docker compose --env-file .env up -d --remove-orphans
