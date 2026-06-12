# Micro Node Activation MediaPipe Bridge

1. Open `Assets/Shared/Try/MicroNodeActivation.unity` in Unity and press Play.
2. Double-click `run_micro_node_tracker.command`.
3. Allow camera access when macOS asks.
4. Move the index fingertip to a node and pinch the thumb and index finger.
5. Press `Q` or `Esc` in the camera window to stop tracking.

The first launch creates a local Python environment, installs MediaPipe and
OpenCV, and downloads the official Hand Landmarker model. Unity receives
`OPEN,x,y` and `PINCH,x,y` messages over UDP port 5052.
