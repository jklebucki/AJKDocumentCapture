#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
if [[ ! -f "$root/deploy/.env" ]]; then
  cp "$root/deploy/.env.example" "$root/deploy/.env"
  printf 'Created deploy/.env. Replace POSTGRES_PASSWORD before starting services.\n'
fi
dotnet restore "$root/InvoiceCapture.slnx"
