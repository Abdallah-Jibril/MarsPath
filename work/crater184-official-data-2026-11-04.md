# Exact reported crater route: source audit and offline data

Start: 24.15337 N, 83.02737 E. Destination: 26.60797 N, 84.74411 E.

The start has no HiRISE or CTX DTM envelope in the app's official USGS STAC catalog. Two CTX stereo DTMs cover parts of the crater terrain farther along the route. Their actual nodata footprints are narrower than the catalog bounding rectangles. This audit probes source pixels, rather than treating a bounding rectangle as valid elevation coverage.

## Delivered data

* NASA PDS / ESA Mars Express `H2926_0000_DA4.IMG`, original 19,790,856-byte file. Native 100 m sinusoidal areoid elevations, 1938 × 5100, central longitude 84°, original data offset 23,256. Its valid footprint covers all 17 reference stations along the direct corridor. Original SHA-256: `ed150bcac5daf165a227a83b6373e803a0f24bf94d0f24432e6af3f8ef99139b`.
* NASA PDS / ESA Mars Express `H2948_0000_DA4.IMG`, native 75 m grid, central longitude 82°. Included only original rows 500–3099, all 1935 columns, downloaded by HTTP bytes 1,958,220–12,020,219. The 10,062,000 source row bytes are unchanged. A 16-byte zero prefix replaces the original file header, and the provider's documented projection metadata is adjusted only for the row offset. The bundle is 10,062,016 bytes. SHA-256 `26e09668efb7ae62929713e0c83f62e8faeef3d1df37df3e15ff9f33aac61092`. Valid at the start and lower portion of the corridor; fallback to the 100 m strip outside its footprint.
* USGS CTX `G18_025383_2053_XN_25N276W__G19_025528_2053_XN_25N276W`, native 20.667078685594507 m, 1774 × 2395. All valid native Float32 heights retained without resampling; nodata becomes NaN. Height range of valid source pixels: −3650.2883 to −1312.4623 m.
* USGS CTX `J19_052046_2055_XN_25N276W__J18_051901_2055_XN_25N276W`, native 20.768311056212898 m, 1735 × 2129. All valid native Float32 heights retained without resampling; nodata becomes NaN. Height range of valid source pixels: −3789.4326 to −1516.9948 m.
* Matching J19/J18 CTX orthoimage, **actual native 20.7683 m grid**, not a 5–6 m image. The official matching product uses the same dimensions and projection as its DTM. A 1,154,730-byte grayscale PNG preserves native dimensions/georeferencing, with a display-only 2nd–98th percentile linear contrast stretch from −0.04710576..0.66809559 into gray values 18..238. Nodata is transparent. Elevations are not inferred from image brightness.

Total new offline data is approximately 63 MB. No synthetic craters, terrain displacement or arbitrary visual relief were added by these source changes.

The G18/G19 matching orthoimage was subsequently bundled as well, retaining its native 1774 × 2395 dimensions and 20.667079 m georeference. Its display-only 2nd–98th percentile stretch is 0.054219514..0.789767504 into gray values 18..238; nodata is transparent. Both matching CTX images are approximately 20 m pixels, independently from the instrument's nominal approximately 6 m acquisition scale.

## Source-seam continuity correction

The exact walking location 24.96827 N / 83.80193 E revealed a rendering defect in source composition. The real J19 height there is −3428.540325 m; its support-distance corner values are 13,12,12,11 cells. The previous code selected the minimum corner distance, making the source weight constant within each 20 m pixel. At a pixel edge the weight jumped. When neighboring real elevation products differ, this manufactured vertical steps even though each product's height was bilinear and smooth.

The correction bilinearly interpolates the support-distance field using the same pixel fractions as height sampling. A two-cell support guard reaches zero before a four-corner interpolation would touch nodata, including diagonal invalid corners. The existing 12-cell transition width remains. Native source heights are unchanged; this fixes the composition weight, rather than smoothing or filtering scientific elevations. HRSC strip composition now uses the same continuous support policy with a lazily built distance map.

Independent offline scans across ±200 m around the reported position, bearings 0°,45°,90°,135° and 0.2 m steps, measured old artificial jumps of 4.07–5.12 m. Corrected largest height changes are 0.056–0.079 m per 0.2 m step. Detailed source values and scan results are in `crater184-seam-continuity-audit.json`; reproducible with `audit-crater184-seam.cjs`.

## Optical image-footprint blending

Official orthoimages now receive a 24-native-pixel alpha feather inward from both nodata holes/irregular boundaries and image-window borders. This alters display blending only; no DTM values or image georeference changes. Fully supported central display pixels remain unchanged. RGB is premultiplied by coverage in the renderer's colour space: for linear rendering, decode sRGB, multiply by alpha, then encode sRGB texture bytes. The surface shaders can therefore divide filtered RGB by filtered alpha without bright or dark outlines. Cached PNG files remain straight-alpha; preparation runs once on each freshly decoded/generated texture before material binding. The policy applies to future official image downloads as well as bundled CTX/HiRISE images. `MarsOfficialTerrain.ImageFootprintSelfCheck()` checks unchanged central pixels, transparent nodata/borders and recovered transition brightness.

## Independent samples

* HRSC100 start interpolation: −1984.547075 m; GDAL original pixel 235,2966 = −1985 m.
* HRSC75 start interpolation: −2031.134797 m; original pixel 1426,2756 = −2031 m. This finer source takes priority at the start. Separate real source products differ; these values are not presented as sub-metre ground accuracy.
* CTX G18 24.76702 N / 83.456555 E: −1617.016686 m, real crater-rim terrain.
* CTX J19 25.073845 N / 83.6711475 E: −3344.805710 m.
* CTX J19 25.2272575 N / 83.77844375 E: −3462.781987 m. Independent GDAL original pixel 1143,1238 = −3461.52783203125 m.
* CTX J19 25.38067 N / 83.88574 E: −2928.221267 m.
* 24.9204325 N / 83.56385125 E lies inside both catalog rectangles but lacks valid CTX data at the sampled pixel. The provider correctly uses HRSC rather than inventing a 20 m surface.

The reference station line is a direct corridor, not a substitute for the app's selected A* track. The native acceptance test computes the actual route and checks the same provider used for rendered terrain.

## Source-query fix

The old dynamic HRSC query supplied equal minimum/maximum latitude and longitude. The official ODE server responded `ERROR: Max latitude must be greater than min latitude`. The corrected query uses a bounded ±0.0001° envelope and ignores cached ODE error responses. Failed requests can retry after 60 seconds. Reproduction responses are in `crater184-start-hrsc.json` and `crater184-start-hrsc-valid.json`.

## Reproduction evidence

Metadata and native audits are saved in `crater184-hrsc-native-audit.json`, `crater184-hrsc75-native-audit.json`, `crater184-ctx-bundle-audit.json`, and original GDAL metadata JSON files. The bounded extraction scripts are `audit-crater184-sources.cjs`, `bundle-crater184-hrsc75.cjs`, and `bundle-crater184-ctx.cjs`.

Official source URLs are recorded in every resource metadata record and in the HRSC strip catalog. The HRSC source root is https://pds-geosciences.wustl.edu/mex/mex-m-hrsc-5-refdr-dtm-v1/mexhrs_2001/ . CTX official native DTM and matching ortho are from https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/ctx/controlled/usgs/ .

Official methodology: https://stac.astrogeology.usgs.gov/docs/data/mars/ctxdtms/ . CTX images commonly have approximately 6 m pixel scale, while these stereo DTMs have approximately 20 m post spacing. The official matching ortho assets used here themselves have approximately 20 m grid spacing. HiRISE DTMs are selected stereo coverage at 1–2 m spacing: https://www.uahirise.org/dtm/about.php . No HiRISE height coverage was claimed for this particular corridor.
