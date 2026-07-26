#!/usr/bin/env bash
set -euo pipefail
: "${OLLAMA_BASE_URL:?}" "${OLLAMA_MODEL:?}"
curl -fsS "$OLLAMA_BASE_URL/api/tags" | grep -Fq "$OLLAMA_MODEL"
printf 'Ollama OK: %s\n' "$OLLAMA_MODEL"
