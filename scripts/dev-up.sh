#!/usr/bin/env bash
set -euo pipefail

readonly scripts_dir="$(cd "$(dirname "$0")" && pwd)"
source "$scripts_dir/lib/dev-profile.sh"
cd "$scripts_dir/../deploy"

configure_dev_profile
if [[ "$use_mlx" == true ]]; then
  "$scripts_dir/paddleocr-mlx.sh" start
  echo "Profil OCR: Apple Silicon (layout w kontenerze, VLM przez hostowy MLX/Metal)."
else
  echo "Profil OCR: CPU (pełny pipeline w kontenerze)."
fi

docker compose --env-file .env "${compose_files[@]}" pull postgres
docker compose --env-file .env "${compose_files[@]}" build
docker compose --env-file .env "${compose_files[@]}" up -d --remove-orphans
