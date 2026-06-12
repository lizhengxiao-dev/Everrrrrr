# MicroNode Activation clean handoff

Open this scene:

`Assets/Shared/Try/MicroNodeActivation.unity`

Press Play. The scene has `MicroNodeMediaPipeLauncher` on `MicroNodeGameManager`; on macOS it automatically opens:

`Tools/MediaPipe/run_micro_node_tracker.command`

First launch may take a few minutes because it creates a local Python venv, installs MediaPipe/OpenCV, downloads the hand model, and asks for camera permission. After that, Play should connect through UDP port 5052 automatically.

Controls:
- Move index fingertip to a glowing node.
- Pinch thumb + index to collect it.
- Press F for female robot, M for male robot.
- Press R to restart after the ending panel.

If automatic launch fails, double-click `Tools/MediaPipe/run_micro_node_tracker.command` once manually, then press Play again.
