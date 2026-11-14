# Reported crater route, geographic context and illustrated warnings

7 November 2026. User's exact selection: start **26.73911 N, 82.70998 E**, destination **24.24785 N, 84.94257 E**. The computed Balanced track is about 199.59 km.

## Source diagnosis

Original HRSC H2948 (75 m) gives the start elevation −1729.245914 m, matching the visor. Relief spans about 10.05 m over its surrounding 360×360 m and 29.73 m over 1×1 km. The known CTX floor reference at 25.2272575 N, 83.77844375 E is 106.16 km away. A first-person view at 0% of the route therefore cannot show the full crater floor seen in an orbital overview. The original start is preserved. Raw source probes and provenance are in `reported199-source-audit.json`; these direct-corridor probes are distinguished from the actual computed Balanced route.

## Changes

- A bounded adaptive mesh replaces uniform regional rendering for regions below 1520 km. Shared-edge triangulation is crack-free, original outer edges match the context, and every vertex uses the unchanged physical source-height provider. All five route polylines receive a mandatory narrow refinement corridor before remaining detail is allocated by measured height interpolation error. Limits are 16,000 leaves, 70,000 vertices and 100,000 samples. Local inspection remains available for finer source-dependent detail.
- Route lines/arrows drape over the visible adaptive and focus triangles. The initial camera aligns the journey left to right while retaining physical 1:1 heights. Fit View restores that frame; legacy startup data retains its original camera initialization.
- Clicking the planning elevation profile focuses its actual geographic station. Inspecting terrain synchronizes the nearest route station with ground preview. Normal mission entry explicitly begins at the selected start.
- The visor's existing source card now contains the same measured route profile, a current-position marker, coordinate readout, and separate source/image spacings. Clicking it pauses and seeks the actual route at ×1. Full details collapse this mini-profile to avoid overlapping panels. Profile regeneration restores the metre-based surface frame.
- Rock and slope warnings now show 72 px raster illustrations with one heading and a short distance/grade. Their sensor logic is unchanged. The atlas is a generated UI illustration, not NASA data or mapped obstacle evidence.

## Native verification

The exact route was checked in the Windows release. Four actual computed-track stations were compared between planner and visor: 0% at −1729.243 m, 25.6% descent at −1284.339 m, 60.7% floor at −3725.123 m, and 74.9% ascent at −1698.047 m. The source provider agreed at the identical geographic points; source-aware ground meshes and spherical horizon checks passed. The visible map interpolation errors at these stations were about 0.96, 6.17, 1.56 and 0.72 m. Numerical agreement with source data does not establish survey accuracy or metre-scale global coverage.

The final adaptive case used 34,315 vertices and 68,629 samples, built the regional view in about 1.67 s with the current cache, had zero unmatched interior triangle edges, and zero sampled vertex/source height error. Flat areas can use larger cells while retaining small height interpolation error; source spacing, image pixels, mesh cells and measurement accuracy are separate. The acceptance check uses a direct 10 m map-height error bound at these checked stations rather than an arbitrary uniform mesh-density threshold. Headless regression checks also cover startup, short/continental/dateline/polar routes, framing, original endpoints, smooth walking and guidance. Native flag: `--mars-reported199-review`.

Screenshots in `outputs/MarsPathReported199*.png` include the actual descent, floor and a two-minute demo station. `SlopeWarningUI` is an explicitly forced UI diagnostic, not a measured hazard assertion. Individual scattered small rocks remain illustrative.

## Generated warning asset

Saved atlas: `MarsPathUnity/Assets/Resources/NASA/HudHazardAtlas.png` (original alpha preserved). Integration uses non-destructive UV regions and an alpha-aware Unity import. Mode: built-in `image_gen` via the imagegen skill. Exact generation prompt:

> Use case: game UI illustration. Create a single square 1024x1024 transparent PNG icon atlas for a professional MarsPath astronaut helmet hazard HUD. It contains exactly TWO separate, isolated, photorealistic miniature terrain cutouts aligned horizontally: in the LEFT HALF, three sharply detailed rugged Martian basalt boulders with dusty warm rust/gray surfaces and convincing chipped rock edges, different sizes; in the RIGHT HALF, a clear miniature cross-sectional slice of a steep Martian rocky escarpment and loose scree with a visibly sharp downhill drop, readable as a slope warning. Large clean silhouettes, three-quarter view, neutral frontal studio-style illumination, rich midtones, no black shadows that obscure silhouette. Keep each illustration fully within its own half with generous transparent padding. Absolutely no lettering, no numbers, no warning triangles, no frames, no UI panels, no terrain background, no sky, no checkerboard. These two readable raster illustrations will be cropped into two 64px warning thumbnails on a dark helmet interface. Use genuine alpha transparency outside the objects. Do not include astronauts or any logo.
