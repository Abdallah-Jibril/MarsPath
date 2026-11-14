# MarsPath QGIS terrain inspection

This project was generated and opened with the real QGIS 3.44.15 Python runtime on 6 November 2026. Open `MarsPathTerrain.qgz` in QGIS. Its saved bookmarks switch between the NASA Trek west crater and the recent route corridor.

Each group contains the native DEM, a Horn slope raster in degrees, and hillshade with a vertical scale of 1. Slope layers are initially hidden and can be enabled in the Layers panel. The selected start, destination and reference crater are real coordinate markers. Layer footprints and internal source nodata remain visible. Colors and hillshade explain measured elevation.

Rebuild from the repository root with the QGIS Python runtime. The helper verifies original source hashes, copies every native DEM cell without resampling, compares every exported DEM cell, and checks independent Horn calculations against derived slope samples. The project uses relative paths, so the entire `GIS` folder can be moved together.

```powershell
& 'C:/Program Files/QGIS 3.44.15/bin/python-qgis-ltr.bat' tools/prepare_qgis_terrain.py
```

The seven sources are the bundled native CTX stereo windows `TrekWestF03` and `NewRouteJ01`, plus HRSC science-team strips H7407, H7432, H7457, H7482 and H5271. H5271 supplies an additional native 50 m context crop around the west crater. Original URLs, CRS, pixel transforms, input/output hashes, source ranges and verification results are recorded in `terrain-audit.json`.

Source DEMs stay in their original Mars projected metre grids. Slope uses Horn derivatives with one vertical metre per horizontal projected metre. Hillshade uses azimuth 315 degrees and altitude 45 degrees. The QGIS display reprojects layers on the fly. Projection distortion and source resolution remain relevant when interpreting small slope differences. Nodata cells retain source gaps.

The Unity runtime uses its own measured-elevation providers and the team searches are now implemented in C# (`MarsGraphRouting.cs`). This QGIS project supplies a reproducible analysis and visual cross-check; Unity does not invoke QGIS for each mission. A separate C++ deployment and external raster/JSON contract remain future work.

## Optional display estimates and masks

Pass `--include-void-estimates` to add two initially hidden layers for each CTX source. `visual-estimates` contains an explicitly estimated display surface in small enclosed source voids, and `estimated-mask` identifies those cells. The shipped project was generated with this option. The native DEM and its slope/hillshade retain the original source values and gaps.

The estimate policy matches `MarsDtmVoidEstimates.cs`. Eight-connected invalid components must be enclosed, contain at most 512 cells, and have a bounding-box diagonal of at most 400 m. Each estimated cell uses all original valid neighboring boundary cells, weighted by inverse squared pixel distance and clamped to the original boundary range. Larger/open gaps retain nodata. Estimated cells require exclusion from a measured routing grid. Current independent masks contain 6,884 cells for TrekWestF03 and 155 cells for NewRouteJ01.
