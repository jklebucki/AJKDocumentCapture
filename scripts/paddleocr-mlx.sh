#!/usr/bin/env bash
set -euo pipefail

readonly state_root="${PADDLEOCR_MLX_STATE_ROOT:-$HOME/.cache/ajk-document-capture/paddleocr-mlx}"
readonly venv_path="$state_root/venv"
readonly log_path="$state_root/server.log"
readonly plist_path="$state_root/service.plist"
readonly version="${PADDLEOCR_MLX_VERSION:-0.6.8}"
readonly port=8111
readonly model="PaddlePaddle/PaddleOCR-VL-1.6"
readonly service_label="pl.ajk.documentcapture.paddleocr-mlx"
readonly launch_domain="gui/$(id -u)"

require_apple_silicon() {
  if [[ "$(uname -s)" != "Darwin" || "$(uname -m)" != "arm64" ]]; then
    echo "MLX-VLM wymaga macOS na Apple Silicon." >&2
    exit 1
  fi
}

find_python() {
  local candidate
  for candidate in "${PADDLEOCR_MLX_PYTHON:-}" python3.13 python3.12 python3; do
    [[ -n "$candidate" ]] || continue
    command -v "$candidate" >/dev/null 2>&1 || continue
    if "$candidate" -c 'import sys; raise SystemExit(not ((3, 9) <= sys.version_info[:2] <= (3, 13)))'; then
      command -v "$candidate"
      return
    fi
  done

  echo "Brak Pythona 3.9–3.13 dla MLX-VLM. Zainstaluj np. python@3.13 przez Homebrew." >&2
  exit 1
}

server_is_ready() {
  curl --fail --silent --show-error --max-time 2 \
    "http://127.0.0.1:$port/v1/models" 2>/dev/null \
    | grep --fixed-strings --quiet "\"id\":\"$model\""
}

install_runtime() {
  require_apple_silicon
  mkdir -p "$state_root"

  if [[ ! -x "$venv_path/bin/python" ]]; then
    "$(find_python)" -m venv "$venv_path"
  fi

  local installed_version=""
  installed_version="$("$venv_path/bin/python" -c \
    'import importlib.metadata; print(importlib.metadata.version("mlx-vlm"))' 2>/dev/null || true)"
  if [[ "$installed_version" != "$version" ]]; then
    "$venv_path/bin/python" -m pip install --disable-pip-version-check --upgrade pip
    "$venv_path/bin/python" -m pip install --disable-pip-version-check "mlx-vlm==$version"
  fi
}

start_server() {
  install_runtime
  if server_is_ready; then
    echo "MLX-VLM działa na http://127.0.0.1:$port."
    return
  fi

  : >"$log_path"
  "$venv_path/bin/python" - \
    "$plist_path" "$service_label" "$venv_path/bin/mlx_vlm.server" \
    "$port" "$model" "$log_path" <<'PY'
import plistlib
import sys

_, plist_path, label, executable, port, model, log_path = sys.argv
configuration = {
    "Label": label,
    "ProgramArguments": [
        executable,
        "--host", "127.0.0.1",
        "--port", port,
        "--model", model,
    ],
    "RunAtLoad": True,
    "ProcessType": "Interactive",
    "StandardOutPath": log_path,
    "StandardErrorPath": log_path,
}
with open(plist_path, "wb") as stream:
    plistlib.dump(configuration, stream)
PY

  if launchctl print "$launch_domain/$service_label" >/dev/null 2>&1; then
    launchctl bootout "$launch_domain/$service_label"
    for _ in {1..20}; do
      launchctl print "$launch_domain/$service_label" >/dev/null 2>&1 || break
      sleep 0.25
    done
  fi

  local bootstrapped=false
  for _ in {1..20}; do
    if launchctl bootstrap "$launch_domain" "$plist_path" 2>/dev/null; then
      bootstrapped=true
      break
    fi
    sleep 0.25
  done
  if [[ "$bootstrapped" != true ]]; then
    launchctl bootstrap "$launch_domain" "$plist_path"
  fi

  for _ in {1..120}; do
    if server_is_ready; then
      echo "MLX-VLM uruchomiony na http://127.0.0.1:$port."
      return
    fi
    if ! launchctl print "$launch_domain/$service_label" >/dev/null 2>&1; then
      echo "Usługa MLX-VLM zakończyła pracę podczas startu. Log: $log_path" >&2
      tail -30 "$log_path" >&2
      exit 1
    fi
    sleep 1
  done

  echo "MLX-VLM nie osiągnął gotowości w 120 s. Log: $log_path" >&2
  exit 1
}

stop_server() {
  if launchctl print "$launch_domain/$service_label" >/dev/null 2>&1; then
    launchctl bootout "$launch_domain/$service_label"
    echo "MLX-VLM zatrzymany."
  else
    echo "Brak procesu MLX-VLM zarządzanego przez ten projekt."
  fi
}

show_status() {
  if server_is_ready; then
    echo "ready http://127.0.0.1:$port"
    return
  fi
  echo "stopped"
  return 1
}

case "${1:-}" in
  install) install_runtime ;;
  start) start_server ;;
  stop) stop_server ;;
  status) show_status ;;
  logs) tail -n "${2:-80}" "$log_path" ;;
  *)
    echo "Użycie: $0 {install|start|stop|status|logs [liczba-linii]}" >&2
    exit 2
    ;;
esac
