# Jezero western delta: verified 10 × 10 km mission region

This pack uses the original USGS Mars 2020 Terrain Relative Navigation HiRISE master DTM and its co-registered photograph. It replaces the active four-tile mission-zone dataset with a larger coherent source, while the earlier 4 × 6 km GIS pack and standalone HiRISE example remain preserved separately.

## Selectable area and audited coverage

| Quantity | Verified value |
|---|---|
| Selectable width × height | 9.999914 × 10.000000 km, approximately 100 km² |
| West / east | 77.33578971367626° E / 77.51365504968241° E |
| South / north | 18.38445484384847° N / 18.553160892429624° N |
| Native support crop | 10564 × 10020 samples, unchanged 1 m DTM grid |
| Valid original height samples | 105,851,280 / 105,851,280; zero source nodata |
| Valid final 1 m photographic samples | 105,851,280 / 105,851,280; zero source nodata |
| Height range in this crop | -2649.1907 to -2303.2661 m, 345.925 m of relief |
| Native source window | x=6679, y=6875, width=10564, height=10020 |
| Native crop transform | west=4584045 m, north=1099743 m, dx=1 m, dy=-1 m |

The selectable boundary is inset by 10 m of actual local ground support on each side. Latitude-dependent east–west distance is accounted for. The rectangle was chosen only after checking every native height sample. A slightly more southerly trial crossed a real nodata wedge; it was moved north before production instead of filling that wedge.

All of these values describe the existing producer dataset, not ground-truth certainty at every metre. Scientific stereo products can contain correlation, interpolation and registration artifacts. Validity and native post spacing do not establish absolute measurement accuracy. Free endpoints within the supported region can still have no route under the chosen slope/resource constraints. Individual sub-grid rock models remain illustrative and are not a survey of real rock positions.

## Official sources and consistent registration

- [USGS HiRISE TRN DTM product](https://astrogeology.usgs.gov/search/map/mars_2020_terrain_relative_navigation_hirise_dtm_mosaic)
- [USGS matching HiRISE TRN orthomosaic and producer methodology](https://astrogeology.usgs.gov/search/map/mars_2020_terrain_relative_navigation_hirise_orthorectified_image_mosaic)
- [Official USGS source-file index](https://asc-pds-services.s3.us-west-2.amazonaws.com/mosaic/mars2020_trn/HiRISE/index.html)
- [Canonical native 1 m DTM](https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/JEZ_hirise_soc_006_DTM_MOLAtopography_DeltaGeoid_1m_Eqc_latTs0_lon0_blend40.tif)
- [Canonical matching 0.25 m source photograph](https://planetarymaps.usgs.gov/mosaic/mars2020_trn/HiRISE/JEZ_hirise_soc_006_orthoMosaic_25cm_Eqc_latTs0_lon0_first.tif)

The producer jointly adjusted the component stereo images and applied common control/registration transformations before creating the master. Consequently, the master is not numerically identical to older separately released strips at the same coordinate. The entire expanded mission frame must use this master consistently; the historical individual-strip example keeps its own source. Producer metadata identifies quality limitations in the far-western crater-rim stereo model. This application does not remove those scientific limitations.

The CRS is planetocentric equirectangular on the Mars 2000 sphere, radius 3,396,190 m, central meridian 0°, standard parallel 0°. The original product is the MOLA-topography/DeltaGeoid `blend40` release. Source metadata, original nodata and complete affine/projection definitions are retained in the JSON files. Do not assign an Earth EPSG CRS to these metre coordinates.

## Height and image preparation

`Jezero10kmNativeDtm.tif` is a losslessly compressed Float32 native-pixel crop. No elevations were resampled, quantized, invented or gap-filled. Its SHA-256 is `0a667030c8f38591d6b236fce35fa4623f58acb254fa93afe0e1646f898c3228`.

`Jezero10kmMatchingPhotograph1m.tif` is a declared 4:1 nearest-neighbor photographic reduction from the co-registered 0.25 m producer image to a 1 m display grid. This reduction is restricted to imagery; it is not elevation interpolation or a claim of native quarter-metre imagery throughout the application. The resulting image and DTM have the identical affine frame. Its SHA-256 is `a124f0762ce57c9da9bce67fda7ff001c397f4419ee269bad364a0d4288457bf`.

Runtime photographs use one shared documented contrast stretch and lossless grayscale PNG encoding. Decoded RGB channels are identical to the corresponding original monochrome display values; alpha is implicitly opaque because every sampled image pixel in this chosen region is source-valid. This avoids storing three redundant color channels. The 4096 × 3885 overview is a separately labelled photographic display reduction at approximately 2.5791 m; the ground uses the 1 m image tiles.

The canonical server's transient HTTP errors were handled with source-pixel stripe checkpoints and SHA-256 verification before resuming. The official S3 mirror's metadata and a native DTM stencil were independently checked against the canonical product. Original native rows are retained without substituting coarser terrain or fabricated image content.

## Runtime package and independent checks

The active resource index is `NASA/MissionZone/BundledWindows`. It contains 16 height tiles of 2737 × 2601 samples, with 128 m overlaps in each direction, matching photographic tiles, one overview and the generic `NASA/MissionZone/JezeroZoneElevationReference` oracle. The live definition is `NASA/MissionZone/JezeroZone`.

`independent-local-verification.json` confirms all native samples are covered, every runtime height tile is bitwise identical to the corresponding original crop, every image DN/alpha matches its declared source processing, overlaps agree, and all tile dimensions remain below the current 3072 limit. `independent-elevation-reference-points.json` records five bilinear source heights at exact Float32 coordinates for application acceptance, including their original 2 × 2 pixel stencils. These references are guarded by the exact DTM source URL.

The published runtime data total **547,309,809 bytes** (approximately 547 MB). The compact independent reference pack is approximately 232 MB, including both GeoTIFFs and a portable QGIS preview. Unreferenced generated four-tile runtime duplicates were removed from Resources; the historical 4 km source pack and all standalone NASA/Stereo resources were preserved.

Open `Jezero10kmReference.qgz` in QGIS. All paths are relative within this folder. `Jezero10kmOverview.png` is its rendered and visually checked photograph. To reproduce preparation, use `tools/prepare_jezero_10km_zone.py` with the QGIS Python environment; it writes a staging folder without automatically replacing application resources. `tools/verify_jezero_10km_zone.py` checks that staging folder, or the published resources when staging is absent.
