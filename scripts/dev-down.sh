#!/usr/bin/env bash
set -euo pipefail

readonly scripts_dir="$(cd "$(dirname "$0")" && pwd)"
source "$scripts_dir/lib/dev-profile.sh"
cd "$scripts_dir/../deploy"

configure_dev_profile
docker compose --env-file .env "${compose_files[@]}" down
if [[ "$use_mlx" == true ]]; then
  "$scripts_dir/paddleocr-mlx.sh" stop
fi
