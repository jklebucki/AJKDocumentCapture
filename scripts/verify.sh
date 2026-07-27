#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
source "$root/scripts/lib/dev-profile.sh"
cd "$root/deploy"
configure_dev_profile
docker compose --env-file .env "${compose_files[@]}" config --quiet
set -a
source .env
set +a
"$root/scripts/check-ollama.sh"
if [[ "$use_mlx" == true ]]; then
  "$root/scripts/paddleocr-mlx.sh" status >/dev/null
fi
docker compose --env-file .env "${compose_files[@]}" ps postgres --status running --format '{{.Name}}' | grep -q .
docker compose --env-file .env "${compose_files[@]}" ps paddleocr-vl-api --status running --format '{{.Name}}' | grep -q .
curl -fsS "http://127.0.0.1:${PADDLEOCR_PORT:-8090}/health" >/dev/null
curl -fsS "http://127.0.0.1:${WEB_PORT:-8088}/health/ready" >/dev/null
cd "$root"
dotnet format InvoiceCapture.slnx --no-restore --verify-no-changes
dotnet build InvoiceCapture.slnx -c Release --no-restore
dotnet test InvoiceCapture.slnx -c Release --no-build --logger 'console;verbosity=minimal'
