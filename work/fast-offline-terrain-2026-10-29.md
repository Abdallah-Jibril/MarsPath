# Local-first measured terrain and transition performance

The foreground global → planner and planner → visor transitions now use only bundled data and existing disk cache. They contain no network preparation calls. Previously the transition waited serially for HRSC queries, a regional crop, stereo windows and eight demo stations before entering the visor. Those downloads now refine an already usable view in cancellable background work.

## Scientific coverage

- Full NASA MGS MOLA MEGDR 64 pixels/degree topography is bundled in four original SignedMSB2 rasters. Approximately 926 m post spacing at the equator, four times finer linearly than the old 16 ppd fallback. This is gridded/interpolated altimetry, not metre-scale global terrain. The original full files occupy 530,841,600 bytes; a bounded page/band cache reads them without allocating the complete raster in RAM.
- Source labels and download hashes: `MarsPathUnity/Assets/StreamingAssets/Mola64/sources.json` and `work/Mola64-label.xml`. Maintained NASA PDS4 archive: https://pds-geosciences.wustl.edu/missions/mgs/megdr.html . Labels specify pixel centres, 11,520 columns × 5,760 rows per quadrant, east-positive longitude, areoid heights and signed big-endian 16-bit samples.
- Eleven independent direct-file golden samples verify all four quadrants, equator, antimeridian and poles. `work/mola64-golden.cjs` reads exact original byte offsets independently from the Unity paged decoder. Expected-versus-runtime error tolerance is 1 cm, reflecting interpolation arithmetic rather than claiming source accuracy.
- Existing original HRSC 75–175 m strips, four 200 m crops, expanded Jezero CTX 20 m grid, and bundled 1.01655 m HiRISE stereo crop remain preferred where covered. The finer global MOLA cannot be overridden by a coarser 1,000–3,200 m online overview.
- The global screen offers **HIRISE / 1 m TERRAIN EXAMPLE**. It selects a real measured ridge approach at 18.46470 N, 77.42535 E to 18.46820 N, 77.42534 E; five routes use the actual heights and the existing matching orthoimage. The local 360 m square spans about 80 m of measured height. Other arbitrary coordinates do not inherit this ridge or its resolution.

## Rendering and latency changes

- Network STAC catalog parsing is removed from local cache restoration. A small local metadata index identifies already-downloaded windows. The 45,480-entry catalog is needed only for optional background discovery.
- Near meshes use 385² vertices for HiRISE, 193² for ≤25 m grids and 97² for coarser sources. Regular metre grids replace the former sub-decimetre central warp, which oversampled coarse data and amplified floating-point shading stripes. Planning meshes cap at 513².
- A 24 km Cartesian context uses the same measured elevation provider. Ground stays at physical 1:1 scale. Labelled 1×/3×/6× controls exaggerate only the planner's vertical display.
- Matching HiRISE orthoimagery activates locally before building its planner. The RELIEF layer now retains restrained source-image detail alongside geometric lighting, rather than removing all mapped tonal features. Ground albedo is warm ochre/brown, with registered orbital contrasts. NASA photographic high-pass material grain adds close surface texture only; it is illustrative, does not displace heights, and creates no claimed mapped rock hazards.
- Planning refinement has a bounded lifetime and is cancelled on entry to the visor or return to the globe. A completed update recomputes mesh/costs/profile together while preserving the selected profile and camera pose. Surface refinement works during playback and prefetches the next demo station. A remote prefetch does not rebuild the current ground unless its measured source footprint actually changes.
- All route endpoints and the 120-second/eight-highlight presentation remain intact. Full playback still offers 1×/30×/50×; demo hides these controls. Source resolution and measured relief vary along each journey.

## Verification

Run `MarsPath.exe --mars-fast-terrain-review -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile <log>` for native captures and timed production transitions. The first reported-route run ignores persistent cache without deleting or renaming user files. It still uses bundled official data. Measurements exclude screenshot delays and are specific to this machine. Results are written to `%USERPROFILE%/AppData/LocalLow/LAGRANGE V/MarsPath/MarsPathFastTerrainMetrics.json`.

The regression checks actual planner/visor methods, geographic endpoints, five profiles, source/mesh agreement, measured HiRISE relief, sparse arrows, all eight station rebuilds and original route completion. General runtime smoke coverage also verifies manual/globe/landmark input, dateline/poles, resource model, smooth late-route movement and controls.

Verified release measurements (native 1600×900 rendering on this machine, 2026-10-29; screenshot waits excluded):

| Scenario | Open planner | Enter visor | Slowest demo station rebuild |
|---|---:|---:|---:|
| Reported ~400 km route, persistent cache ignored | 0.630 s | 0.173 s | 0.109 s |
| Same route, warm/local cache | 0.717 s | 0.431 s | 0.314 s |
| Valles Marineris, another region | 0.169 s | 0.162 s | 0.085 s |
| Bundled HiRISE 1 m ridge | 0.696 s | 0.804 s | 0.792 s |

The additional network-enabled test entered the visor in **0.679 s while optional refinement was running**, then verified source/mesh agreement and retained location after ten seconds of background activity. Both native and headless transition checks exited 0. Logs: `work/fast-terrain-verified-native.log`, `work/fast-terrain-verified-headless.log`; exact metrics: `work/fast-terrain-native-metrics.json`. The reported route has 10.7 m of measured height variation across its near 360 m square; Valles 61.8 m; the HiRISE example 80.1 m. Native captures are saved in `outputs/MarsPath_Fast_*`.

The full release runtime smoke check also exited 0 (`work/fast-terrain-runtime-smoke.log`): global/manual/landmark flow, five profiles, polar/dateline coordinates, 361/7,157 km projected maps, full route endpoint, resources, 120-second demo, backpack/gait, and late-route 60 Hz movement. The tested long-route walk moved 6.40 m in eight simulated real seconds with 3–6 visible forward arrows each second.

Metre-scale detail is available only within measured stereo coverage. A low-relief region in a 75–926 m grid can remain smooth at eye level; distant relief must not be fabricated into local cliffs or boulders. The desktop visor and on-device Quest/OpenXR remain separate deliverables.
