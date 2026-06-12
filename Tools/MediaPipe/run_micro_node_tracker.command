#!/bin/zsh
set -e

SCRIPT_DIR="${0:A:h}"
VENV_DIR="$SCRIPT_DIR/.venv"
MODEL_DIR="$SCRIPT_DIR/models"
MODEL_PATH="$MODEL_DIR/hand_landmarker.task"
MODEL_URL="https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task"

cd "$SCRIPT_DIR"

OLD_TRACKER_PIDS=$(pgrep -f "$SCRIPT_DIR/(micro_node_tracker|sky_reach_tracker|energy_flow_tracker|rotation_core_tracker|lateral_circuit_tracker).py" || true)
if [[ -n "$OLD_TRACKER_PIDS" ]]; then
  echo "$OLD_TRACKER_PIDS" | xargs kill
  sleep 0.5
fi

if [[ ! -x "$VENV_DIR/bin/python" ]]; then
  python3 -m venv "$VENV_DIR"
fi

"$VENV_DIR/bin/python" -m pip install --quiet --upgrade pip
"$VENV_DIR/bin/python" -m pip install --quiet -r requirements.txt

if [[ ! -f "$MODEL_PATH" ]]; then
  mkdir -p "$MODEL_DIR"
  curl -L --fail --progress-bar "$MODEL_URL" -o "$MODEL_PATH"
fi

exec "$VENV_DIR/bin/python" "$SCRIPT_DIR/micro_node_tracker.py" "$@"
