#!/usr/bin/env bash

configure_dev_profile() {
  compose_files=(-f compose.yml -f compose.override.yml)
  use_mlx=false

  local accelerator="${PADDLEOCR_DEV_ACCELERATOR:-auto}"
  if [[ "$accelerator" != "auto" && "$accelerator" != "cpu" && "$accelerator" != "mlx" ]]; then
    echo "PADDLEOCR_DEV_ACCELERATOR musi mieć wartość: auto, cpu albo mlx." >&2
    return 2
  fi

  if [[ "$accelerator" == "mlx" ]] \
    || [[ "$accelerator" == "auto" && "$(uname -s)" == "Darwin" && "$(uname -m)" == "arm64" ]]; then
    compose_files+=(-f compose.apple-silicon.yml)
    use_mlx=true
  fi
}
