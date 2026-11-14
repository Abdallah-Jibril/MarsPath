# MarsPath Unity Application

MarsPath shares measured terrain and route planning between a Windows mission console and standalone Meta Quest 3.

## Open the project

1. Clone the parent repository with Git LFS and run `git lfs pull`.
2. Install Unity **6000.6.5f1**, Windows build support, and Android build support with SDK, NDK, and OpenJDK.
3. Add this `MarsPathUnity` folder to Unity Hub.
4. Open `Assets/Scenes/MarsPathDemo.unity` and press **Play**.

Packages include OpenXR **1.17.0** and Unity UI **2.6.0**. The exact dependency graph is in `Packages/manifest.json` and `Packages/packages-lock.json`.

## Build

From the parent repository root:

```powershell
powershell -File .\tools\prepare-pointer-radio-builds.ps1
```

The helper checks radio and builds Windows and Quest. Close any editor holding the project first. Override `-UnityEditor` when the installed editor path differs. For Quest alone:

```powershell
powershell -File .\tools\build-quest.ps1
```

| Target | Output |
| --- | --- |
| Windows | `MarsPathUnity/Builds/Windows/MarsPath.exe` and adjacent data |
| Quest | `MarsPathUnity/Builds/Quest/MarsPath.apk` |

Quest uses OpenXR, IL2CPP, ARM64, Vulkan, and local debug signing. Generated builds and Unity caches are excluded from version control.

## Reference workflow

Select **JEZERO HACKATHON DEMO**, compare routes, choose an alternative, then enter the helmet. **JEZERO 10 × 10 KM ZONE** supports free endpoints within measured coverage.

Planner and helmet share 1:1 vertical elevations. Native height spacing is 1 m; selected imagery reaches 0.25 m. Rocks are illustrative. See [Data Sources](../docs/DATA_SOURCES.md).

## Controls and validation

The [developer handoff](../HANDOFF.md) contains the full control table, USB/Wi-Fi voice setup, code map, installation commands, and acceptance status.

In a mission, hold keyboard **Y** on Windows or **left index trigger** on Quest to transmit. Hold **right index trigger** to walk and release to stop. Software receive/playback checks passed; latest physical reticle and downlink voice confirmation awaits feedback.

Read [Route Planning Algorithms](../docs/ALGORITHMS.md) for solver details. The parent GIS folder and adjacent runtime source notices preserve source attribution and audits.
