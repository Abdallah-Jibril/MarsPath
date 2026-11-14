# Peridier Crater: newly acquired measured stereo terrain

Retrieved and checked on 7 November 2026 using QGIS 3.44.15 / GDAL. This is a research source pack, not an automatic replacement of the running application's height model.

## Official sources

- NASA PDS bundle: [Day et al. (2023), DOI 10.17189/ervc-mr85](https://doi.org/10.17189/ervc-mr85).
- [USGS archive and source description](https://astrogeology.usgs.gov/search/map/mars_human_exploration_zone_dem_archive_2023).
- [Author's archived processing description](https://github.com/GALE-Lab/Mars_DEMs).
- [HiRISE observation and stereo pair, Sinuous Ridges and Valleys in Peridier Crater](https://www.uahirise.org/ESP_051901_2055).

Source products: `esp_051901_2055_esp_052046_2055_tied-dem.tif` and `_tied-drg.tif`; paired context product `j18_051901_2055_xn_25n276w_j19_052046_2055_xn_25n276w_tied-dem.tif`. Exact download URLs appear in `native-window-validation.json` and `remote-metadata.json`.

## What is included

`PeridierHiRISE1mNative2048.tif` contains an exact 2048 by 2048 native elevation crop, with 1.000 m cells. The corresponding little endian float32 `.bytes` file retains every value, including source nodata −32767. The crop is copied, not resampled, denoised, vertically stretched, or inferred from image brightness. The source crop equality and hashes are recorded in `native-window-validation.json`.

`PeridierHiRISE1mNative2048Ortho.tif` is the geographically matching orthophoto crop in its **own native 1.0269419246 m grid**. It must use its own affine transform; image and DEM pixels do not have identical indices. Both use local sinusoidal projection, central meridian 83.68506713829 degrees east, sphere radius 3396190 m. Native elevation min/max in this crop are −3407.54 and −3218.21 m, a relief span of 189.32 m over approximately 2 km. Pixel spacing is not a measurement accuracy claim.

`PeridierCTX18mNative.tif` is the full, relatively small paired native 18 m context DEM. The hillshade is a derived diagnostic with vertical scale 1:1, azimuth 315 degrees, altitude 45 degrees. PNGs are reference previews, not elevation inputs.

## Actual selected route coverage

The original selected endpoints are 26.73911 N, 82.70998 E and 24.24785 N, 84.94257 E. Coverage was checked against all five actual computed route polylines exported by the application on the retrieval date. See `coverage-and-provenance.json`.

| Actual route | Intersection with full native 1 m source rectangular extent | Nearest distance if it misses |
|---|---:|---:|
| Balanced | 0 km | 284 m |
| Safest | 6.326 km, about 3.05% of the full route | 0 m |
| Fastest | 0 km | 18.43 km |
| Science | 0 km | 13.89 km |
| Energy | 0 km | 45 m |

The downloaded central 2.048 km crop is smaller than the full 6.1 by 8.6 km source footprint. These percentages describe the full original native source extent, not the smaller packaged crop. Internal nodata further reduces usable measured coverage. The paired 18 m CTX extent intersects about 42.43 km of Balanced. The current endpoints and route choices must remain unchanged. This evidence cannot support a claim of metre-scale coverage across the whole 199 km mission or all of Mars.

## Quality and vertical registration

The source description reports NASA Ames Stereo Pipeline/ISIS stereo processing, CTX tied to MOLA and HiRISE tied to its paired CTX, using the Mars areoid reference (spelled “aeroid” in the source README). The PDS XML labels document metre height units, local sinusoidal coordinates, and MOLA point cloud alignment. Preserve this provenance; do not infer a different datum from the horizontal sphere's radius.

Nine independently projected locations were compared against the original source data. Valid HiRISE minus paired 18 m CTX differences have median about 1.85 m; the old ~20.8 m CTX product is approximately 30–45 m lower in this sampled window. Different source resolutions and registration contribute to local differences. A robust, separately validated co-registration is required before blending across products. No vertical correction has been applied in this pack.

The native elevation crop is 94.89% valid and contains correlation noise, striping and a large source gap. Some raw local slopes are implausibly high for navigation because stereo artifacts remain. The 1 m label therefore must not make this layer automatically override cleaner terrain. Visual quality review is recorded by the raw hillshade and original orthophoto preview.

`hirise-quality-mask.png` is the original ASP Good Pixel Map: grey denotes successful correlation and red unsuccessful correlation. It is **2553 by 3168 RGB**, while the complete source DEM is **6121 by 8636**. The PNG is a processing diagnostic, not a pixel-aligned georeferenced DEM mask. Do not reuse its row/column indices against the DEM. Use exact DEM nodata for geographic validity; inspect the PNG with its PDS metadata for additional source quality context. Never fill its gaps and present the result as measured terrain.

## Current catalog search

The existing USGS HiRISE ARD catalog states a July 2023 update. The current UArizona catalog advertises 1,377 images. All 58 current catalog pages were downloaded; 1,370 unique product page IDs were found. Ninety-two center-latitude candidates approximately 20–29 N were checked through their individual DTM metadata pages. None had a center inside 80–88 E, 22–29 N. This is a targeted search, not a claim that every future product or large peripheral footprint was ruled out.

Four overlapping HRSC science-team DEMs were also inspected directly. Their actual native height spacings are H2882 200 m, H2926 100 m, H2948 75 m and H7421 75 m. The `.50` filename suffix does not mean a 50 m height grid. No height samples were synthesized from their finer imagery.

Open the GeoTIFFs in QGIS with their stored native CRS, or allow QGIS to reproject them for display. The runtime's former global equirectangular formula cannot directly read these sinusoidal coordinates. `coverage-and-provenance.json` records file sizes and SHA-256 hashes for every packaged artifact.
