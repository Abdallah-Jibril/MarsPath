# Verified native Jezero mission zone (4 × 6 km generation)

This independently prepared regional source pack preserves the original HiRISE pixel values. The selectable rectangle is 4.054 km east–west by 6.079 km north–south, inside a native 4224 × 6000 sample source crop. It contains the previously verified western-delta scarp. It is separate from any later expanded zone; these files and the original 1 m example remain available as evidence.

Selectable bounds: west 77.40108323193172° E, east 77.47318686681028° E, south 18.410559891432932° N, north 18.513121693789902° N. The crop has a 10 m equirectangular-map inset around the selectable boundary for source sampling support. Actual east–west ground distances account for latitude. Neither a point inside the rectangle nor complete source coverage guarantees a feasible route under slope/resource constraints.

## Native source and validity

DTM: `DTEEC_045994_1985_046060_1985_U01`, source window x=1024, y=2048, width=4224, height=6000. All **25,344,000 samples are valid**, with zero source-height nodata pixels. Elevations span -2616.6167 to -2404.8843 m relative to the Mars areoid. The 1.016553302 m value is the official USGS reprojected grid spacing, not a claim of vertical accuracy.

Matching photograph: `ESP_045994_1985_RED_C_01_ORTHO`, original 1.016553302 m source grid. The independently retained source mask has 12,135 nodata pixels in the photographic crop, approximately 0.047862%; all are retained transparently. No photographic gaps were filled. All native runtime image tiles use the same documented linear 0.5–99.5% source-DN display stretch. The separate 2884 × 4096 overview is averaged only for display at approximately 1.48934 m; it is not the native ground image or a higher-resolution elevation model.

- DTM source: https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/hirise/controlled/dtm/ESP_045994_1985_ESP_046060_1985/DTEEC_045994_1985_046060_1985_U01.tif
- Matching native RED_C image: https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/hirise/controlled/dtm/ESP_045994_1985_ESP_046060_1985/ESP_045994_1985_RED_C_01_ORTHO.tif
- Original producer/product documentation: https://www.uahirise.org/dtm/dtm.php?ID=ESP_045994_1985
- USGS projection and control documentation: https://stac.astrogeology.usgs.gov/docs/data/mars/hirise_dtms/

CRS: Mars 2015 sphere / ocentric equirectangular, central meridian 0°, standard parallel 0°, radius 3,396,190 m, IAU:49910. Height samples are Float32 with original nodata -3.4028226550889045e38. These are planetary coordinates, not Earth EPSG metre coordinates.

## Reproducibility and verification

`coverage-audit.json` contains original affine transforms, CRS, dimensions, exact bounds, validity counts, display parameters, original-source URLs and SHA-256 hashes. `independent-local-verification.json` checks that:

- All four runtime height tiles are bitwise identical to their native source subwindows, with 128 native pixels (130.119 m) of overlap and no uncovered seams.
- Every overlap agrees exactly; the previously independently verified 4,194,304-value small source crop also matches bitwise.
- Runtime photograph DN values exactly match the declared display stretch, and alpha exactly preserves source nodata.
- No terrain was resampled, no height gaps were filled and all height tile dimensions remain below 3072.

`independent-elevation-reference-points.json` records five actual Float32-coordinate bilinear check points, source pixels and 2 × 2 original stencils. It is tied to this original source and must not be applied automatically to a different mosaic.

Open `JezeroMissionZoneReference.qgz` in QGIS. Its data paths are relative within this folder. The default layer is the registered photographic overview; original Float32 DTM and image-validity layers can be toggled separately. `JezeroMissionZoneOverview.png` is the rendered and visually checked overview.

The corresponding 4 km generation runtime manifest and definition are preserved here as `runtime-resource-manifest.json` and `JezeroZone.json`. Initial runtime data total approximately 144.57 MB, excluding Unity-generated metadata and duplicate independent GIS reference files. A later version may publish a different live `NASA/MissionZone/` definition; this pack continues to document the original four-tile source generation.
