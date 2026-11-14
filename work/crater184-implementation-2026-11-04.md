# Exact reported crater route: measured geometry and zoom

Reported coordinates: **24.15337 N, 83.02737 E → 26.60797 N, 84.74411 E**. This update reproduces that route rather than substituting the Jezero demonstration.

## Fixed causes

1. The overview automatically used a 6x vertical multiplier while the walking scene used 1x. Both now use physical height scale 1:1; all imagery/relief/slope and multiplier controls have been removed from the user interface. Legacy saved multiplier values are ignored.
2. Dynamic HRSC searches sent a zero-area geographic box. The official ODE service rejected it. Searches now use a small positive-area box and reject cached error responses.
3. Source-support blending used an integer minimum distance to nodata. Different source heights therefore produced artificial 4–5 m jumps over a 0.2 m walking step. Continuous support interpolation removes these steps while retaining all native bilinear height values. At the reproduced station, the native runtime now measures a maximum 0.0825 m change over a 0.2 m step in the tested 400 m cross-sections.
4. Planning arrows were explicitly disabled at close zoom by a scale threshold, and guidance could follow a source interpolant below the actual rendered triangles. Lines and chevrons now follow the visible mesh, use screen-adaptive widths, and remain depth-tested. Surface arrow arms are subdivided and attached to the exact near-ground triangles at a 0.18 m lift.
5. A fixed overview mesh and imagery were being enlarged during close inspection. A bounded view-centred measured mesh now replaces the coarse triangles under the inspection area. At the tested close view it samples approximately 20.7 m cells; cached matching orthos bind immediately. Network image refinement remains optional background work.
6. Ground context ended at 24 km and fog ended at 32 km, omitting distant measured landforms. The visible context now extends to 60 km with actual spherical Mars curvature; the near walking mesh remains a metre-scale tangent frame. No synthetic craters or elevation noise were added.

## Added original source data

The build includes native HRSC 75 m and 100 m elevations for the reported region, two native CTX stereo DTMs at about 20.7 m, and their matching registered grayscale orthoimages. Real nodata remains absent; it falls back to the next measured source. Original pixels, source URLs, hashes, map projections and independent GDAL checks are in:

- `work/crater184-official-data-2026-11-04.md`
- `work/crater184-ctx-bundle-audit.json`
- `work/crater184-hrsc-native-audit.json`
- `work/crater184-hrsc75-native-audit.json`
- `work/crater184-seam-continuity-audit.json`

HiRISE/CTX fine elevation coverage is not global. The selected start is genuinely gentle ground in these measured sources. The crater landforms occur later along the same full route; they are not moved underneath the selected start. Grid spacing is not a vertical-accuracy guarantee or a validation of individual hazards. Decorative rock positions and suit resources remain models.

## Rendering and bounds

Image footprints fade over 24 native pixels, with linear-space premultiplication, rather than pasting their nodata outline as a hard polygon. Monochrome ortho contrast is exposure-matched to the contextual colour. This changes display composition, not geographic registration or elevations. Near and horizon meshes retain exactly the same source heights. Fine imagery cannot reveal detail smaller than its actual source pixels.

Planning zoom tiles are capped at 257² vertices; selected-route refinement preserves all original segments with a 16k-point budget. Planning chevrons use a 48-object pool. Persistent caches are retained. New bundled source data makes the reported area available offline without blocking mission entry.

## Verification evidence

`--mars-crater184-review` reproduces the exact endpoints, selects Fastest, checks independent CTX heights, 1:1 geometry, source/mesh equality, three zoom levels, visible arrows, source continuity, several full-route stations and the exact destination. Native screenshots are actual application renders.

On the verified native run before final optical-only polishing: planner 0.962 s, visor entry 0.198 s; close tile 64,516 vertices / 20.69 m display cells; eight planning chevrons at each tested zoom and five initial surface arrows. Source/facts, large planetary route, transition and full runtime regression flags all exited 0 in the `work/crater184-*.log` files. Final rendering logs and screenshots are saved separately under the same prefix.

Final native render verification passed with exit 0 in `work/crater184-final-render-native.log`; the final shader build passed in `work/crater184-final-render-build.log`. The latest full runtime regression passed in `work/crater184-delivery-smoke.log`. Final native output images are `outputs/MarsPathCrater184*.png`. The final viewport resolves measured ~20.7 m display cells at close zoom; this does not create sub-grid measured detail. Small decorative rock positions and material grain remain explicitly illustrative.

Diagnostic renders with shadows disabled and then orbital imagery disabled showed that the remaining fine slope striping was not eliminated by removing shadows or the ortho alone. Wider source-aware lighting derivatives and retained foreground material grain reduce its emphasis; the interpolated 20–100 m source geometry should not be described as photorealistic metre-scale reconstruction. Heights and route continuity remain verified independently of that rendering limitation.
