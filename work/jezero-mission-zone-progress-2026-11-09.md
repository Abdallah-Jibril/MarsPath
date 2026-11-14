# Jezero 10 × 10 km desktop release — 9 November 2026

The existing application now includes a validated, fully local Jezero sector with freely selected START and DESTINATION. This is an addition to the current planner, team algorithms and helmet, not a restart or a fixed-route video. Desktop acceptance remains the priority before any Quest transfer.

## Released area and source

- Selectable area: 9.999914 × 10.000000 km, about 100 km².
- Bounds: west 77.33578971367626° E, east 77.51365504968241° E, south 18.38445484384847° N, north 18.553160892429624° N.
- Source: official USGS Mars 2020 TRN HiRISE master DTM, native 1 m Float32 grid. All 105,851,280 original cropped samples are valid. No height upsampling, quantization or gap filling.
- Source height range: −2649.1907 to −2303.2661 m, approximately 345.9 m relief. Height scale remains physical 1:1.
- Matching co-registered photography: original 0.25 m source reduced to a declared 1 m display grid. All selected output samples are valid. The complete overview has a separately labelled 2.5791 m display grid.
- Runtime data: 547,309,809 bytes, sixteen overlapping height/image tiles and a whole-sector overview. The independent GeoTIFF/QGIS reference pack is about 232 MB. Combined permanent extra data are under 800 MB.
- The official master differs from older individually registered strips. Its geometry and matched imagery are authoritative throughout this sector. Legacy global and standalone HiRISE modes keep their previous sources.

Original metadata, hashes, complete native validity checks, bitwise tile comparisons, image processing and producer quality caveats are in [the source pack](../GIS/OfficialSurfaceSources/JezeroMissionZone10km/README.md). [Portable QGIS project](../GIS/OfficialSurfaceSources/JezeroMissionZone10km/Jezero10kmReference.qgz). The earlier 4 × 6 km reference pack is preserved under GIS/OfficialSurfaceSources/JezeroMissionZone; unreferenced runtime duplicates were removed.

## User flow

Launch MarsPathUnity/Builds/Windows/MarsPath.exe. From Global Mars, choose **JEZERO 10 × 10 KM ZONE**. Choose either endpoint by clicking the map or entering latitude/longitude, then choose **VIEW ROUTES** and a route. **ENTER HELMET DEMO** runs the 120-second presentation; **FULL MISSION** retains normal mission-time playback and the existing controls.

The local map uses the full-height viewport, with compact coordinate cards on the left, small 📍 markers, zoom, pan and fit. Its orientation is explicitly north-right/east-down; the cached image rotation, pins and inverse mouse picks share the same mapping. Opening the sector preserves existing coordinates. Outside-sector input is rejected without silently moving the point.

Some endpoint pairs can have no feasible modeled path under the selected slope/resource limits. The routing graph is sampled, and individual small rock models remain illustrative. Source grid spacing is not a claim of absolute survey accuracy.

## Implementation

- MarsMissionZone reads the region metadata, checks actual source support and exposes registered image coverage. Heights load lazily from local resources; mission tiles do not evict the existing global source cache.
- MissionZoneId travels with routing options. Graph nodes, dense edge checks and modeled science candidates remain inside valid sector coverage. Existing A*, lexicographic Dijkstra and constrained multi-label routing are retained.
- Planning, helmet entry and walking use local sector sources instead of starting network refinement. The complete overview sits behind the native foreground image tiles, so tile transitions retain registered sector imagery.
- Geographic elevation, world-space height, source labels and uncertainty checks use the same authoritative master sampler inside the sector. This prevents an older cached individual strip from changing the ground after a seek or rebase.
- Surface patches preserve routing options. The default helmet keeps the previously requested compact HUD; the local scene evidence record includes the mission-zone identity.

## Final desktop verification

All final checks exited 0:

| Check | Verified result |
| --- | --- |
| Windows build | work/jezero-10km-final-build.log |
| Independent GDAL source oracle | Five source samples, maximum application difference 0.000121 m |
| Sector grid check | 900/900 sampled interior points use the real measured source |
| Wide route | 9.273 km, 17,638 intermediate/waypoint containment checks, original endpoints retained |
| Short route | 2.562 km, 5,176 containment checks, original endpoints retained |
| Planning time in local review | 0.829 s wide; 0.382 s short |
| Initial local height load | 1.983 s on this laptop in the review |
| Planning ↔ helmet | Identical sampled station elevations; both actual movement and forward arrows passed |
| Demo/full playback | Same geographic track and physical source at tested stations |
| Legacy/global regression suite | MARSPATH_RUNTIME_TEST_PASSED |
| Original standalone HiRISE | HIRISE_NATIVE_REVIEW_PASSED, original native photo/scarp retained |

The native mission-zone review ends with MISSION_ZONE_PASSED in work/jezero-10km-final-review.log. The other final logs are work/jezero-10km-final-smoke.log and work/jezero-10km-final-legacy-hirise.log. Machine-local logs and build binaries are intentionally not committed. Source agreement and local timings are not absolute terrain-accuracy or cold-device performance guarantees.

Actual screenshots: [selector](../outputs/MarsPathJezero10kmSelector.png), [wide planner](../outputs/MarsPathJezero10kmPlanner.png), [wide visor](../outputs/MarsPathJezero10kmVisor.png), [short visor](../outputs/MarsPathJezero10kmShortVisor.png).

## Reproduce and continue

The branch is codex/measured-terrain-team-routing. Use Git LFS pull after cloning; new Resources/NASA/MissionZone binary assets are covered by LFS. Rebuild Windows with the existing MarsPathProjectSetup.BuildWindowsPrototype method.

Data preparation and independent QGIS verification scripts are tools/prepare_jezero_10km_zone.py and tools/verify_jezero_10km_zone.py. Their original-source stripe checkpoints allow downloads to resume. The native acceptance flag is --mars-mission-zone-review.

Next acceptance is the user's laptop review of freely selected points and the helmet. Quest export/hardware validation remains separate pending desktop acceptance.
