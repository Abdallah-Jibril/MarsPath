# Mission dispatch and route-facing lock

## Behavior

The desktop **Enter Helmet Demo / Start Full Mission** action queues a frozen copy of the selected route even when the Quest connection is not ready. The desktop remains in the planner until Quest acknowledges loading that plan. It then prepares the matching local mirror at zero. Quest movement remains gated by the desktop readiness acknowledgement. A failed or unavailable connection leaves a visible waiting/error state; it cannot start a standalone advancing mission. **Laptop Preview** remains an explicit local inspection action.

Quest walking requires a valid horizontal head direction toward the route. Looking more than 75° away locks movement. The lock releases within 50°, providing hysteresis. These are interaction thresholds, separate from terrain slope limits. A backwards gaze displays **ROUTE BEHIND YOU / TURN AROUND · WALK LOCKED**. Other off-route headings show a left/right correction. The right trigger never recenters the camera. Facing the route again permits walking while the trigger remains held; releasing the trigger stops movement. Missing tracking retains the existing release-to-rearm rule.

The radio stays available while direction is locked. Head pose, movement lock, and warning are mirrored on the laptop. A compact red warning remains at the side of the view. Walking sounds and astronaut movement stop together.

## Verification

The Windows runtime review passed these cases:

1. A backwards gaze and held right trigger do not advance progress.
2. A blocked trigger does not rotate the camera.
3. Partial recovery keeps the movement lock; facing the route releases it.
4. A launch issued without a connection queues the route and stays in the planner.
5. Quest receives the frozen selected route even if the local selection changes afterward.
6. The desktop stays in the planner during Quest preparation, then enters the acknowledged route at zero.
7. The desktop mirrors the direction lock, warning, head pose, progress, stop, reserves, and mission exit.

Editor checks passed opposite headings, turn direction, vertical/invalid gaze, and threshold hysteresis. Encrypted voice in both directions, half-duplex interruption, jitter-buffer continuity, and speech filtering passed again.

Evidence is saved in `tmp/dispatch-facing-windows.log` and `tmp/dispatch-facing-live-review.log`. Android build output is in `MarsPathUnity/Builds/Quest/build.log`. Physical controller behavior remains available for final user verification after installation.

Both platform builds succeeded. The updated Quest APK was installed successfully, launched, and verified against the local artifact by SHA-256. The matching Windows application was opened. The separate headset listening fix and speech cleanup remain included.
