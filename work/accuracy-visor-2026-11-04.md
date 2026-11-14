# Bounded planetary planning, visor UX, and terrain coverage

## Actual source investigation

The reported visor position is **22.64165 N, 65.79308 E**. It falls inside the STAC bounding box of the official USGS CTX stereo product `J19_052284_2030_XN_23N293W__K17_059959_2027_XN_22N293W`, nominal grid 20.112154 m. Bounding-box intersection does not establish valid height coverage. An independent GDAL read of original pixel **317,4387** returned **-32767**, that product's nodata value. Thus this finer CTX product cannot supply a measured height at the reported point; the application's native PDS HRSC 75 m fallback is appropriate with the checked sources. It cannot resolve a small crater below that grid. No matching HiRISE DTM covers that point in the app's official USGS catalog.

Evidence: `work/reported-22N-ctx-gdal.json`, `work/reported-point-height.txt`, `work/reported-point-probe.tif`, and `work/reported-22N-ctx-window.json`. The two-byte unsuccessful ENVI extraction files are diagnostic failures and are not shipped or used. The original point probe is a successful unmodified GTiff extraction, read using its actual projection and areoid heights.

Official CTX source: https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/ctx/controlled/usgs/J19_052284_2030_XN_23N293W__K17_059959_2027_XN_22N293W/J19_052284_2030_XN_23N293W__K17_059959_2027_XN_22N293W_dtm.tif

CTX DTMs have ~20 m posts, nodata holes and irregular footprints: https://stac.astrogeology.usgs.gov/docs/data/mars/ctxdtms/ . HiRISE DTMs typically have 1–2 m posts in selected stereo regions: https://www.uahirise.org/dtm/about.php . The bundled Jezero stereo example retains its measured 1.01655 m grid; individual displayed rocks remain modeled objects, not recovered measurements of the reported location.

## Freeze fixes

- The MOLA64 provider previously loaded 2.95 MB full-width latitude bands on sparse cache misses. Wide annular map samples repeatedly evicted eight cached bands, multiplying I/O and allocation. It now reads 128-value / 256-byte row segments with an O(1), 16,384-entry LRU, bounded to 4 MiB raster payload. Pixel-centre bilinear arithmetic and original values are unchanged. Reused cache blocks avoid per-miss allocation once full.
- Far map context uses the existing in-memory official MOLA16 display grid, smoothly joined to the measured inner boundary. At >=1,520 km regional span, the wide main overview also uses MOLA16 display heights and explicitly labels that display LOD. Route costs, original endpoints, physical elevation profile and metre-scale ground still use the finest available provider. Globe display detail does not turn into false metre-scale accuracy.
- Context extension is capped at one azimuthal-equidistant geodesic domain. Fine overview triangles and A* cells outside its antipodal disk are excluded, preventing repeated globe laps on near-antipodal journeys.
- A further geographic error appeared for opposite-longitude endpoints whose midpoint is exactly at the pole: scalar destination-longitude arithmetic lost precision through cancellation. Geographic destinations and map texture projection now rotate a position vector along its local north/east tangent. This preserves meridians at polar origins. The same east/west acceptance case improved from a spurious 22,276 km route to 9,644 km, with its actual route waypoints round-tripping to both selected endpoints.

## Source refinement fixes

- Cancelled/timed-out COG attempts are retryable after a bounded cooldown, rather than blacklisting a crop for the entire session.
- Already-loaded windows containing nodata at the focus are retained without repeatedly reading the same binary, regenerating edge maps and raising geometry revisions. Such a hole does not become a valid fine-resolution height.
- Optional background stereo refinement has time to finish (up to the provider's 30-second budget) while the existing local terrain remains usable. Moving away cancels the current focus. Loading indicators track active source work rather than marking every cache-only poll as a download.

## Visor changes

The persistent dock contains Pause/Resume, View, and Mission. Escape toggles Mission. The menu contains demo/timed playback, timed-only 1×/30×/50×, route timeline, source/mission details and return to planner. Opening the menu does not pause or switch playback mode. The dock/menu/details use the existing visor-safe transform, with warnings and timeline outside the side panels. Mission, route, camera changes and restarts reset optional panels. The source badge identifies regional resolution and unresolved sub-grid detail; the source sheet explains measured terrain versus modeled rocks/resources.

Acceptance flag: `--mars-accuracy-ux-review` covers near-antipodal, east/west and dateline routing with time/I/O bounds, exact full-route endpoint, unchanged measured HiRISE mesh, menu state without time/mode mutation, timeline and timed control behavior. Separate existing regression flags verify MOLA quadrant/pole samples and normal mission behavior.

## Final native verification

`work/nov4-accuracy-ux-final-native.log` passed with exit 0. Native 1600×900 measurements on this machine (excluding screenshot waits):

| Case | Computed route | Open planner | Logical raster I/O |
|---|---:|---:|---:|
| Equatorial near-antipodal east/west | 10,609.7 km | 0.815 s | 2,776,576 bytes |
| Opposite meridians, polar midpoint | 9,643.8 km | 0.816 s | 3,081,984 bytes |
| Antimeridian crossing | 59.0 km | 0.116 s | 70,656 bytes |

All five available route profiles preserve their actual endpoint coordinates within the test's angular tolerance, not merely the full-track endpoint override. The fastest route remains within the broad geodesic plausibility bound. Meter-scale mesh/provider equality, opening the Mission menu without changing pause/mode/progress, resetting panels and full route completion passed. Existing runtime regression coverage passed in `work/nov4-final-runtime-smoke.log`.
