# Terrain rendering, route UI and live facts

## Delivered behavior

- Planner: numbered color-coded route cards, selection marker, compact model scores, geographic endpoints, four selected-route metrics, interactive elevation profile, highlighted helmet entry, and a source evidence sheet.
- Visor: bracketed translucent panels, heading tape, distance and remaining ETA, modeled suit reserves, local measured elevation and grid spacing. Pause, View and Mission remain the only persistent controls. Secondary controls and the timeline remain collapsible and inside the visor-safe area.
- Show Details: per-route sampled minimum/maximum areoid elevations, ascent/descent, along-track grade, sampling step, source grid range and length-weighted source coverage. Suit demand, time, risk and science candidates remain labeled as models. Equal values for identical geographic tracks are allowed; numbers are not randomized to force differences.
- Live details: selected route identity, traversed/remaining length, remaining ETA, measured position/elevation, local along-track grade, route grade, ascent/descent and current modeled effort.

## Ground rendering

The measured mesh and collider elevations were not displaced. Added only optical grain roughness, stronger registered-image contrast, stable geographic material coordinates across 100 m rebases, consistent near/context normals and clearer angular decorative rocks near the passable walking corridor. Existing NASA photographic grain is an illustrative material, not a recovered rock inventory. Reduced ambient fill improves shadow contact; the default 1.70 m visor eye now looks 3 degrees down with a 66-degree FOV.

Arbitrary 75–926 m elevation grids cannot resolve metre-scale real craters. The bundled 1.01655 m HiRISE example remains available through the globe's HIRISE / 1 m TERRAIN EXAMPLE button. No new source coverage is claimed. HiRISE stereo coverage and grid interpretation: https://www.uahirise.org/dtm/about.php . CTX grids and actual nodata holes: https://stac.astrogeology.usgs.gov/docs/data/mars/ctxdtms/ .

## Correctness fixes

Constructing a geographic elevation profile previously switched the global terrain frame to the planning mission. The constructor now restores the active walking frame, so opening details cannot change the ground. Profile caches invalidate when source revisions change. Sampling is bounded to 257–1,025 geographic stations; long-route maxima/ascent may be underestimated between stations and are identified as sampled estimates.

Timeline seeks now reconstruct modeled suit consumption over the traversed portion of the current resupply leg using bounded sampling of actual along-track heights and the route's estimated pace. Live effort is refreshed at the destination station after a jump instead of retaining a prior station's grade or a hardcoded 0.7 m/s pace.

## Verification

Build: `work/nov4-experience-build.log`, exit 0, no shader/compiler errors. Updated `Assembly-CSharp.dll` built 2026-11-04. Native screenshots, not mockups, saved in `outputs/MarsPathExperience*.png`.

- Native `--mars-experience-review`, exit 0: HiRISE planner 1.345 s, visor entry 0.985 s. Five-profile route facts, all eight demo stations, source/mesh equality, details frame isolation, full playback and another 398 km region passed.
- `--mars-route-facts-check`, exit 0: source samples, cache identity, coverage shares total 100%, ascent-minus-descent conservation, frame isolation and region-specific facts.
- Runtime smoke, exit 0: coordinate selection, dateline/polar routing, walking, full journey, suit reserve bounds, arrows, 120-second demo and camera framing.
- `--mars-fast-terrain-review`, exit 0: four-region headless transitions 0.11–0.80 s for planning, 0.31–0.99 s for visor; original MOLA quadrant/pole samples unchanged.
- `--mars-accuracy-ux-review`, exit 0: near-antipodal/east-west routes bounded, actual endpoints preserved, source mesh equality and menu behavior preserved.

Actual HiRISE profile differences from the acceptance run:

| Profile | Route length | Sampled ascent | Sampled descent | Maximum along-track grade |
|---|---:|---:|---:|---:|
| Balanced | 0.519 km | 64.1 m | 7.8 m | 23.13 degrees |
| Safest | 0.529 km | 63.8 m | 7.5 m | 22.55 degrees |
| Fastest | 0.216 km | 56.3 m | 0.0 m | 27.43 degrees |
| Science | 0.325 km | 61.9 m | 5.6 m | 40.98 degrees |
| Energy | 0.522 km | 59.6 m | 3.3 m | 22.31 degrees |

The sampled science grade above 35 degrees demonstrates that a feasible coarse A* grid corridor does not certify EVA safety; fine hazards and operations remain unvalidated. The UI presents the actual statistic, not a lowered number.
