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

Reference Belva endpoints are approximately **18.475002° N, 77.377312° E** and **18.476877° N, 77.377708° E**. The reference Balanced track is approximately 144 m and includes measured ascent and descent.

The two-minute demo moves continuously from the selected start. It covers the short reference track or the first 192 m of a longer route, then offers continuation. Full mission mode exposes mission-time speeds ×1, ×30, and ×50. Avoid reintroducing unexplained jumps between route highlights.

The shortcut opens borderless fullscreen. Interface text is English. Coordinate parsing also accepts additional numeral forms.

## Controls

| Quest input | Global Mars / Jezero zone | Route comparison | Helmet mission |
| --- | --- | --- | --- |
| Right index trigger | Place endpoint / select UI | Select route / UI | Hold to walk; release to stop |
| Left index trigger | — | — | Hold to transmit voice |
| Left stick | Rotate globe / pan zone | Orbit terrain | — |
| Right stick | Up/down to zoom | Left/right pan; up/down zoom | — |
| Right A | Switch start/destination | Enter selected route | Continue / replay after arrival |
| Right B | Zone back to globe | Back to endpoints | Close radio panel / back to routes |
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
| Hold Y | Transmit during a mission |
| V | Switch visor/chase |
| Esc | Mission menu |
| Alt+F4 | Close application |

## Radio behavior

Both applications must be inside a mission. Their routes and demo/full choices may differ, and radio can work while walking is paused. Endpoint and route-comparison screens do not keep mission radio active.

Initial pairing is provisioned automatically over authorized USB. Windows prefers USB while attached, then attempts discovery of the paired headset over a compatible local network. USB needs no internet. Wi-Fi requires local device-to-device connectivity; institutional client isolation may block it despite working internet access.

| State | Meaning |
| --- | --- |
| USB READY / WI-FI READY | Connected transport |
| CONNECTING VIA USB / WI-FI | Setup in progress |
| Talking · amber | Local microphone is transmitting |
| Receiving · cyan | Incoming voice is playing |
| Waiting for link | Talk button held before connection |

Discovery uses UDP 42740. Quest listens on TCP 42741; USB forwarding uses desktop TCP 42739. Pairing material stays in local application data. Never commit pairing keys, credentials, or microphone recordings.

The current fix prefers the physical Windows microphone over virtual Quest Link inputs, keeps reception active while local push-to-talk is held, raises quiet received voice, and limits loud peaks. The software check reaches the encrypted receive/playback buffer; physical microphone capture and speaker audibility need live testing.

Desktop-to-headset **route dispatch** remains unimplemented. Voice connection is independent of route synchronization.

## Terrain contract

The active Jezero resources use the USGS Mars 2020 TRN HiRISE master DTM and matching orthomosaic. Approximately 100 km² are supported by a 1 m original height grid, 16 overlapping runtime height tiles, and a documented source-valid crop. The runtime sector package is approximately 547 MB.

The [sector audit](GIS/OfficialSurfaceSources/JezeroMissionZone10km/README.md) records exact bounds, affine transform, validity checks, source URLs, and hashes. Selected image crops reach 0.25 m. That does not make the DEM quarter-metre resolution or extend fine imagery to uncovered locations.

Planner and visor use the same geographic terrain and 1:1 vertical scale. Preserve measured heights, vertical reference consistency, and real data gaps. Mesh density, source post spacing, and source accuracy are separate quantities.

Rock meshes and placements are illustrative. Warnings count rendered obstacles ahead. O2, power, energy, risk, and science are prototype models requiring calibration. QGIS supports preparation and independent checks; Unity reads the prepared products through C# providers.

## Code map

Paths start at `MarsPathUnity/Assets`. Where a row lists additional filenames, they share the first file's directory.

| Area | Entry points |
| --- | --- |
| Startup / screen flow | `Scripts/Core/MarsPathDemoBootstrap.cs`, `MarsRegionalMissionFlow.cs` |
| Routing / constraints | `Scripts/Data/MarsGraphRouting.cs`, `MarsRoutePlanner.cs`, `MarsPathModels.cs` |
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
| Radio microphone choice, gain, encrypted two-way playback, reception while transmitting | Software regression passed |
| Installed Quest APK hash, permission, pairing, USB forwarding | Verified on 14 November |
| Latest zone reticle and laptop-to-Quest live voice | Awaiting latest physical user feedback |
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

Remaining work includes live radio/reticle confirmation, measured Quest performance, suit/risk calibration, optional desktop route dispatch, and any external engine integration. The owner will provide the next implementation task after this handoff.
