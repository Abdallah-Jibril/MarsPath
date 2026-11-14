# MarsPath streamed terrain sources — 2026-10-27

This update loads bounded measured-source windows for arbitrary selected regions. It distinguishes image detail, elevation samples and displayed geometry. Build/runtime/network results are recorded separately; this note makes no claim about tests still in progress.

## Elevation

Product: USGS HRSC/MOLA blended DEM, 200 m v2, Fergason, Hare and Laura (2018).

- [USGS product inventory](https://fdp.astrogeology.usgs.gov/fdp/mars/)
- [Official raster](https://planetarymaps.usgs.gov/mosaic/Mars/HRSC_MOLA_Blend/Mars_HRSC_MOLA_BlendDEM_Global_200mp_v2.tif)

Verified layout: 106694×53347 signed little-endian Int16 samples, nodata −32768; Mars sphere radius 3396190 m; origin −180,90 and pixel step 0.003374120830641 degrees. The client reads required byte ranges, not the complete 11,384,463,908-byte file. Native-pixel crops are capped at 2048 samples per dimension and 48 MiB. Requests outside those limits retain available fallback instead of presenting a decimated grid as 200 m detail.

Expanded Jezero CTX elevation retains 20 m source samples where covered. The four bundled regional HRSC/MOLA crops and global MOLA remain offline fallbacks. Nominal grid spacing is distinct from true feature resolution. Independent GDAL reference samples at the reported region are retained in `research-reported-elevation-golden.json`.

## Orbital imagery

[NASA Mars Trek](https://trek.nasa.gov/tiles/apidoc/trekAPI.html?body=mars) lists the global CTX beta01 mosaic. Its [WMTS capabilities](https://trek.nasa.gov/tiles/Mars/EQ/CTX_beta01_uncontrolled_5m_Caltech/1.0.0/WMTSCapabilities.xml) point to:

`https://astro.arcgis.com/arcgis/rest/services/OnMars/CTX/MapServer/tile/{level}/{row}/{col}`

[Published metadata](https://astro.arcgis.com/arcgis/rest/services/OnMars/CTX/MapServer?f=pjson) identifies a preliminary **uncontrolled** grayscale mosaic with approximately 5 m source pixels. Credit NASA/JPL/MSSS/The Murray Lab/Esri. Display color combines CTX detail with Viking chroma; it is processed colorization, not measured natural RGB or elevation inferred from brightness.

The 512-pixel geographic tiles have origin −180,90 and span `180 / 2^LOD` degrees. Approximate equatorial display spacing is 81 m at LOD 8, 41 m at LOD 9, 20 m at LOD 10, 10 m at LOD 11 and 5 m at LOD 12. The roughly 75 km reported selection initially loads LOD 8. Smaller planning-camera footprints can load progressively sharper imagery after camera motion settles, up to LOD 12 where available. These requests update image materials only, preserving existing mesh heights and routes. Missing areas retain context.

The [NASA/JPL/USGS Viking MDIM 2.1 service](https://astro.arcgis.com/arcgis/rest/services/OnMars/MDIM/MapServer) supplies colorized fallback. Its native source is 231 m/pixel; the bundled 8192×4096 global display represents about 2.6 km/pixel at the equator.

## Geometry, cache and interpretation

The regional boundary matches the coarser planet's position, normals and UV coordinates, with consistent material treatment. Orbital views use small stars; EVA uses the atmospheric sky. Ground exploration retains 1 world unit = 1 metre and 1:1 macro source height. Planning relief remains exaggerated for inspection.

The adaptive planning grid is capped at 769 vertices per dimension. A 160 km-wide patch therefore has approximately 208 m vertex spacing at that cap. A 5 m-source image, a 200 m-source DEM, this mesh spacing and the 73×73 A* grid are separate resolutions. The image can show genuine small features that geometry or routing does not resolve. Fine soil, boulders, science candidates and hazard models remain simulated.

Checksummed source windows are reused under `Application.persistentDataPath/RegionalSources/v3`. Requests have cancellation and time limits; failures retain available bundled/cached coverage. Loading finer camera imagery must not mutate the elevation provider beneath an existing mesh.

Detailed stereo elevation is regional. [USGS CTX DTM documentation](https://stac.astrogeology.usgs.gov/docs/data/mars/ctxdtms/) explains artifacts, feature-resolution limits and offsets between independently controlled products. Queries of official CTX and HiRISE DTM catalogs for 80.3–81.7 E, 25.6–26.8 N returned no features at research time. That does not exclude every research-team product; it means this update must not promise metre-scale or 20 m elevation there. Raw queries and further references are in `research-terrain-sources-2026-10-27.md`.
