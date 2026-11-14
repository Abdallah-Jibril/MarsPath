# New route and general native HRSC coverage

Reported start 24.67645 N / 58.36556 E; destination 27.68045 N / 53.42641 E. Reference NASA Trek crater center **21.010789 N / 22.531535 W**. The longitude sign was independently verified from the screenshot URL and lower-right coordinate readout. An earlier east-longitude probe described a different location and is not the user's reference crater.

## General source improvement

NASA PDS ODE's older HRSC DA4 archive returned no products at the newly selected route endpoints or reference crater. The HRSC science team's public FU Berlin map service exposes a separate preliminary GeoTIFF collection with newer processed strips. Its full public footprint response contains 1,567 candidate products. A compact 809 KB version is included as `Resources/NASA/HRSC/TeamCatalog.json` with native footprint polygons, official HTTPS URLs and release labels. The one-degree spatial index and actual point-in-polygon query avoid scanning the full collection repeatedly.

These are **ESA / Mars Express HRSC science-team preliminary products**, not NASA-produced data or the older NASA PDS archive. The footprint metadata describes the nadir image; it identifies candidates, not guaranteed valid elevation pixels. Actual TIFF projection, sample format, grid spacing and nodata are inspected before admitting terrain.

`MarsHrscStereoTerrain.GlobalCandidates(focus)` provides inexpensive offline candidate discovery. `PrepareNativeWindowIfAvailable(focus, cancelled)` uses native HRSC team windows before falling back to older PDS discovery. `MarsCogWindow` now explicitly supports the team's native sinusoidal GeoTIFF projection, validates the Mars radius and projection GeoKeys, and reads signed Int16 elevations correctly. Only the official FU `/mex4/` host is admitted for this mode. Existing USGS simple-cylindrical products keep their original projection path.

New native windows use bounded HTTP ranges around the requested position, preserve source pixels and projection, and are saved with SHA-256 for subsequent offline restoration. Nodata and source-edge feathering retain the continuous support policy established in the crater184 investigation. Terrain loading remains geometry-only; image acquisition is independent.

Primary source service: https://maps.planet.fu-berlin.de/ . Public footprint endpoint: https://maps.planet.fu-berlin.de/eqc-bin/wms?SERVICE=WFS&VERSION=1.1.0&REQUEST=GetFeature&TYPENAME=hrsc4and&OUTPUTFORMAT=geojson&SRSNAME=EPSG:49910&MAXFEATURES=10000 . Raw response retained in `fu-hrsc-team-global-footprints.json`.

## Exact selected route: offline native coverage

Four original HRSC terrain strips were verified independently with GDAL and read using bounded native TIFF tile ranges. Their actual DTM spacing is **75 m**, despite the `.da4.50.tif` suffix: `.50` is a product-version suffix, not 50 m spacing. The linked nadir images have 12.5 m pixels, which is also not terrain-grid spacing.

* https://maps.planet.fu-berlin.de/mex4/h7407_0009.da4.50.tif — native sinusoidal longitude origin 59°.
* https://maps.planet.fu-berlin.de/mex4/h7432_0000.da4.50.tif — longitude origin 57°.
* https://maps.planet.fu-berlin.de/mex4/h7457_0000.da4.50.tif — longitude origin 56°.
* https://maps.planet.fu-berlin.de/mex4/h7482_0000.da4.50.tif — longitude origin 54°.

Only original native rows intersecting latitudes 24–29 N were included. The four bundles total approximately 42.6 MB. Pixel values are unchanged; only byte order is converted into the existing scientific sampler's MSB storage. All 17 reference stations along the direct corridor have valid native 75 m elevations. Actual A* routes can vary, and their source mix is sampled individually in the application.

Reference samples:

* Start: 1210.051497 m; H7407 source pixels 1208,1208,1212,1211.
* Direct corridor midpoint: −419.113129 m; H7457 source pixels −423,−414,−420,−410.
* Destination: −53.135666 m; H7482 source pixels −49,−59,−46,−56.

Metadata, source windows, hashes and all reference probes are saved in `nov5-newroute-hrsc-team-bundle.json` and `newroute-hrsc-native-probes.json`.

## Finer CTX section

The official USGS CTX DTM `J01_045230_2051_XN_25N304W__J02_045586_2051_XN_25N304W` was downloaded and validated. Its native grid is 20.1327878732818 m. A native 2746 × 2048 Float32 window is bundled, containing 2,970,188 valid pixels of 5,623,808 total. No resampling is applied, and nodata becomes NaN.

The route's direct midpoint lies inside its bounding rectangle but has nodata. Real valid coverage exists farther northwest around 26.272325 N / 55.74163656 E, 26.3662 N / 55.58728813 E and 26.460075 N / 55.43293969 E. The provider therefore uses 75 m at the exact midpoint and actual 20.1 m coverage only where source pixels are valid.

Source: https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/ctx/controlled/usgs/J01_045230_2051_XN_25N304W__J02_045586_2051_XN_25N304W/J01_045230_2051_XN_25N304W__J02_045586_2051_XN_25N304W_dtm.tif . Audits: `newroute-J01-valid-route-audit.json`, `newroute-ctx-offline-window-audit.json`.

## NASA Trek reference crater: corrected western longitude

At the actual screenshot position **21.010789 N / −22.531535 E**, the official USGS CTX F03/F02 stereo DTM has valid native 20.0584367028 m elevations. Its original pixels were independently sampled with GDAL. The other intersecting J06/J07 catalog envelope has nodata at the exact point and is not falsely counted as valid coverage.

A native 2048 × 2048 window from F03/F02, approximately 41 × 41 km, is now bundled without resampling. It contains 3,013,905 valid pixels, with measured-source heights from −4759.689941 to −1924.622559 m: 2835 m of genuine crater relief. Exact center interpolation is −3750.063044 m. The original source quad is −3750.479492, −3749.468506, −3751.663574, −3750.899658 m.

Source: https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/ctx/controlled/usgs/F03_036859_2005_XN_20N022W__F02_036569_2004_XN_20N022W/F03_036859_2005_XN_20N022W__F02_036569_2004_XN_20N022W_dtm.tif . Audit: `trekwest-F03-native-audit.json`, `trekwest-exact-height-golden.json`.

A separate 356 × 356 native USGS HRSC/MOLA 200 m blended context window spans approximately 70 × 70 km around the western reference position. This supplies measured surrounding context beyond the finer valid DTM footprint. The earlier 200 m east-longitude crop remains valid data for its own geographical location but is not the NASA Trek reference crater.

Context source: https://planetarymaps.usgs.gov/mosaic/Mars/HRSC_MOLA_Blend/Mars_HRSC_MOLA_BlendDEM_Global_200mp_v2.tif . Audit: `station1-and-west-reference200-audit.json`.

## Balanced-route station coverage gap

The actual Balanced A* track detours south of the direct reference corridor. Its first intermediate station, 24.65266 N / 57.80936 E, falls in a genuine native stereo-coverage gap. Original H7407 pixels are all −32768 nodata; the checked FU team, PDS ODE and USGS CTX/HiRISE inventories supply no valid finer product there. A 70 km native USGS 200 m blended window is bundled around this station. The application reports **200 m**, not invented 75 m coverage. All source pixels and hashes are retained in the corresponding audit.

Grid spacing is not a positional/vertical accuracy guarantee. These source improvements do not imply metre-scale stereo coverage everywhere on Mars. Each selected route and each walking location retain their actual source/resolution labels.

## Independent general live-pipeline probes outside all bundled crops

The locations 30 N / 58.6 E and 31 N / 59 E are beyond the bundled 24–29 N terrain windows. The global science-team footprint index returns exactly one candidate for each: `h7407_0009.da4.50.tif`. Independent GDAL reads of original source pixels establish:

* 30 N / 58.6 E: original pixel column 652, row 4188, four heights −49,−51,−50,−51 m; expected bilinear height −49.8157327015 m.
* 31 N / 59 E: original pixel column 926, row 3398, four heights −217,−221,−208,−212 m; expected bilinear height −215.5387995460 m.

Each live window needs one native 256 × 256 tile: 131,072 source bytes, plus the bounded 262,144-byte TIFF header. Its scientific sampler buffer is 131,088 bytes including the documented 16-byte prefix. Native spacing remains 75 m; there is no stride/resampling. The dynamic cache stores the actual native west/north, Mars radius, sinusoidal longitude origin, dimensions, spacing, source label and SHA-256 of the sampler buffer. Restoration checks byte length/layout and hash. These probes test the general source-discovery/projection/download path rather than reusing a bundled local terrain patch. Goldens are saved in `hrsc-general-live-probe-goldens.json`.
