# MarsPath

### Plan. Survive. Explore.

**LAGRANGE V · NASA Space Apps Challenge prototype**

MarsPath connects Mars mission planning on a laptop with an astronaut's first-person navigation experience on Meta Quest 3. Choose a start and destination, compare terrain-aware routes, and explore the selected track through a helmet HUD and controller-driven movement.

The detailed demonstration covers a measured **10 × 10 km region of Jezero Crater** and a short **Belva Crater showcase**. The planner and helmet use the same geographic coordinates and elevation sources.

[Submission resources](resources/README.md) · [Watch the 20-second English demo](resources/MarsPath_20s_Live_Quest_Demo_EN.mp4) · [Run the demo](#run-the-demo) · [Algorithms](docs/ALGORITHMS.md) · [EVA equations and warnings](docs/PHYSICS_AND_HAZARDS.md) · [NASA and planetary data](docs/DATA_SOURCES.md)

## Submission materials

| Material | Location |
| --- | --- |
| Application source, scenes, and runtime data | [MarsPathUnity](MarsPathUnity/) |
| Demo video with English narration | [20-second live Quest walkthrough](resources/MarsPath_20s_Live_Quest_Demo_EN.mp4) |
| Ready-to-run builds | [Windows ZIP and Quest APK](https://github.com/Abdallah-Jibril/MarsPath/releases/tag/hackathon-demo-2026-11-15) |
| Team presentation | Awaiting the final team deck, to be added to `resources/` |
| Planetary data and source references | [GIS datasets](GIS/) and [data attribution](docs/DATA_SOURCES.md) |
| Algorithms and engineering methods | [Route searches](docs/ALGORITHMS.md) and [EVA calculations](docs/PHYSICS_AND_HAZARDS.md) |

[Resources and submission links](resources/README.md)

## The challenge

Future Mars explorers need to understand the terrain between a habitat and a science destination. Distance alone cannot describe the trade-offs between slope, traversability, estimated energy use, and science opportunities.

MarsPath is an independent prototype for **Interplanetary Survival Guide: Martian Map**. It combines planetary elevation products, orbital imagery, route search, and immersive navigation. Its outputs require further validation before operational use.

## Explore the application

| Experience | Capabilities |
| --- | --- |
| **Global Mars** | Rotate and zoom the globe, explore landmarks, enter coordinates, or place endpoint pins. |
| **Jezero mission zone** | Choose endpoints freely inside the supported 10 × 10 km region. |
| **Route comparison** | Inspect alternatives, route length, estimated time, sampled ascent/descent, maximum track grade, and an elevation profile. |
| **Helmet navigation** | Follow surface arrows, look around with tracked head movement, and view compact terrain warnings. |
| **Mission Control** | Send the selected route to Quest, mirror its live progress and head pose on the laptop, and use two-way push-to-talk. |
| **Desktop presentation** | Demonstrate the planner and helmet view without a headset, in borderless fullscreen. |

The showcase button opens route comparison so the user chooses a route before starting. The two-minute demonstration follows the complete selected Jezero route in 120 seconds of walking. The countdown pauses with the astronaut and resumes from the same point. Longer routes outside the measured sector support a continuous preview followed by optional continuation.

## Route algorithms

| Route | Implemented search |
| --- | --- |
| **Balanced** | A* with distance and integrated modeled risk. |
| **Safest** | Lexicographic Dijkstra, prioritizing risk and then distance. |
| **Fastest** | A* minimizing three-dimensional travel distance. |
| **Science priority** | Stage-expanded A* through ordered science stops. |
| **Energy efficient** | A* minimizing estimated energy. |
| **Resource-constrained Balanced** | Multi-label A* for RCSP, with Pareto-dominance and lower-bound pruning. |

The implemented engine is **C# inside Unity**. A separate C++ service remains a proposed integration. See [algorithm definitions, constraints, and validation](docs/ALGORITHMS.md).

## Terrain and scientific scope

- The Jezero zone uses the **USGS Mars 2020 TRN HiRISE DTM**, with a native **1 m height grid** and a matching registered orthomosaic.
- Selected Belva and western-delta image windows retain **0.25 m imagery**. The wider zone uses 1 m near imagery and a separate overview.
- Elevations use **1:1 physical vertical scale**. Rendered mesh spacing changes with distance and device capability.
- Global context and regional fallbacks use **MOLA, HRSC/MOLA, HRSC, and CTX** products as available. Fine stereo coverage is geographically limited.
- QGIS preparation and independent checks retain source registration, valid-data masks, affine transforms, and elevation reference points.

**Grid spacing is not absolute measurement accuracy.** Stereo artifacts and registration limitations remain part of the source products. Small rock meshes and their placement are illustrative. Suit oxygen, energy, risk, and science scores are prototype estimates. Warm surface coloration is a display treatment of orbital imagery.

See [data sources and attribution](docs/DATA_SOURCES.md) and the [Jezero coverage audit](GIS/OfficialSurfaceSources/JezeroMissionZone10km/README.md).

### Oxygen, electrical use, and warnings

The shared engineering model separates body metabolism from suit electricity. Signed DEM grades drive gravitational work, the Weir calorimetry conversion estimates oxygen demand, and explicit electrical loads deplete a configured battery. Route scores do not feed into physiology. The parameters distinguish reference values from concept-suit assumptions; no calibrated Mars-human performance or thermal-safety claim is made.

Slope warnings use source-elevation gradients and a declared 10° caution policy. Rock warnings count visible rendered models in a forward corridor; individual rock locations remain illustrative. See [equations, units, examples, sources, and limitations](docs/PHYSICS_AND_HAZARDS.md).

## Getting started

### Run the current version

Download the [15 November Windows and Quest builds](https://github.com/Abdallah-Jibril/MarsPath/releases/tag/hackathon-demo-2026-11-15), or build both apps from source with the instructions below. The [current build manifest](resources/BUILD_MANIFEST.json) records package sizes and SHA-256 checksums.

- **Windows** opens `MarsPathUnity/Builds/Windows/MarsPath.exe` in borderless fullscreen. Keep the executable, data folder, and libraries together. Press `Alt+F4` to close.
- **Quest 3** requires Developer Mode and authorized USB debugging for APK installation. Open MarsPath from the headset application library after installation. [Installation steps](HANDOFF.md#build-and-install)

### Build from source

Install Git LFS before cloning. Use **Unity 6000.6.5f1** with Windows build support and Android build support, including SDK, NDK, and OpenJDK.

```powershell
git lfs install
git clone https://github.com/Abdallah-Jibril/MarsPath.git
Set-Location MarsPath
git lfs pull
```

Open `MarsPathUnity` in Unity Hub and load `Assets/Scenes/MarsPathDemo.unity` to inspect the project. Close the editor before running the build helper from the repository root.

```powershell
powershell -File .\tools\prepare-pointer-radio-builds.ps1
```

The helper checks the radio and builds Windows and Quest. The Quest output is `MarsPathUnity/Builds/Quest/MarsPath.apk`. Use the [build and install guide](HANDOFF.md#build-and-install) for the USB installation command and editor-path override.

## Run the demo

1. Open MarsPath on Quest and leave it on **Waiting for Mission Control**. Pair the laptop and Quest once over authorized USB. For a wireless session, put both devices on a local Wi-Fi network that permits device-to-device communication.
2. On the laptop, select **Jezero Hackathon Demo** for the Belva preset or **Jezero 10 × 10 km Zone** to choose both endpoints. Compare the alternatives and select a route.
3. Once the comparison screen shows **Quest Waiting**, choose **2 Min Demo** or **Full Mission** and click **Enter Helmet Demo** or **Start Full Mission**. Quest loads the selected route and the laptop joins its live view at zero.
4. On Quest, look toward the route arrows and hold the **right index trigger** to walk. Release it to stop. Turn your head to look around. The laptop mirrors heading, movement, distance, modeled suit reserves, and alerts. The trigger stays locked while the astronaut faces away from the route.
5. On the laptop, use **Browse Routes** to inspect alternatives during the traverse and **Back to Live** to return to the astronaut's current view. On Quest, **B** ends the dispatched mission and returns to the lobby for another route.

| Control | Action |
| --- | --- |
| Laptop **Y**, Quest **left index trigger** | Hold to talk, release to listen. Use the radio from laptop route comparison and the Quest lobby, or during the traverse. |
| Laptop **D**, **V**, **Esc** | Open live details, switch visor/chase view, or open the mission menu. |
| Left-drag, right-drag, mouse wheel | Pan, orbit, and zoom the laptop route map. |
| Quest **A** in the lobby | Open the standalone Quest planner to choose endpoints and routes in the headset. |
| Laptop **Laptop Preview** | Inspect the selected route from ground level on Windows. |

Jezero terrain and imagery are bundled with both apps. USB carries a paired session offline. Local Wi-Fi carries the route, telemetry, and radio without downloading Jezero terrain. A campus network must allow communication between the laptop and headset. [Full controls and connection states](HANDOFF.md#mission-control-and-radio)

## Project layout

```text
MarsPathUnity/       Unity runtime, Quest UI, scenes, assets, build methods
GIS/                QGIS projects, source provenance, native terrain references
tools/              Data preparation, verification, build and install helpers
docs/               Algorithms, equations, source attribution, validation records
resources/          Submission demo and final team presentation
```

## Technology

| Layer | Implementation |
| --- | --- |
| Application | Unity 6000.6.5f1, C# |
| XR | OpenXR 1.17.0, Quest 3 controllers and head tracking |
| Quest build | Android, IL2CPP, ARM64, Vulkan |
| Interface | Unity UI 2.6.0, desktop planning, world-space Quest panels |
| GIS preparation | QGIS and Python for DEM, slope, imagery, and source audits |
| Voice | Local encrypted audio, USB forwarding, Wi-Fi discovery |
| Large assets | Git LFS and documented source products |

## Validation and current limits

The software checks cover balance and geometry, graph search against exhaustive reference cases, radio, and live synchronization. The Belva runtime check verifies arrival after 120 seconds of active walking, the selected coordinate, and measured surface elevation. [Physics verification](docs/PHYSICS_VALIDATION.md) · [Live mission and voice verification](docs/LIVE_MISSION_AUDIO_VALIDATION.md)

### Quest 3 demonstration timings

On 15 November 2026, both rebuilt versions ran together over local Wi-Fi. Times start at the **Enter Helmet Demo** click with the headset already waiting.

| Trial | Quest ready | Laptop live view ready |
| --- | ---: | ---: |
| Belva Balanced, 202 m | 2.55 s | 3.64 s |
| Jezero zone Science priority, 5.51 km | 2.47 s | 3.54 s |

The headset prepares bundled Jezero data while it waits. These measurements describe two runs on the tested laptop, Quest 3, and local network. Route and suit estimates still require empirical validation for real EVA planning. [Test method and scope](docs/LIVE_MISSION_AUDIO_VALIDATION.md#15-november-faster-mission-entry-over-local-wi-fi)

Repository and package checks are recorded in [Migration Validation](docs/VALIDATION.md).

## Team and credits

**LAGRANGE V**

| Team member | Project responsibility |
| --- | --- |
| Abd-Al Salam Dweikat | Data resources and collection |
| Asma Nairat | Data resources and collection |
| Aram Odeh | Algorithm and data analysis research |
| Haneen Turkieh | Algorithm and data analysis research |
| Abdallah Jibril | Desktop application and Meta Quest 3 implementation |

Data and imagery are credited to their producers, including NASA mission teams, JPL-Caltech, USGS Astrogeology, HiRISE, ASU, MSSS, ESA/DLR, and the PDS Geosciences Node where applicable. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

This repository was initialized on **14 November 2026** by importing an existing MarsPath codebase and data package. Import commits record repository organization and packaging. See [migration provenance](docs/MIGRATION.md).
