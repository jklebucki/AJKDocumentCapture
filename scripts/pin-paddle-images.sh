#!/usr/bin/env bash
set -euo pipefail
: "${PADDLE_OCR_GPU_IMAGE:?Set the exact Paddle image tag first}"
docker pull "$PADDLE_OCR_GPU_IMAGE"
digest="$(docker image inspect --format='{{index .RepoDigests 0}}' "$PADDLE_OCR_GPU_IMAGE")"
printf 'PADDLE_OCR_GPU_IMAGE=%s\n' "$digest" > "$(dirname "$0")/../deploy/.env.lock"
printf 'Pinned %s\n' "$digest"
