# MarsPath · Developer Handoff

**LAGRANGE V · Existing baseline imported 14 November 2026**

Use this document to continue Windows and Meta Quest 3 development without earlier conversations. Start with the [README](README.md), then review this handoff before changing runtime behavior.

## Baseline

| Item | Value |
| --- | --- |
| Repository | [Abdallah-Jibril/MarsPath_Hackathon](https://github.com/Abdallah-Jibril/MarsPath_Hackathon) |
| Default branch | `main` |
| Unity project | `MarsPathUnity` |
| Unity version | `6000.6.5f1` |
| Entry scene | `Assets/Scenes/MarsPathDemo.unity` |
| Quest package | `com.lagrangev.marspath` |
| Demonstration | Belva showcase and Jezero 10 × 10 km mission zone |
| Import provenance | [Migration record](docs/MIGRATION.md) and [source inventory](docs/IMPORT_MANIFEST.json) |

The laptop and standalone headset share terrain and routing code, with separate input and presentation layers. Preserve both platforms when changing mission behavior. Continue the existing application.

## User experience

**Global Mars → endpoints or Jezero showcase → compare routes → select route → helmet mission.**

The showcase loads reference endpoints and opens comparison. It must allow selecting another route before starting. The 10 × 10 km map supports free endpoint selection within measured coverage; endpoints need at least 80 m separation.

Reference Belva endpoints are approximately **18.475002° N, 77.377312° E** and **18.477800° N, 77.377800° E**. The reference Balanced track is approximately 202 m, with roughly 8 m ascent and 43 m descent. Its full-route ETA is about 6.55 mission minutes; demo playback completes it in 120 active seconds.

The two-minute demo completes the selected Jezero route in 120 active seconds. Releasing the trigger or losing a valid walking state freezes position and countdown together. Longer Jezero routes play at a faster presentation pace. Outside the measured sector, the demo previews up to 192 m and offers continuation. Full mission mode exposes mission-time speeds ×1, ×30, and ×50.

The shortcut opens borderless fullscreen. Interface text is English. Coordinate parsing also accepts additional numeral forms.

## Controls

| Quest input | Global Mars / Jezero zone | Route comparison | Helmet mission |
| --- | --- | --- | --- |
| Right index trigger | Place endpoint / select UI | Select route / UI | Hold to walk while facing the route; release to stop |
| Left index trigger | Hold to transmit from Mission Control lobby | — | Hold to transmit voice |
| Left stick | Rotate globe / pan zone | Orbit terrain | — |
| Right stick | Up/down to zoom | Left/right pan; up/down zoom | — |
| Right A | Enter independent planning from the lobby / switch pins in the planner | Enter selected route | Continue / replay after arrival |
| Right B | Zone back to globe / globe back to Mission Control | Back to endpoints | Close radio panel / return to Mission Control or local routes |
| Left X | Compare routes | Route details | Live details |
| Left Y | Jezero showcase comparison | — | Toggle radio status |
| Head movement | Look around | Look around | Look around while stopped or moving |

Release the trigger after entering a mission or accepting microphone permission, then press again. The neutral Quest eye height is 1.84 m above local terrain. Movement uses the controller, without requiring a physically large room.

| Desktop input | Action |
| --- | --- |
| Left drag | Pan planner |
| Right drag | Orbit planner / mission look |
| Wheel / + / − | Bounded planner zoom |
| FIT VIEW | Reset framing |
| Hold Y | Transmit from route comparison or a mission |
| V | Switch visor/chase |
| Esc | Mission menu |
| Alt+F4 | Close application |

## Mission Control and radio

Quest opens in the Global Mars **Waiting for Mission Control** lobby. When the laptop comparison screen shows **Quest Waiting**, choose a route and press **Enter Helmet Demo** or **Start Full Mission**. Both apps enter the selected route at zero. Quest waits until the laptop confirms readiness, then the astronaut controls movement with the right trigger.

Quest publishes movement, progress, geographic position, demo/full state, reserves, effort, terrain alerts, and tracked head pose up to 30 times per second. The laptop visor mirrors head yaw, pitch, roll, and eye offset even when the astronaut is stopped. Its chase view remains available with V. Releasing the Quest trigger stops both views. A stale link holds the last confirmed position and displays a reconnecting indicator. The laptop can use D for details and Esc for its local menu. Returning from Quest with B returns the laptop to route comparison.

The Quest right trigger does not recenter the camera. Looking more than 75° away from the horizontal route direction locks walking. The lock releases within 50° of the route, providing hysteresis at the boundary. A backwards view shows **ROUTE BEHIND YOU / TURN AROUND · WALK LOCKED**; other off-route headings show the left/right correction. A missing valid heading also holds position. These are interface thresholds, not measured terrain safety limits. Returning to face the route permits movement while the trigger is held. Releasing it stops movement. Radio and head-pose synchronization remain active while the direction lock is on. Route bends update the virtual heading during traversal. Standing-height calibration remains unchanged. Windows runs in the background and allows one application instance.

Desktop helmet launch always queues the selected route for Mission Control, even if the headset is temporarily unavailable. It stays in the planner until Quest acknowledges loading the frozen plan. It then prepares the matching desktop mirror at zero and announces readiness. There is no disconnected fallback to an advancing local mission. The separate **LAPTOP PREVIEW** button supports local visual inspection.

The Quest lobby preloads the bundled Jezero DEM and initial Belva imagery. The dispatched headset builds the walking surface directly, and Windows reuses the current Jezero planner when it matches the transmitted plan. The 15 November local Wi-Fi trials reached Quest readiness in 2.55 s and 2.47 s, with both live views ready in 3.64 s and 3.54 s. [Timing method and trial details](docs/LIVE_MISSION_AUDIO_VALIDATION.md#15-november-faster-mission-entry-over-local-wi-fi)

The astronaut starts stationary and holds the right trigger to walk. Right B returns from a dispatched mission to the waiting lobby. A opens the independent Quest planner from the lobby. In independent planning, B returns from the zone or globe as indicated on screen.

Hold Y from the laptop route comparison or helmet. Hold the left trigger from the Quest lobby or helmet. Take turns and release the button after speaking. The radio stays available while walking is paused.

Quest speech playback uses a 48 kHz mono PCM Android `AudioTrack` with media/speech attributes and blocking writes on a dedicated thread. This gives received speech a hardware-paced output independent of Unity's source virtualization and render timing. Unity DSP remains the desktop output and the Quest fallback if native output cannot initialize. The shared jitter buffer, receive gain, peak limiter, encryption, automatic USB/Wi-Fi selection, and half-duplex rules still apply. Headset suspension and push-to-talk clear queued playback. `MARSPATH_VOICE` logs separate capture peak, decoded samples, consumed samples, output peak, and native written samples. `Receiving` confirms traffic; verify audibility in both directions on the device after installing a new build.

The EVA effects are synthesized simulation assets: eight boot-contact/grit variations with distance-driven timing and a soft looping helmet ventilation/breathing layer. They are not recorded astronaut audio. Mars attenuation is an acoustic design reference, described in [NASA's Perseverance sound report](https://www.nasa.gov/solar-system/what-sounds-captured-by-nasas-perseverance-rover-reveal-about-mars/). Radio activity ducks the effects. Regenerate assets with `py -3.9 tools/prepare_eva_audio.py` or another Python 3 interpreter.

Initial pairing is provisioned automatically over authorized USB. Windows prefers USB while attached, then attempts discovery of the paired headset over a compatible local network. USB needs no internet. Wi-Fi requires local device-to-device connectivity; institutional client isolation may block it despite working internet access.

| State | Meaning |
| --- | --- |
| USB READY / WI-FI READY | Connected transport |
| CONNECTING VIA USB / WI-FI | Setup in progress |
| Talking · amber | Local microphone is transmitting |
| Receiving · cyan | Incoming voice is playing |
| Waiting for link | Talk button held before connection |

Discovery uses UDP 42740. Quest listens on TCP 42741; USB forwarding uses desktop TCP 42739. Pairing material stays in local application data. Never commit pairing keys, credentials, or microphone recordings.

The radio selects a physical Windows microphone, passes one speaker at a time, and boosts incoming Quest speech with a peak limiter. Dedicated reader/writer tasks carry encrypted frames independently of terrain rendering. Playback starts with a 40 ms USB or 60 ms Wi-Fi prefill and adapts up to 140 ms after packet gaps. Quest speech has one active output, requests low-latency Android playback, and disables spatialization for the radio. Route state is coalesced to its latest value to prevent growing live latency. Both apps need the same protocol revision.

Live speech prioritizes freshness over retaining delayed syllables: capture and unsent-voice queues each retain at most six 20 ms frames. If the receive queue exceeds 300 ms, it skips to the current speech near the adaptive prefill target. These are queue limits, not a guarantee of end-to-end latency on a stalled network. Diagnostics report capture skips, dropped sends, and skipped receive samples. Regression checks cover a 60-second simulated utterance with distinct sequential frames, irregular delivery, and recovery from a blocked sender or delayed receive burst.

For a same-room presentation approximately 2–3 m apart, verify the acoustic setup at that actual distance. The user confirmed clear long transmissions when the headset was moved into a separate room, so proximity contributes to the reported echo. Direct speech audible to the wearer and headset-speaker leakage into the laptop microphone are distinct possible paths; this trial does not identify which dominates. Prefer a moderate headset volume and quiet speech close to the laptop's physical microphone. Isolating wired earphones are an alternative when available. The app does not perform acoustic echo cancellation and cannot cancel speech that reaches the wearer's ears directly through the room. USB and Wi-Fi do not remove that physical path.

The software regression covers encrypted route transfer, duplicate protection, the laptop-ready gate, live state, two-way voice arriving without render ticks, speech sample conservation, and irregular packet batches. The player flag `--mars-live-mission-review` exercises zero start, non-Balanced route, movement, stop, source reserves and alerts, stale updates, session identity, continuation, arrival, and return. Physical speech quality requires listening on the target hardware.

## Terrain contract

The active Jezero resources use the USGS Mars 2020 TRN HiRISE master DTM and matching orthomosaic. Approximately 100 km² are supported by a 1 m original height grid, 16 overlapping runtime height tiles, and a documented source-valid crop. The runtime sector package is approximately 547 MB.

The [sector audit](GIS/OfficialSurfaceSources/JezeroMissionZone10km/README.md) records exact bounds, affine transform, validity checks, source URLs, and hashes. Selected image crops reach 0.25 m. That does not make the DEM quarter-metre resolution or extend fine imagery to uncovered locations.

Planner and visor use the same geographic terrain and 1:1 vertical scale. Preserve measured heights, vertical reference consistency, and real data gaps. Mesh density, source post spacing, and source accuracy are separate quantities.

Rock meshes and placements are illustrative. Warnings count rendered obstacles ahead. O2, power, energy, risk, and science are prototype models requiring calibration. QGIS supports preparation and independent checks; Unity reads the prepared products through C# providers.

## EVA physics update

Read [PHYSICS_AND_HAZARDS.md](docs/PHYSICS_AND_HAZARDS.md) before presenting or changing consumption and warning behavior. Windows and Quest load the shared `Resources/Science/MarsEvaModel.json` profile.

- Signed measured grades drive mechanical work; ascent and descent are integrated separately.
- Metabolic power converts to oxygen through the simplified Weir equation at declared RQ/STPD conditions.
- Suit electrical use is a separate assumed load, integrated in Wh. Capacity is 446.88 Wh from a clearly labeled legacy EMU battery reference, replacing the former arbitrary 4 kWh.
- Gait coefficients, electrical loads, 0.8 kg oxygen capacity, and operating reserves remain concept assumptions requiring calibration.
- Mission pause freezes simulated time. It does not mean a stopped real astronaut consumes no oxygen.
- The 10° warning is an early-caution policy, while 25° is the configurable graph exclusion threshold. Neither establishes actual boot/regolith traction safety.
- The source-gradient warning does not use decorative microrelief. Rock alerts describe rendered illustrative objects, not surveyed planetary obstacles.
- Energy A* and RCSP resource bounds derive their minimum resource cost per metre from the actual graph. Preserve admissibility if load models change.

Run `MarsPath.Editor.MarsPhysicsRegressionChecks.Run` for the independent balance/geometry tests. `CheckAndBuildWindows` also runs randomized routing checks, two-way radio software checks, and the Windows build. Rebuild Quest for shared-code changes. Numerical checks are not human physiology or flight validation.

The [current verification record](docs/PHYSICS_VALIDATION.md) and [physics package manifest](docs/PHYSICS_PACKAGES.json) supersede the imported baseline for this shared model. Download [EVA Physics v1](https://github.com/Abdallah-Jibril/MarsPath_Hackathon/releases/tag/eva-physics-v1-2026-11-14) for matching Windows and Quest packages. The migration release and its checksums remain historical records.

## Code map

Paths start at `MarsPathUnity/Assets`. Where a row lists additional filenames, they share the first file's directory.

| Area | Entry points |
| --- | --- |
| Startup / screen flow | `Scripts/Core/MarsPathDemoBootstrap.cs`, `MarsRegionalMissionFlow.cs` |
| Routing / constraints | `Scripts/Data/MarsGraphRouting.cs`, `MarsRoutePlanner.cs`, `MarsPathModels.cs` |
| EVA physics / source warnings | `Scripts/Data/MarsEvaPhysics.cs`, `MarsHazardPhysics.cs`, `MarsRouteFacts.cs`, `Scripts/Core/MarsEvaSimulation.cs` |
| Mission zone | `Scripts/Data/MarsMissionZone.cs`, `Resources/NASA/MissionZone/` |
| Terrain / imagery providers | `Scripts/Data/MarsRegionalTerrain.cs`, `MarsOfficialTerrain.cs`, `MarsCtxImageryCache.cs`, `MarsRegionSourceCache.cs` |
| Terrain / rocks / arrows | `Scripts/Visualization/MarsTerrainGenerator.cs`, `MarsSurfaceRockScatter.cs`, `MarsPathRouteView.cs` |
| Quest endpoints | `Scripts/Visualization/MarsQuestGlobe.cs`, `MarsQuestPanel.cs`, `Resources/MarsPathQuestUI.shader` |
| Quest route selection | `Scripts/Visualization/MarsQuestPlanner.cs` |
| Head tracking / camera | `Scripts/Visualization/MarsQuestHeadTracking.cs`, `MarsPathCameraRig.cs` |
| Desktop HUD / warnings | `Scripts/Core/MarsPresentationInterface.cs`, `MarsVisorControls.cs`, `MarsHazardIcons.cs` |
| Showcase / movement | `Scripts/Core/MarsJezeroShowcase.cs`, `MarsDemoDirector.cs`, `MarsFullJourney.cs`, `MarsQuestMission.cs` |
| Radio / UI | `Scripts/Core/MarsMissionRadio.cs`, `MarsMissionRadioInterface.cs` |
| Transport / pairing | `Scripts/Core/MarsRadioUsbBridge.cs`, `MarsRadioDiscovery.cs`, `MarsRadioPairing.cs` |
| Suit / footsteps | `Scripts/Core/MarsEvaSoundscape.cs` |
| Builds | `Editor/MarsPathProjectSetup.cs`, `MarsPathQuestBuild.cs` |
| Radio check | `Editor/MarsRadioRegressionChecks.cs` |

The latest zone-map crosshair is foreground UI above the map so aiming is visible before clicking. The globe menu also uses foreground UI to prevent controls disappearing inside the sphere. Terrain warnings are red, peripheral, and pulse every 1.8 seconds without disappearing.

### Speech clarity refinement

The user confirmed that laptop-to-Quest voice was audible and clear after switching to the native media output, then requested less residual hiss. Native Quest speech now has a second-order 90 Hz high-pass and 5.5 kHz low-pass. Frame-based receive gain retains the boost for quiet speech and reduces amplification of strong frames before the soft limiter. This refinement applies receive gain on both platforms. Automated frequency checks preserve 500 Hz speech energy and attenuate 20 Hz rumble and 14 kHz hiss. Use signed JNI byte arrays for PCM to avoid per-frame conversion warnings. On 15 November 2026 the user accepted the refreshed build with no audible echo at approximately 70% Quest speaker volume and nearby presentation spacing. See [the live audio verification](docs/LIVE_MISSION_AUDIO_VALIDATION.md) for software counters, queue limits, and the scope of that physical trial.

## Build and install

Clone with Git LFS and open the matching Unity version. Close any editor using the project before batch builds. From the repository root:

```powershell
powershell -File .\tools\prepare-pointer-radio-builds.ps1
```

This helper checks radio and builds both platforms. Override `-UnityEditor` on another computer. Add `-InstallAndLaunch` only when exactly one authorized Quest 3 is connected and installation is wanted.

For Quest alone:

```powershell
powershell -File .\tools\build-quest.ps1
```

The Quest script configures Android input and restores the desktop input setting. Output is `MarsPathUnity/Builds/Quest/MarsPath.apk`, using OpenXR, IL2CPP, ARM64, and Vulkan.

The Windows editor method is `MarsPath.Editor.MarsRadioRegressionChecks.CheckAndBuildWindows`. Output is `MarsPathUnity/Builds/Windows/MarsPath.exe` with adjacent folders.

For an APK update, use Unity Android SDK's `adb.exe`:

```powershell
adb devices -l
adb -d install --no-streaming -r .\MarsPathUnity\Builds\Quest\MarsPath.apk
```

Confirm **Success**. `-r` preserves application data when signatures match. The imported APK uses local debug signing; another computer's fresh keystore may require a planned reinstall or the original signing setup.

Builds are release assets, outside source Git. Extract the complete Windows archive to run it. The shortcut helper expects a locally built `MarsPathUnity/Builds/Windows/MarsPath.exe`. Preserve the third-party notices alongside distributed builds.

## Verification and open work

| Check | Imported baseline status |
| --- | --- |
| Windows and Quest builds | Built successfully before migration |
| Route dispatch, microphone selection, two-way playback, and one-speaker radio | Software regression passed |
| Installed Quest APK hash, permission, pairing, USB forwarding | Verified; latest APK installed and hash-matched on 15 November |
| Laptop-to-Quest speech at nearby presentation spacing | User confirmed clear speech without echo, approximately 70% Quest volume, on 15 November |
| Latest zone reticle | Earlier rendering check; separate physical acceptance |
| Belva movement, continuation, alternate route, stop-on-release | Earlier application regression passed |
| Quest globe controls at different zooms | Earlier rendering regression passed |
| Solver versus exhaustive small-graph search | Earlier 40-graph regression passed |
| Repository import / packages | Inventory and checksums in `docs/` |

Relevant checks:

- Unity menu **MarsPath → Check Two Way Radio**, or `MarsPath.Editor.MarsRadioRegressionChecks.Run`.
- Player `--mars-team-routing-check -batchmode -nographics`, marker `TEAM_ROUTING_PASSED`.
- `--mars-jezero-showcase-review`, marker `JEZERO_SHOWCASE_PASSED`.
- `--mars-quest-globe-ui-review`, marker `QUEST_GLOBE_UI_PASSED`.
- `--mars-jezero-audio-review` for sound behavior.

Rendering checks require a graphical Windows session. Historical checks describe the imported baseline. Rerun relevant checks after implementation changes. Repository packaging is separate from physical headset acceptance.

For terrain reports, collect `LastMissionScene.json` from persistent app data, under `AppData/LocalLow/LAGRANGE V/MarsPath` on the original Windows machine. Diagnostic runs use `LastDiagnosticScene.json`. Record coordinates, route, source spacing, and viewpoint.

Remaining work includes separate physical reticle acceptance, measured Quest performance, suit/risk calibration, and any external engine integration. Live radio acceptance for the tested presentation setup is recorded above.
