# Live mission and voice verification

**Date: 14 November 2026**

## Changes

- Quest is the source of mission progress, movement, resources, alerts, and tracked eye pose. Pose updates continue while the astronaut is stationary.
- The desktop visor mirrors head yaw, pitch, roll, and position relative to the astronaut. Its V chase view remains available.
- Quest walking requires facing the route. A backwards or off-route gaze locks movement, provides a turn warning, and preserves radio and head tracking. The trigger does not turn the camera. Direction lock begins beyond 75° and releases within 50°.
- Desktop dispatch stays in the planner until the selected, frozen route is acknowledged as loaded on Quest. An unavailable link cannot trigger a standalone local mission.
- Quest radio playback uses a dedicated 48 kHz Android media/speech track. Windows uses Unity DSP. The shared encrypted transport and jitter buffer remain in place.
- Strong received speech gets less amplification before limiting. Native Quest speech has a 90 Hz high-pass and 5.5 kHz low-pass.
- EVA effects include eight boot-contact variations and a soft helmet ventilation/breathing loop. Effects duck during radio traffic.
- The Windows player permits one instance and continues updating in the background.

## Automated checks

| Check | Result |
| --- | --- |
| Encrypted radio in both directions without render ticks | Passed |
| Exact selected route, preparation handshake, stale-session rejection | Passed |
| Jitter buffer continuity, bounded queues, half-duplex interruption | Passed |
| Head yaw, pitch, roll and eye offset while stopped | Passed in the Windows runtime review |
| Desktop chase view and source-controlled progress | Passed |
| Opposite-heading lock, recovery, turn direction and heading hysteresis | Verification recorded in the dispatch/facing revision logs |
| Source stop, stale link freeze, arrival, demo continuation, return | Passed |
| Single-instance Windows launch | Passed with an already running player |
| Speech filter gain at 500 Hz | 0.999 |
| Speech filter gain at 20 Hz | 0.049 |
| Speech filter gain at 14 kHz | 0.083 |
| Strong-input receive headroom | Passed |
| Eleven generated WAV assets and seamless-loop boundary | Passed |
| Focused Windows audio runtime | Passed: eleven clips, four footsteps, pause/seek without footsteps, silent globe |

## Connected-device evidence

The earlier laptop capture was nonzero despite the missing headset sound. The headset's Unity mixer reported 24 kHz while the radio payload is 48 kHz. The native media output explicitly uses the radio sample rate and its own hardware-paced writer.

During the connected Quest test, the headset decoded and consumed 55,680 incoming samples, reported a nonzero output peak of 0.8871, and recorded zero jitter-buffer underruns for that sample. The laptop also received nonzero headset speech. These measurements concern that test interval; they are not a guarantee for every network.

The user confirmed audibility of laptop-to-Quest speech and described it as excellent, then requested cleanup of slight residual noise. The frequency filtering and strong-input gain refinement were added afterward. The 15 November follow-up below records acceptance of the subsequently updated headset build.

The final filtered build was installed successfully on the connected Quest at **23:49:12** local time. The installed APK's SHA-256 matched the local release artifact. The corresponding Windows build was opened for the final trial. Both builds completed successfully, and the focused Windows soundscape review passed.

No microphone recording was saved. Diagnostics contain signal levels and counters only.

## Local evidence

- `tmp/head-live-review.log`: live movement and head-pose runtime assertions.
- `tmp/speech-cleanup-windows.log`: encrypted transport, recentering, and frequency/level checks.
- `tmp/native-radio-laptop.log`: laptop transmission and reception counters.
- `tmp/quest-native-voice.log`: connected headset output evidence before the signed-array cleanup.
- `tmp/eva-audio-focused-review.log`: focused player soundscape verification.
- `tmp/quest-speech-final.log`: final headset startup and filtered speech diagnostics after installation.
- `MarsPathUnity/Builds/Quest/build.log`: latest Android build.

The two-minute preview remains continuous along the route. Terrain heights, route calculations, reserve physics, and coverage limits were not changed by this revision.

## 15 November: speech delay and longer showcase

The user reported hearing laptop speech twice in succession while both participants were in the same room. Direct acoustic speech followed by delayed radio playback is a possible contributor; the report alone does not establish the cause.

- Quest speech now explicitly excludes simultaneous Unity DSP playback while the native Android output owns the receive stream. A regression check verifies that the inactive output neither plays nor consumes the stream.
- Radio speech bypasses Unity reverb and spatialization. The Android speech track requests low-latency playback and disables platform spatialization where supported.
- USB reception starts with 40 ms of prefill; Wi-Fi starts with 60 ms. Genuine receive gaps can increase this to 140 ms. The native output also adapts its effective buffer if hardware underruns occur.
- Clearing or changing the connection discards pending radio samples. The existing speech filter, level limiter, encrypted transport, and half-duplex microphone behavior remain in use. This revision does not add acoustic echo cancellation.
- The Jezero showcase endpoint is now 18.4778 N, 77.3778 E. The measured Balanced route is approximately 202 m, with about 8 m of ascent and 43 m of descent. All five alternatives are available; minimum pairwise route divergence is approximately 15.7 m.
- Runtime checks verify arrival after 120 seconds of active demo walking, with the countdown paused when walking stops. Full Mission covers approximately 30.52% after 120 mission seconds and reaches the endpoint after its modeled 393.20-second duration for Balanced. Demo compression does not alter the Full Mission clock.

Both release builds completed, the APK signature verified, and installation on the connected Quest succeeded. The installed APK's SHA-256 matches the latest local build. The sustained-speech follow-up below records the subsequent user acceptance.

The connected USB trial decoded and consumed 883,200 incoming samples, with a nonzero output peak of 0.7571. Quest used the Android media output while Unity DSP playback was stopped. The native track reported zero underruns with a 40 ms effective buffer. Four receive-buffer gaps increased adaptive prefill to 120 ms during the trial. These counters demonstrate reception and a single active software output, not subjective absence of echo. No microphone recording was saved.

Evidence: `tmp/echo-showcase-windows.log`, `tmp/echo-showcase-timing.log`, `tmp/echo-showcase-live.log`, `tmp/echo-radio-laptop.log`, `tmp/echo-quest-radio.log`, and `MarsPathUnity/Builds/Quest/build.log`.

### Sustained speech and same-room follow-up

The user reported that a transmission starts clearly and becomes echo-like while Y remains held. A further physical trial found clear sustained speech with no echo after moving the headset into another room. This identifies proximity as a contributor, not a proven electronic replay defect. It does not distinguish direct room speech from headset-speaker leakage recaptured by the laptop microphone.

The live-audio queues now discard stale speech: microphone capture and pending sends retain at most 120 ms each, and a receive queue exceeding 300 ms catches up to the adaptive prefill plus 40 ms. The queue limit does not bound total network or hardware latency. Distinct-frame regression checks passed for 60 simulated seconds, preserving order without repetition or steadily increasing backlog. Additional checks verify current speech after a delayed receive burst and a blocked send queue.

For the requested 2–3 m demonstration setup without earphones, the trial used the Quest speakers and speech close to the laptop microphone. The user subsequently confirmed clear speech with no audible echo at approximately 70% headset volume and a nearby separation suitable for presenting to the judges. The separation was not instrument-measured. No acoustic echo canceller or guaranteed speakerphone performance is claimed.

Both rebuilt applications were opened for the proximity trial. The refreshed APK installed successfully and its on-device SHA-256 matched the release artifact. Initial connected-device diagnostics used the native output exclusively, with nonzero received speech, no native underruns, and no capture or send-queue skips in that interval. The user accepted the listening result for the tested presentation setup on 15 November 2026; other room acoustics, devices, and network conditions still require their own check.

Additional evidence: `tmp/sustained-voice-windows.log`, `tmp/sustained-radio-laptop.log`, and `tmp/sustained-quest-radio.log`.
