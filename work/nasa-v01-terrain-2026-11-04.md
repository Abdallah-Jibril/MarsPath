# MarsPath photographic terrain — 4 November 2026

The reported route is **24.67645° N, 58.36556° E → 27.68045° N, 53.42641° E**. All five profiles retain these endpoints. They may change as actual finer height coverage becomes available; the direct geographic distance and routed length are distinct.

## Actual defects fixed

- Initial imagery refinement waited for elevation work or camera movement. Images now stream immediately, independently of heights.
- A single shared image texture was replaced by unrelated future stations. Separate protected region/view/detail atlases retain the current photographic view while other stations prepare.
- Selecting the finest image only at the camera centre displayed one tiny sharp square over a blurry wide image. View selection now checks the complete visible footprint, with a third layer for finer local detail.
- Ten downloaded beta01 overview tiles were structurally truncated at the official old service. HTTP 200 and content length did not establish a valid PNG. The exact same fresh URL and cache-busted requests reproduced the defect. No manufactured fill was used. The replacement is the officially published CTX V01 service, with complete PNG chunks/CRC/IEND or JPEG completion checked before caching; an atlas is accepted only after every tile decodes successfully.
- Mission geometry requests used to be cancelled by each 15-second demo jump before completing. Bounded geometry requests now survive jumps and prefetch upcoming positions. Image jobs run separately. Only the legacy globally shared fallback reader is serialized.

## Sources and precision

- [NASA MRO CTX / Murray Lab V01](https://murray-lab.caltech.edu/CTX/V01/TilePages/), published [ArcGIS item](https://www.arcgis.com/home/item.html?id=30c4acfe36284b1784bf07d5d73f9e4d), service `https://astro.arcgis.com/arcgis/rest/services/OnMars/CTX1/MapServer`. Native nominal image scale is 5 m; this service's finest pyramid grid is about 5.09 m at the equator. Overview pixels are intentionally coarser, with view-dependent finer tiles on zoom. V01 is semi-controlled orbital imagery and has finite registration error; it does not provide metre-level surveyed ground truth.
- Native HRSC science-team preliminary strips H7407/H7432/H7457/H7482 provide **75 m height samples** across most of the reported corridor. A genuine CTX stereo window provides **20.13 m** samples where valid. Native nodata remains nodata. File suffix `.50` is a release identifier, not a 50 m sample spacing.
- A detour station near 24.65266° N, 57.80936° E falls in a confirmed stereo data gap. It truthfully uses a **200 m USGS HRSC/MOLA** window rather than claiming 75 m coverage.
- The NASA Trek reference screenshot is **21.010789° N, 22.531535° W**, not east. Its native F03/F02 CTX stereo DTM has **20.0584 m** spacing and about 2,835 m of measured relief in the bundled 41 km window. Independent original-pixel bilinear elevation at the reference is **−3750.063044 m**.
- The ordinary general path also queries a spatial index of 1,567 additional HRSC science-team footprints and the existing USGS HiRISE/CTX catalog. Independent live probes outside the bundles resolved from 926 m MOLA to genuine 75 m native HRSC.

Planning and helmet use measured physical heights at **1:1 vertical scale**. Source-scaled normals avoid oversampling lighting gradients. A bounded instanced pebble layer supplies human-scale visual material detail; pebble positions are illustrative, never measured obstacles or added DEM craters. Suit resources and science/risk scores remain prototype models.

## Verification

Reproducible native acceptance: `--mars-nasa-quality-review`. It checks the actual reported endpoints, independent source height goldens, every demo station, exact source-to-rendered-mesh agreement, retained route arrows, PNG truncation rejection, visible instanced gravel, demo/full route endpoints, the corrected NASA Trek crater, and two independent general live HRSC windows. Native screenshots are copied to `outputs/MarsPathNasa*.png`. Build/runtime logs: `work/nasa-v01-final-build.log`, `work/nasa-v01-native.log`, `work/nasa-v01-smoke.log`.

Raw official V01 tile provenance and SHA-256 hashes: `work/nasa-ctx-v01-cache-audit.json`. The local sample cache is a prewarm of the general provider, not a coordinate-specific renderer. Future uncached regions still depend on network availability and actual source coverage; no global metre-scale elevation claim is made.
