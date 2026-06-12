#!/usr/bin/env python3
"""MediaPipe hand tracking bridge for the Micro Node Activation Unity scene."""

from __future__ import annotations

import argparse
import math
import socket
import time
from pathlib import Path

import cv2
import mediapipe as mp


HAND_CONNECTIONS = (
    (0, 1), (1, 2), (2, 3), (3, 4),
    (0, 5), (5, 6), (6, 7), (7, 8),
    (5, 9), (9, 10), (10, 11), (11, 12),
    (9, 13), (13, 14), (14, 15), (15, 16),
    (13, 17), (17, 18), (18, 19), (19, 20), (0, 17),
)


def parse_args() -> argparse.Namespace:
    script_dir = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--model",
        type=Path,
        default=script_dir / "models" / "hand_landmarker.task",
        help="Path to the MediaPipe Hand Landmarker model.",
    )
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5052)
    parser.add_argument("--width", type=int, default=1280)
    parser.add_argument("--height", type=int, default=720)
    parser.add_argument("--pinch-enter", type=float, default=0.38)
    parser.add_argument("--pinch-release", type=float, default=0.52)
    parser.add_argument("--smoothing", type=float, default=0.35)
    return parser.parse_args()


def distance(a: object, b: object) -> float:
    return math.hypot(a.x - b.x, a.y - b.y)


def draw_hand(frame, landmarks, pinching: bool) -> None:
    height, width = frame.shape[:2]
    points = [
        (int(landmark.x * width), int(landmark.y * height))
        for landmark in landmarks
    ]
    line_color = (60, 240, 255) if not pinching else (255, 80, 220)
    for start, end in HAND_CONNECTIONS:
        cv2.line(frame, points[start], points[end], line_color, 2, cv2.LINE_AA)
    for index, point in enumerate(points):
        color = (255, 255, 255)
        radius = 4
        if index in (4, 8):
            color = line_color
            radius = 8
        cv2.circle(frame, point, radius, color, -1, cv2.LINE_AA)


def main() -> int:
    args = parse_args()
    if not args.model.is_file():
        raise FileNotFoundError(
            f"MediaPipe model not found: {args.model}\n"
            "Run run_micro_node_tracker.command once to install and download it."
        )

    capture = cv2.VideoCapture(args.camera)
    capture.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    capture.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)
    if not capture.isOpened():
        raise RuntimeError(f"Could not open camera {args.camera}.")

    udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    destination = (args.host, args.port)
    base_options = mp.tasks.BaseOptions(model_asset_path=str(args.model))
    options = mp.tasks.vision.HandLandmarkerOptions(
        base_options=base_options,
        running_mode=mp.tasks.vision.RunningMode.VIDEO,
        num_hands=1,
        min_hand_detection_confidence=0.55,
        min_hand_presence_confidence=0.55,
        min_tracking_confidence=0.55,
    )

    smoothed_x = 0.5
    smoothed_y = 0.5
    pinching = False
    started_at = time.monotonic()

    try:
        with mp.tasks.vision.HandLandmarker.create_from_options(options) as landmarker:
            while True:
                ok, frame = capture.read()
                if not ok:
                    break

                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
                timestamp_ms = int((time.monotonic() - started_at) * 1000)
                result = landmarker.detect_for_video(mp_image, timestamp_ms)
                status = "SHOW ONE HAND"
                status_color = (80, 180, 255)

                if result.hand_landmarks:
                    landmarks = result.hand_landmarks[0]
                    thumb_tip = landmarks[4]
                    index_tip = landmarks[8]
                    wrist = landmarks[0]
                    middle_mcp = landmarks[9]

                    palm_scale = max(distance(wrist, middle_mcp), 0.001)
                    pinch_ratio = distance(thumb_tip, index_tip) / palm_scale
                    if pinching:
                        pinching = pinch_ratio < args.pinch_release
                    else:
                        pinching = pinch_ratio < args.pinch_enter

                    smoothing = min(max(args.smoothing, 0.01), 1.0)
                    # Camera coordinates are opposite to Unity world movement:
                    # raw left/right faces the viewer, and image Y grows down.
                    unity_x = 1.0 - index_tip.x
                    unity_y = 1.0 - index_tip.y
                    smoothed_x += (unity_x - smoothed_x) * smoothing
                    smoothed_y += (unity_y - smoothed_y) * smoothing
                    action = "PINCH" if pinching else "OPEN"
                    message = f"{action},{smoothed_x:.5f},{smoothed_y:.5f}"
                    udp.sendto(message.encode("ascii"), destination)

                    draw_hand(frame, landmarks, pinching)
                    status = "PINCH" if pinching else "OPEN"
                    status_color = (255, 80, 220) if pinching else (60, 240, 255)
                else:
                    pinching = False

                # Mirror only the preview. Tracking and Unity coordinates above
                # stay independent from display orientation.
                preview = cv2.flip(frame, 1)
                cv2.putText(
                    preview,
                    status,
                    (24, 48),
                    cv2.FONT_HERSHEY_SIMPLEX,
                    1.1,
                    status_color,
                    3,
                    cv2.LINE_AA,
                )
                cv2.imshow("Micro Node Activation - MediaPipe", preview)
                if cv2.waitKey(1) & 0xFF in (27, ord("q")):
                    break
    finally:
        capture.release()
        udp.close()
        cv2.destroyAllWindows()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
