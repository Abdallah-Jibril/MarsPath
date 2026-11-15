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

## Match to the challenge resource list

This audit distinguishes actual data inputs from reference portals and other products published by the same agency. Shared agency attribution does not mean the exact listed website or dataset was integrated.

| Challenge resource | Verified project role | Evidence and scope |
| --- | --- | --- |
| [JPL Mars Trek](https://trek.nasa.gov/mars/) | Documented source exploration and geographic comparison | Reference portal; the Unity terrain engine reads prepared source products rather than a live Trek embed or API. |
| [USGS–NASA planetary mapping portal](https://astrogeology-usgs.hub.arcgis.com/pages/interactive-maps-mars) | Related USGS planetary products are direct inputs | The shipped Jezero DTM and matching orthomosaic are Mars 2020 TRN HiRISE products. No geological-unit layer from the listed interactive-map portal is integrated. |
| [Curiosity proposed Mount Sharp route](https://science.nasa.gov/resource/curiositys-proposed-path-up-mount-sharp/) | Not an input | The five Jezero alternatives are computed by the project's graph searches, not copied from this route map. |
| [NASA Worldview](https://worldview.earthdata.nasa.gov/) | Not integrated | Worldview serves Earth-observation imagery; it supplies no Mars terrain to this project. |
| [Perseverance Raw Images Gallery](https://mars.nasa.gov/mars2020/multimedia/raw-images/) | Related Perseverance photographs are used; direct gallery ingestion is not evidenced | The rock material uses the published NASA WATSON Dourbes image PIA24940, Figure 1. Belva and delta panoramas are appearance references. These are credited published image products, not an asserted Raw Images API integration. |

The direct NASA-related input families include MRO/HiRISE elevation, registered orbital imagery, MGS/MOLA global topography, Viking global imagery, and rover photographs. This supports a claim of multiple NASA data products, but does not establish use of two or three of the exact five challenge links. Any formal minimum-resource rule must be checked against the challenge's actual requirements.

## Additional providers and research references

- **ESA / DLR / Freie Universität Berlin:** HRSC Mars Express elevation strips and regional support. The original science-team service is [HRSC webGIS](https://maps.planet.fu-berlin.de/); the project also contains the joint HRSC/MOLA 200 m blend. These regional sources are separate from the 1 m HiRISE master used by the detailed Jezero demo.
- **USGS / IAU Gazetteer:** named landmark coordinates and labels, documented in the globe source notes. This is a separate use from geological classification.
- **Weir (1949), with Chen et al. (2018):** the respiratory-calorimetry equation used to convert modeled metabolic power into oxygen demand. **Cavagna et al. (2000)** supplies partial-gravity walking research context, not a fitted calibration of this suit model. Exact primary links and limitations are in [Physics and Hazards](PHYSICS_AND_HAZARDS.md#12-primary-references).
- **University of Arizona and Washington University PDS hosting:** source catalogues and NASA mission-data distribution. University hosting does not make the underlying NASA instrument data an independent non-NASA observation.
- **Esri:** hosting of NASA/JPL/USGS Viking imagery used in the regional visual basemap. Hosting is distinct from producing a new planetary dataset.

Unity, OpenXR, QGIS, and GDAL are implementation or preparation tools, not additional Mars observation sources. Preserve source-product credits and the existing third-party notices. Use of NASA material does not imply NASA endorsement.

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

## Physiology, energy, and alert provenance

The [EVA physics and hazard note](PHYSICS_AND_HAZARDS.md) provides equations, units, parameter classifications, and primary links. It references NASA OCHMO-TB-047 for nominal crewmember context, the Weir equation for respiratory calorimetry, and the legacy EMU 2000-series battery for an identified capacity reference. These sources do not validate the application's assumed suited-walking or electrical-load coefficients. Rock footprint thresholds and DEM slope caution levels are explicitly documented demonstration policies.
