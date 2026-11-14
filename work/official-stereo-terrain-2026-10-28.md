# Official stereo terrain release — 28 October 2026

The regional planner, desktop visor, 120-second presentation and full-mission playback now share measured elevation. Regional geometry does not add procedural hills, craters or boulders. Photographic grain and grayscale colorization affect material appearance only. Mission risk estimates, science candidates and life support remain prototype models.

## Sources and coverage

- Official USGS Astrogeology STAC: https://stac.astrogeology.usgs.gov/api
- HiRISE collection `mro_hirise_socet_dtms`: 852 nonpolar entries in the downloaded catalog. Native post spacing is normally 1–2 m; these DTMs cover selected stereo footprints, not the whole planet. https://stac.astrogeology.usgs.gov/docs/data/mars/hirise_dtms/
- CTX collection `mro_ctx_controlled_usgs_dtms`: 44,628 unique entries. Uses `geoid_adjusted_dem` for current products or `dtm` for older products, never the unadjusted `dem` asset. Typical post spacing is 20 m. https://stac.astrogeology.usgs.gov/docs/data/mars/ctxdtms/
- Asset host: `https://astrogeo-ard.s3-us-west-2.amazonaws.com/`. Catalog-derived north-up equirectangular sources use Mars sphere radius 3,396,190 m. Polar stereographic products are excluded from this decoder; polar areas retain official fallback.
- Coarser context: USGS HRSC/MOLA 200 m blended DEM and bundled global MOLA ~3.7 km grid. Stereo holes and ragged boundaries blend into coarser measured context. Nominal post spacing does not establish actual terrain feature resolution or accuracy.

## Bundled offline source

`DTEEC_045994_1985_046060_1985_U01`, paired observations ESP_045994_1985 / ESP_046060_1985, Jezero. Source DTM:

https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/hirise/controlled/dtm/ESP_045994_1985_ESP_046060_1985/DTEEC_045994_1985_046060_1985_U01.tif

The bundled crop is 2048 x 2048 Float32 values, source pixels x=2048..4095, y=4096..6143, grid spacing 1.0165533020257713 m. West=4588956.747130277 m, north=1095287.787449006 m, dx positive, dy negative. Matching RED_C orthoimage is included. Heights are areoid referenced. Assets are in `Assets/Resources/NASA/Stereo`.

An independent GDAL extraction of every source pixel in that crop matches MarsPath's managed TIFF decode byte for byte. Both SHA-256 hashes:

`73F13D023D11E045E31ECE4B179F3970553FC40C23C44D94B107B4DEC7F21DE7`

GDAL check: `gdal_translate -srcwin 2048 4096 2048 2048 -of ENVI -ot Float32 <source> <output>`.

## Runtime behavior and limits

COG reads request only necessary compressed source tiles, not whole gigabyte files. Bounds: 256 KiB header, 8 concurrent requests, 64 MiB compressed transfer per crop, 3072 x 3072 maximum decoded window, 65-second request deadline, up to 256 MiB/24 decoded height crops retained. Managed codec is BitMiracle.LibTiff.NET 2.4.660 (BSD; license included in Assets/Plugins). No QGIS/Python/native GIS installation is required by the delivered application.

All eight presentation stations prepare local geometry and imagery before the demo clock starts. Image windows are cached as PNG and switched on geographic jumps. Geometry is restored from cached native values if evicted. Full mission mode streams the current location and a point 500 m ahead; source revisions rebuild terrain and guidance together. Network failure keeps the available official fallback, so resolution can still vary along a route. Downloaded cache lives in `%USERPROFILE%/AppData/LocalLow/LAGRANGE V/MarsPath/OfficialTerrain/v1`.

The route planner uses 145 x 145 cost cells below 5 km and 73 x 73 at longer distances. Map mesh density is separately capped at 769 x 769. A long-range map necessarily samples more coarsely than a human-scale local view. At planetary distances, azimuthal-equidistant projection has off-center distortion; source post spacing must not be mistaken for uniformly preserved map scale.

## Verification

- Native decoder and independent GDAL: complete 4,194,304-value equality above.
- Native geometry/provider agreement for sampled vertices in planning and helmet meshes.
- All eight demo stations, exact 120-second endpoint, five full-mission geographic checkpoints and metre-scale motion.
- Existing regression suite: global/manual/landmark selections; dateline and poles; five route profiles; original endpoints; full route; 1x/30x/50x; oxygen reserve; rigid backpack; late-route 60 Hz smooth movement; navigation arrows.
- Source artwork copied unchanged into `Assets/Branding/MarsPathIcon.png`, applied to default and Standalone icon slots; embedded Windows EXE icon extracted for visual verification.

Reproduce official source check: `MarsPath.exe -batchmode -nographics --mars-official-terrain-check -logFile <log>`.

Visual check: run `MarsPath.exe --mars-official-terrain-check -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile <log>`. This captures the application and exits. Demo coordinates: start 18.46470 N, 77.42535 E; end 18.46950 N, 77.42950 E. Ordinary startup never runs diagnostic workflows.

Headset installation, stereo comfort and on-device performance still require physical Quest testing.
