# General Mars terrain and desktop UI — 8 November 2026

This update targets the laptop application. The supplied team research and algorithm files were read; the existing A*, lexicographic Dijkstra and constrained multi-label routing implementations are unchanged.

## General terrain corrections

The global context and large route maps used `Mola16Elevation` directly, even when the shipped 64 ppd MOLA, resident regional HRSC/MOLA or local stereo measurements were available. This was a real data-selection bug: the old path discarded measured features. Context now samples the same finest resident measured provider hierarchy as the ground view. Physical heights remain 1:1. A wide-map geometry budget replaces the former forced coarse-height substitution; local inspection still uses the finer available sources.

Independent reads of the original PDS rasters at 19,747 positions around seven off-Jezero regions found differences between the old 16 ppd and resident 64 ppd paths reaching 825 m near Gale and 931 m near Valles Marineris. These are resolution-dependent differences, not measurement-accuracy claims. [Audit and original global-pixel reference](../GIS/GlobalCoverageChecks/README.md).

The walking horizon now gives nearby measured rims more samples within a fixed maximum of 192,721 context vertices. Its near-horizon normals use the source resolution at each position. It does not add procedural hills, turn a flat measured plain into a crater, or infer height from image brightness.

## Arbitrary-coordinate source refinement

Planning and walking now request compact native 200 m neighbourhoods from the official [USGS HRSC/MOLA global product](https://astrogeology.usgs.gov/search/map/mars_mgs_mola_mex_hrsc_blended_dem_global_200m), independently of slower local stereo downloads. The request is based on selected geographic points and route stations, not a list of landmark names. Stable snapped neighbourhoods are cache keys only; they never move the user's endpoints or astronaut.

Every completed official DEM range now survives cancellation. Header, offset and source-row records carry URL/range identity, exact lengths and SHA-256 validation; individual range payloads are at most 128 KiB. Later attempts resume completed work. The bounded range cache uses a 512 MiB cap. The first usable global data and navigation are local; additional sources refine in the background.

The 200 m product is a blend of HRSC and MOLA, including resampled MOLA where high-resolution HRSC is absent. A 200 m output grid is not independent 200 m measurements everywhere. HiRISE/CTX/HRSC stereo remains preferred where valid finer coverage exists. No source provides metre-scale terrain for the whole planet.

## Interface

- The globe's START and DESTINATION labels are compact rounded translucent pills with small circular pins, thin connectors, collision separation and viewport clamping. Coordinate entry and selected points are unchanged.
- The red-boxed source/elevation mini-profile panel is hidden from the default helmet and chase views. Source diagnostics remain available under Mission → Data & Mission Details.

## Native verification

The release build passed `GLOBAL_TERRAIN_PASSED` and `MARSPATH_RUNTIME_TEST_PASSED`, both exit code 0. The native review covers seven provider locations, three planning/visor scenes, compact endpoint collision/clamping, and dateline/near-antipodal routes. It retains the production slope constraint; the first canyon-wall diagnostic was rejected, so the explicitly logged nearby Valles test starts at −13.8595076, −59.2. User selections are never changed this way.

The 10,700 km case built in 1.150 s and read 35,670,016 MOLA bytes. The 58.4 km dateline case took 0.176 s. Cached/offline visor entry for the three regional scenes took 0.091–0.154 s in this run. These are measured machine-local results, not a guarantee of uncached internet download speed.

Up to 783 horizon posts per scene were checked against independent spherical-drop/source-height calculations; maximum render/source agreement error was 0.030 m. This verifies rendering consistency, not intrinsic DEM accuracy.

At the arbitrary Arabia point `24.8f,14.2f`, a deliberately interrupted live request resumed using 29 validated cached ranges and completed the native 200 m window. Runtime height −2090.714 m matched independent GDAL original-pixel interpolation −2090.7139304122175 m within 0.01 m. It improved the available local grid from 926 m to 200 m, without changing the requested coordinates.

Evidence: [compact endpoints](../outputs/MarsPathGlobalEndpointLayout.png), [Gale planner](../outputs/MarsPathGlobalTerrainPlanner0.png), [Valles visor with source panel hidden](../outputs/MarsPathGlobalTerrainVisor1.png), [arbitrary Arabia visor](../outputs/MarsPathGlobalTerrainVisor2.png), [native report](../outputs/GlobalTerrainReview.txt). The regional visual tests deliberately disable online refinement to verify the global/bundled baseline; normal operation continues loading finer imagery and elevations in the background.

Local logs: `work/global-terrain-20261108-release-build.log`, `work/global-terrain-20261108-release-native.log`, and `work/global-terrain-20261108-release-smoke.log`. Build entry: `MarsPathUnity/Builds/Windows/MarsPath.exe`.
