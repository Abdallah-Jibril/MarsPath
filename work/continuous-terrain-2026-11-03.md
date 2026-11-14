# Continuous regional terrain and compact alerts

User's reported endpoints: 25.33859 N, 78.30515 E → Jezero 18.46720 N, 77.42980 E. The existing start has USGS CTX stereo DTM coverage at about 20.09 m, not a metre-scale HiRISE DTM in the bundled official coverage catalog. Existing local source windows are reused; transitions remain local-first.

## Changes

- The projected regional analysis mesh now joins a measured surrounding mesh with 32 adaptive perimeter bands extending beyond the normal planning viewport. The exposed square skirt has been removed. Border vertices match exactly, materials are shared, and per-fragment inverse-geodesic image coordinates prevent radial stretching on wide outer bands. All displayed heights use the same elevation provider. Route framing uses less empty padding and background refinement preserves the camera target instead of snapping its height to a different point on the ground. The outer field remains a regional geodesic projection; projection scale/distortion increases away from the selected area.
- Source transitions at stereo-DTM nodata edges now blend over twelve source cells (minimum 80 m) instead of four. This reduces artificial steep boundaries when a coarse fallback differs in elevation from a local stereo solution. Original sample arrays, measured interior elevations and source units are unchanged.
- Ground scenes now activate geographically anchored illustrative rock models. Up to 224 pooled objects use twelve shared rock meshes and three materials, with object-space grain from NASA/JPL-Caltech/MSSS PIA20323. They refresh after 9 m of local movement; positions and shapes derive from geographic cell IDs, so they remain stable across focus movement/rebases. Models follow the measured surface and leave the walking corridor passable. They do not alter the DEM, collider, scientific route costs or mapped hazards. Individual rock positions and shapes are modeled, not measured from 20–926 m DEM cells. The source notice states this explicitly.
- Rock warnings query the currently visible modeled stones. Slope warnings continue to query rendered measured terrain. Both use compact stroke-drawn symbols and short text: ROCKS AHEAD / distance · KEEP CLEAR, or STEEP SLOPE / degrees · SLOW DOWN. Life-support and other warnings use the same compact card. The existing visor-safe HUD transform is preserved.

## Source limits

Official CTX stereo DTMs have roughly 20 m post spacing and nodata regions; matching orthoimages provide additional visual information. HiRISE DTMs generally provide 1–2 m posts in selected stereo coverage. Photographic grain is presentation material, not extra topographic measurements or proof of individual hazards.

- https://stac.astrogeology.usgs.gov/docs/data/mars/ctxdtms/
- https://www.uahirise.org/dtm/about.php
- `MarsPathUnity/Assets/Resources/NASA/CuriositySandstoneSource.txt`

## Verification

Native acceptance flag: `--mars-nov3-review` checks the exact reported region, continuous context object, measured mesh agreement, bounded rock counts, real visible-model proximity sensor, overlapping geographic rock stability, all demo stations, original destination, full mission checkpoints, and the separate bundled HiRISE example. Native captures are written as `MarsPathNovember*.png` in the app's persistent data folder. General runtime smoke checks remain separate.

Final native acceptance passed (exit 0), `work/nov3-terrain-final-native.log`. On this machine, the reported planner opened in 2.727 s and entered the visor in 0.676 s. It rendered 211 illustrative rocks, with 185 overlapping geographic IDs retaining positions within the 5 mm verification tolerance after a 10 m focus update. Source/mesh equality, the modeled-rock sensor, demo/full original destinations and the bundled HiRISE example passed. Model stability tolerance is an implementation check, not a claim of positional accuracy for real Mars rocks.

The full runtime smoke suite also passed (exit 0), `work/nov3-terrain-final-smoke.log`: manual/global/landmark input, five computed routes, polar/dateline cases, 361/7157 km regional views, reserve behavior, full journey, 120-second demo, sparse guidance, rigid backpack, and smooth 60 Hz late-route movement. Final output is `MarsPathUnity/Builds/Windows/MarsPath.exe`; native screenshots are in `outputs/MarsPath_November_*`.
