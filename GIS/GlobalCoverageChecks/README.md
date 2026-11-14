# Independent global Mars elevation check

This is a native-pixel check from the official USGS HRSC/MOLA global 200 m product, independent of the Unity application and outside Jezero. Only a 4 × 4 source window was extracted; the saved GeoTIFF preserves the source Int16 values, grid, CRS and nodata without resampling.

Source: https://planetarymaps.usgs.gov/mosaic/Mars/HRSC_MOLA_Blend/Mars_HRSC_MOLA_BlendDEM_Global_200mp_v2.tif

The source is **geographic Mars 2000 Sphere in degrees**, radius 3,396,190 m. Its affine is `[-180, 0.003374120830641, 0, 90, 0, -0.003374120830641]`. The filename's nominal 200 m spacing describes the equatorial sample distance; it does not make the TIFF affine a metre grid. Do not use equirectangular metre coordinates directly against this degree affine.

At C# single-precision coordinates `24.8f,14.2f`, whose exact double representations are latitude `24.799999237060547`, longitude `14.199999809265137`, the independent bilinear result is **-2090.7139304122175 m**. Pixel-center index is column `57555.23364347237`, row `19323.052426115413`. The 2 × 2 source stencil is:

```text
             col57555  col57556
row19323       -2091     -2090
row19324       -2090     -2089
```

Weights use `tx=0.2336434723692946`, `ty=0.05242611541325459`. All four pixels are valid. Source nodata is -32768; scale 1 and offset 0. The 4 × 4 native crop starts at column 57554, row 19322. Detailed source dimensions, exact CRS, crop values, affine, coordinates and SHA-256 hashes are in `USGS_Global200_Arabia_native4x4.json`.

`global-terrain-fallback-audit-20261108.json`, if present, is a preserved copy of the earlier application source/fallback audit. It is separate evidence from this independent GDAL source-pixel extraction.
