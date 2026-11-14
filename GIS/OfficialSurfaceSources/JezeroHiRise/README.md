# MarsPath measured Jezero desktop reference

This pack is an independent physical and visual reference for validating the desktop planner and visor. It uses original measured HiRISE pixels. No hills, craters, rock positions or imagery were generated. It is not a claim of centimetre-level terrain accuracy or operational EVA validation.

## Open and compare

Open `MarsPath_Measured_Jezero_Reference.qgz` in QGIS. It contains a real georeferenced native HiRISE RED_A image, the original bundled Float32 DTM reconstructed as a GeoTIFF without changing any values, measured check points and a z-factor-1 hillshade. The color elevation and hillshade layers start hidden; turn them on separately to inspect height. The default image is a documented linear 0.5–99.5% display stretch, with its original raw UInt16 source crop retained separately.

`QGIS-native-reference.png` is the rendered and visually checked QGIS view. `native-reference-overview.png` shows the wider registered ortho, native measured elevation and ridge profile. `ridge-profile.csv` contains the independently sampled elevation profile. QGIS reference files use relative paths within this folder.

## Strong real relief to validate at eye level

All coordinates below are planetocentric degrees north and east. Heights are metres relative to the Mars areoid. Pixel coordinates are zero based within the original bundled 2048² DTM crop.

| Point | Latitude N | Longitude E | Native height m | Pixel column,row |
|---|---:|---:|---:|---:|
| Ridge approach | 18.46469625 | 77.42534451 | -2529.3721 | 400,785 |
| Ridge crest | 18.46819482 | 77.42534451 | -2473.2668 | 400,581 |
| Scarp bottom | 18.46205517 | 77.42472712 | -2541.2722 | 364,939 |
| Scarp upper edge | 18.46402740 | 77.42277203 | -2474.7068 | 250,824 |

The northward ridge line from 18.46470 N, 77.42535 E to 18.46820 N, 77.42535 E spans 207.46 m and rises 56.07 m when sampled bilinearly from the unchanged DTM. The native scarp low/high pixels differ by 66.57 m over approximately 160.5 m of local horizontal separation. From the bottom, look northwest toward roughly 318° to put that real scarp in front of the camera. A flat vista there would indicate a mapping, height sampling, camera or geometry error. A route to the crest is a visual reference, not an assertion of safe traversability.

The strongest approximately 120 m equirectangular-map window is centered at 18.46306701 N, 77.42369813 E; the 120 m-labelled neighborhood in the diagnostic covers 66.47 m of native height variation. `native-reference.json` records 20,60,120,360 m local-window ranges and all sampled values. Local east distances are equirectangular map distances multiplied by cos(latitude). Grid post spacing is not vertical accuracy.

## Original datasets and CRS

- DTM: `DTEEC_045994_1985_046060_1985_U01`, observations ESP_045994_1985 / ESP_046060_1985, USGS `M2020_Jezero_W`. Original product nominal spacing is 1 m; the official USGS ARD reprojection has a 1.016553302 m projected grid. All 4,194,304 values in the bundled 2048² crop are valid. Its height range is -2575.1694 to -2464.0374 m.
- The original RED_C matched ortho has a 1.016553 m projected grid. It is not a 0.25 m image. The separate **RED_A** variant downloaded in this task genuinely has a **0.254138226 m projected grid**, 27456 × 56700 source pixels, UInt16, nodata 0. The bounded native window is 3072 × 3072 at source x=8253,y=17984, approximately 780.7 m of map width. `JezeroHiRiseNativeOrtho.tif` preserves its original projection, affine transform, native values and nodata. The stretched PNG preserves all 3072² source sample locations and contains no super-resolution or upsampling.
- CRS: IAU:49910, Mars 2015 sphere, planetocentric equirectangular, central meridian 0°, standard parallel 0°, radius 3,396,190 m. USGS assigns this common planetary CRS to reprojected products. The native DTM is areoid referenced and tied to MOLA. Never use an Earth EPSG CRS for these metre coordinates.
- Raw DTM SHA-256: `73f13d023d11e045e31ece4b179f3970553fc40c23c44d94b107b4dec7f21de7`. The earlier independent official-GDAL crop verification matched every value. Current diagnostics preserve that hash. Native image crop and PNG hashes and exact extents are in `native-ortho-provenance.json`.

## Primary source links

- Original product, nominal spacing and matching A/C image variants: https://www.uahirise.org/dtm/dtm.php?ID=ESP_045994_1985
- Original USGS producer README, areoid convention, 1 m post spacing and source-image quality categories: https://www.uahirise.org/PDS/EXTRAS/DTM/ESP/ORB_045900_045999/ESP_045994_1985_ESP_046060_1985/README.TXT
- USGS reprojection/control documentation: https://stac.astrogeology.usgs.gov/docs/data/mars/hirise_dtms/
- Native image source: https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/hirise/controlled/dtm/ESP_045994_1985_ESP_046060_1985/ESP_045994_1985_RED_A_01_ORTHO.tif
- DTM source: https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/hirise/controlled/dtm/ESP_045994_1985_ESP_046060_1985/DTEEC_045994_1985_046060_1985_U01.tif
- NASA/JPL's MARTIAN pipeline demonstrates handling this same product family: https://github.com/nasa-jpl/martian

HiRISE DTMs cover selected stereo footprints, not the entire planet at 1 m. The original DTM product has a larger footprint than this bounded asset. Original annotated DTM/ortho browse images are included as source context when download succeeds. An item bounding box is not a guarantee that every pixel inside it is valid. Producer quality notes mention possible interpolation, CCD seams and unreliable low-contrast/deep-shadow facets; grid spacing alone does not override those limitations.

## Authentic ground photograph reference

NASA PIA24921, Perseverance Mastcam-Z, is an authentic Jezero delta panorama acquired during June 2022 and credited NASA/JPL-Caltech/ASU/MSSS. The bounded downloaded browse is **enhanced color**, not a calibrated natural-color texture. NASA identifies a separate Figure A natural-color panorama. Use the browse for the general appearance of sedimentary rock and distant relief; do not label it as the exact camera view or texture at the ridge coordinates above. https://science.nasa.gov/photojournal/detailed-panorama-of-mars-jezero-crater-delta/

The app's existing Curiosity sandstone grain source PIA20323 was acquired at Mount Sharp in Gale crater. It is a material reference and cannot establish the actual geometry or rock placement at Jezero or arbitrary selected Mars coordinates.

## Application integration assets

Separate new resources are `Assets/Resources/NASA/Stereo/JezeroHiRiseNativeOrthoPixels.bytes` and `JezeroHiRiseNativeOrtho.json`. Distinct basenames keep Unity's metadata and binary TextAssets unambiguous. The PNG is 6,569,788 bytes. The JSON is registered in `BundledWindows.json` and carries exact double degree bounds for the local image-registration path. Existing 1 m DEM values were preserved.

The optional application example now uses the checked 120 m scarp approach at 18.4620552 N, 77.42473 E → 18.4640789 N, 77.42473 E. All five route objectives find a path under the normal 25-degree slope limit. The original longer ridge profile above remains a measured reference; it is not a promise that its steeper endpoint is admissible for an EVA.
