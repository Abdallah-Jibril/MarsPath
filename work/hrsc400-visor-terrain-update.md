# Reported 400 km route and visor update

User endpoints: 19.37447 N / 67.33276 E to 24.83584 N / 64.00996 E. The five complete routes and original endpoints are retained.

## New actual elevation source

The NASA PDS Orbital Data Explorer search returned original ESA Mars Express HRSC DA4 areoid products intersecting the region. Four finer-than-200-m strips are bundled unchanged, including their original PDS labels and big-endian signed Int16 samples:

| Product | Grid | Native shape |
| --- | --- | --- |
| H3124_0000_DA4.IMG | 75 m | 901 x 5050 |
| H3135_0000_DA4.IMG | 75 m | 885 x 4900 |
| H1402_0009_DA4.IMG | 175 m | 987 x 1901 |
| H5173_0000_DA4.IMG | 75 m | 1261 x 14684 |

H1640_0000_DA4.IMG was investigated but not bundled because its 225 m spacing is coarser than the existing 200 m fallback. The strips have ragged valid-data footprints, so their nominal rectangular extents do not imply complete detailed coverage.

Original source example:
https://pds-geosciences.wustl.edu/mex/mex-m-hrsc-5-refdr-dtm-v1/mexhrs_2001/data/3124/h3124_0000_da4.img

Scientific context:
https://ode.rsl.wustl.edu/mars/pagehelp/Content/Missions_Instruments/MEX/HRSC/Intro.htm

The reader preserves the sinusoidal projection, the product-specific central longitude and the 3,396,000 m radius from the PDS label. These cannot be sampled with the equirectangular HiRISE/CTX transform. Nodata is -32768. Valid edges blend into the existing coarser measured provider. Georeferenced images are never converted into elevations.

All 4,550,050 H3124 source values matched independent GDAL decoding exactly. SHA-256 of the little-endian decoded samples:
`5dea7724d2d03d132b223b75aabec940c6ab9b9b4b7c20429001cf46956fe981`

At the reported start, the HRSC bilinear value is 471.770172 m (Float32 endpoint coordinates), verified independently. Native source hashes and affine metadata are recorded in `Assets/Resources/NASA/HRSC/StripCatalog.json`. The same pixels are used by the planner and ground provider.

Beyond bundled coverage, the app can query NASA PDS ODE for a local HRSC DA4 strip. It accepts only supported sinusoidal, signed 16-bit areoid products with spacing at most 175 m, source files at most 64 MiB, three candidate attempts, a 25-second preparation budget and 128 MiB of retained strip data. Queries and source bytes are cached locally. Failed/unsupported queries retain existing official coverage.

## Visual controls and limits

- IMAGERY uses the selected georeferenced orbital mosaic.
- RELIEF uses measured geometry with neutral albedo, avoiding the acquisition seams visible in the uncontrolled CTX mosaic.
- SLOPE colours gradients sampled from the rendered DEM mesh: green below about 5 degrees, amber around 15 and red at/above 30. It corrects for the selected display height multiplier; this is not a validated small-obstacle hazard map.
- Map relief display can be 1x, 3x or 6x. Large regions default to 6x, labelled in the header and selected-route dock. This changes only map visualization, not route coordinates, physical elevations, climb, estimated time or slope metrics. Human-scale walking always returns to 1:1.
- Exact-route native checks observed 75 m HRSC at the beginning, a ~20.2 m CTX DTM at a covered intermediate highlight, and 200 m HRSC/MOLA where finer stereo data is absent. This route does not have continuous metre-scale HiRISE coverage. Small rocks and sub-grid crater structure cannot be recovered faithfully from 75–200 m grids.
- Demo retains eight highlights and the exact 120-second endpoint. Six interior stations are selected within ordered route intervals using actual elevation changes, while the first and last preserve the chosen endpoints. The same stations are prepared and played back.
- Generic material grain is restrained and fades with distance. Mirrored photo cracks and artificial ripple modulation were removed. Ground rendering uses a dedicated measured-geometry material. Context terrain uses Cartesian bands. Near-surface normals use a source-spacing-aware physical differentiation baseline so sub-decimetre tessellation does not amplify float coordinate quantization into lighting stripes. Material grain is illustrative albedo only.

## HUD and loading fixes

Visor HUD logical coordinates are mapped into the safe rectangle x=110..1490 and y=70..830 on a 1600x900 design canvas, inside the visor's curved frame. Warnings, detail panels, completion UI, seeking and controls share the transform. Chase view restores the full-screen HUD, and timed speed controls remain hidden in demo mode.

Planner detail streaming is disabled while helmet terrain preparation runs. Previously, moving the planning camera could start a competing image request and cancel preparation through the shared source generation ticket. A cached Viking image no longer prevents a later CTX retry. Successful partial CTX windows retain transparency in missing areas; complete new image caches use PNG. Local preflight imagery has a 12-second request budget, and optional stereo refinement has a 30-second total preparation budget, retaining existing measured coverage on timeout.

## Reproduction

`MarsPath.exe --mars-reported-400km -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile <log>`

The review checks original source values, map route framing, unchanged physical data when changing relief scale, all eight demo terrain stations, exact completion, five full-mission checkpoints, and separate visor/chase HUD behavior. It captures native planner, slope, visor, terrain-highlight and chase views. Diagnostic flags are inactive during normal startup.
