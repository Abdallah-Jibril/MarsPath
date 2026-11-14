# Mars presentation and measured surface review — 8 November 2026

Desktop acceptance remains the target. This update does not export to Quest or claim that every metre of Mars has fine stereo coverage.

## Changes

- Global START/DESTINATION markers now use the same transparent 📍 raster as regional planning. Pin tips retain the projected geographic locations; compact labels are arranged together, kept apart and clipped to the map viewport. Coincident points can share a pin location without overlapping their labels.
- Monochrome CTX/HiRISE imagery receives a shared ferric brown/orange display palette in planning and walking. Source luminance and valid dark pixels survive the tint; pale highlights desaturate smoothly. This is presentation colorization, **not a true-color scientific product**. Genuine color context is no longer forcibly converted to gray.
- Less image emission and a restrained cue from smooth measured mesh normals improve depth perception. Default planning pitch is 43 degrees; local inspection uses 40 degrees. Route framing is recalculated at that angle. Source elevations and physical vertical scale remain 1:1.
- Illustrative rocks form geographically stable, irregular patches with aligned fragments, partly buried slabs, sparse larger blocks and bare areas. The 224-object cap and pooling remain. Rock proximity alerts test exposed model height. **Individual rock positions and shapes are modeled, not measured NASA obstacles.** They do not change DEM geometry or scientific routing costs.
- Stereo requests try broad CTX support before narrow HiRISE windows: at most two CTX candidates, ten seconds per request, twelve seconds for the CTX phase inside the existing thirty-second total. Cached sources remain immediately usable. Same-kind cache skipping now requires equally fine or finer actual spacing and full non-estimated coverage, so a coarser cached DTM cannot suppress a finer candidate. Bundled Jezero 20 m support remains eligible.
- `MarsMissionEvidence` saves the most recent mission position, original endpoints, route/mode, measured elevation and active source/grid labels locally in `Application.persistentDataPath/LastMissionScene.json`, throttled to three seconds or rebasing/new missions. Opt-in diagnostic runs use `LastDiagnosticScene.json` separately. Nothing is transmitted. This makes a closed-app terrain report reproducible without asking the user to remember coordinates.

## Verification

- Windows release build and existing headless runtime suite passed. These include rock sensor/rebase stability, full journey, original endpoints, walking continuity, 3–6 forward arrows, 120-second completion, planner framing and planetary/dateline cases.
- Native HiRISE review passed with persistent source caches disabled. At the reference scarp the rendered lower/upper elevations were −2541.277 m and −2474.779 m: a 66.498 m rendered rise, compared with about 66.561 m from the shared native 1.016553 m DTM provider. The original matching 0.254138 m RED_A orthophoto was bound and locally registered; the camera eye remained 1.70 m. These tolerances measure rendering agreement with source data, not absolute survey accuracy.
- The reported 199.6 km route passed crack-free adaptive geometry and colocated rim/floor/plateau checks. Planner construction took 2.435 s in the release review. Helmet stations retained the same geographic coordinates and provider heights (−1729.243, −1284.339, −3725.123 and −1698.047 m); sources were 75 m HRSC, 20.8 m CTX and 100 m HRSC as appropriate. Both full-mission checkpoints and two-minute demo completion passed.
- Global native review passed seven measured-provider sites, three planner/helmet scenes, compact overlapping-endpoint layouts and a near-antipodal 10,700 km plan. That plan constructed in 1.479 s while reading 35.67 MB from MOLA. These are local/cached diagnostic timings, **not cold-network guarantees**. Visor horizon/provider differences were at most 0.030 m in those diagnostic samples.
- The reviewed cached-resolution tolerance was checked for equal/finer/coarser combinations, including overlapping 2.037 m → 1.027 m HiRISE and coarser CTX → 20.374 m upgrades. Actual network completion remains dependent on the remote source and connection.

Release logs are machine-local under `work/voice-terrain-20261108-*`; build outputs and logs are intentionally ignored by Git. The source branch can rebuild the desktop application.

## Evidence and source limits

[Global 📍 layout](../outputs/MarsPathGlobalEmojiPins.png) · [HiRISE planner](../outputs/MarsPathWarmMeasuredPlanner.png) · [HiRISE visor](../outputs/MarsPathWarmMeasuredVisor.png) · [199 km planner](../outputs/MarsPathWarm199Planner.png) · [same-route rim visor](../outputs/MarsPathWarm199RimVisor.png).

The high-resolution reference is intentionally the selectable **HIRISE / 1 m TERRAIN EXAMPLE**. Other locations keep their actual coordinates and available source spacing. MOLA supplies global coverage; stereo models provide finer selective coverage. A 75–926 m height grid cannot establish the shape of a one-metre pit or a rock. Enlarging or recoloring an image does not provide those measurements. No quarter-planet metre-scale completeness is claimed.

Original data, independent measurements and QGIS projects remain in [the Jezero source pack](../GIS/OfficialSurfaceSources/JezeroHiRise/README.md), [global coverage checks](../GIS/GlobalCoverageChecks/README.md) and [the terrain handoff](../HANDOFF.md). The [official HiRISE product page](https://www.uahirise.org/dtm/dtm.php?ID=ESP_045994_1985) identifies the reference stereo DTM and matching image. The [official PDS HRSC archive](https://pds-geosciences.wustl.edu/missions/mars_express/hrsc.htm) and [DLR public products](https://hrscteam.dlr.de/public/data.php) distinguish regional stereo elevation products from color image mosaics.
