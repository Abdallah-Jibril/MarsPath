# NASA and Planetary Data Sources

## Source policy

Published planetary products supply terrain and imagery. GIS notes, source manifests, and resource metadata retain product URLs, identifiers, transforms, and hashes where available. Source spacing, display sampling, mesh density, and scientific accuracy are distinct.

Detailed demonstrations cover Jezero's western delta and Belva. Global navigation provides context with coarser or geographically limited regional coverage elsewhere.

## Core sources

| Source | Use | Reference |
| --- | --- | --- |
| USGS Mars 2020 TRN HiRISE DTM | Native 1 m elevation master for the mission sector | [USGS DTM](https://astrogeology.usgs.gov/search/map/mars_2020_terrain_relative_navigation_hirise_dtm_mosaic) |
| Matching TRN HiRISE orthomosaic | Registered photographs, selected native 0.25 m crops, declared reductions | [USGS orthomosaic](https://astrogeology.usgs.gov/search/map/mars_2020_terrain_relative_navigation_hirise_orthorectified_image_mosaic) |
| HiRISE DTM products | Stereo coverage research and local source comparisons | [Catalogue](https://www.uahirise.org/dtm/) · [DTM map](https://www.uahirise.org/hiwish/maps/dtms.jsp) |
| MGS MOLA MEGDR | Global macro-elevation context and fallbacks | [PDS Geosciences](https://pds-geosciences.wustl.edu/missions/mgs/megdr.html) |
| HRSC/MOLA blend | Regional blended elevation support | [USGS 200 m blend](https://planetarymaps.usgs.gov/mosaic/Mars/HRSC_MOLA_Blend/Mars_HRSC_MOLA_BlendDEM_Global_200mp_v2.tif) |
| CTX stereo/imagery and HRSC stereo | Regional detail where supported by the catalogue/cache | Exact products in GIS and runtime source metadata |
| NASA Viking / USGS imagery | Global visual basemap | [NASA Mars resources](https://science.nasa.gov/3d-resources/mars/) |
| NASA Mars Trek | Source exploration and geographic comparison | [Mars Trek](https://trek.nasa.gov/mars/) |
| USGS Gazetteer | Landmark names and locations | [Planetary names](https://planetarynames.wr.usgs.gov/) |

HiRISE is a NASA Mars Reconnaissance Orbiter instrument. HRSC is an ESA/DLR instrument. Retain the individual producers' credits. Mars Trek is a reference portal; Unity reads prepared products and documented services rather than embedding the Trek website as its terrain engine.

## Jezero production region

| Quantity | Published package |
| --- | --- |
| Selectable area | Approximately 9.999914 × 10 km |
| West / east | 77.33578971367626° E / 77.51365504968241° E |
| South / north | 18.38445484384847° N / 18.553160892429624° N |
| Native height grid | 1 m |
| Valid original crop samples | 105,851,280 / 105,851,280 |
| Crop relief | Approximately 345.925 m |
| Runtime sector package | 547,309,809 bytes |

The [complete sector audit](../GIS/OfficialSurfaceSources/JezeroMissionZone10km/README.md) records exact transforms, checks, and sources. Its CRS uses the Mars 2000 sphere and original equirectangular frame. Preserve the vertical reference and avoid assigning an Earth CRS.

Native DTM heights are retained. Wider matching photographs use a declared 1 m reduction from the producer's 0.25 m image. The overview is approximately 2.58 m. Native quarter-metre image windows apply only within their included coverage.

## Appearance references

- [Perseverance at Belva Crater](https://science.nasa.gov/resource/perseverance-takes-in-view-at-belva-crater/), NASA/JPL-Caltech/ASU/MSSS.
- [WATSON's view of Dourbes](https://science.nasa.gov/resource/watsons-view-of-dourbes-in-mars-jezero-crater/), NASA/JPL-Caltech/MSSS, PIA24940.
- [Curiosity sandstone grains](https://www.jpl.nasa.gov/images/pia20323-nodules-of-cemented-sand-grains-within-martian-sandstone/), NASA/JPL-Caltech/MSSS, PIA20323.

Rock materials use photographic references. Mesh geometry and placement remain illustrative. Hazard icons are generated UI illustrations separate from measured data. Warm coloration is a rendering treatment of largely monochrome orbital imagery.

## QGIS and reproducibility

Open `GIS/OfficialSurfaceSources/JezeroMissionZone10km/Jezero10kmReference.qgz` for the portable region reference. `GIS/MarsPathTerrain.qgz` holds broader checks. [GIS/README.md](../GIS/README.md) documents preparation.

`tools/prepare_jezero_10km_zone.py` and `tools/verify_jezero_10km_zone.py` prepare and validate coverage, registration, overlaps, source processing, and height stencils. Preparation needs QGIS Python. Prepared runtime data can be used without installing QGIS.

Valid samples may still contain stereo artifacts. Sharp imagery does not establish fine height accuracy. Coarse products cannot identify individual rocks. Route risk, science, suit resources, and energy remain demonstration estimates requiring calibration.
