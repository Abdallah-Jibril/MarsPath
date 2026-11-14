# Native HiRISE source and registration update — 7 November 2026

The laptop remains the acceptance target. No headset transfer or Quest readiness claim is part of this update.

## Sources acquired and checked

The Jezero reference uses the actual `DTEEC_045994_1985_046060_1985_U01` stereo elevation model. Its native projected cell spacing is 1.016553 m. The original, matching `ESP_045994_1985_RED_A_01_ORTHO` image has a genuinely finer 0.254138 m grid; the previously bundled RED_C image is 1.016553 m. These are different source products, not software upscaling. Both are listed on the [official HiRISE product page](https://www.uahirise.org/dtm/dtm.php?ID=ESP_045994_1985).

A native 3072×3072 A-image crop is now bundled for offline use. Original GeoTIFFs, exact affine transforms, source URLs, hashes, a portable QGIS project and measured reference points are in [the Jezero source pack](../GIS/OfficialSurfaceSources/JezeroHiRise/README.md). Image display uses a documented linear contrast stretch; it does not alter terrain heights or invent image detail.

The historical ridge reference is 18.46470 N, 77.42535 E to 18.46820 N, 77.42535 E. The native model gives a 56.07 m rise over approximately 207.46 m, but the updated team solver rejects an endpoint/segment exceeding the production slope constraint. The optional **HIRISE / 1 m TERRAIN EXAMPLE** button therefore now explicitly selects an independently checked 120 m scarp-approach route: 18.4620552 N, 77.42473 E to 18.4640789 N, 77.42473 E. All five alternatives are available under the unchanged 25-degree constraint. Manual selections and unrelated user routes are not moved.

The eye-level check looks from 18.46205517 N, 77.42472712 E (−2541.2722 m) toward 18.46402740 N, 77.42277203 E (−2474.7068 m), a 66.57 m rise. It changes the camera heading at the accessible route start, rather than claiming an admissible route directly up the cliff.

An additional NASA PDS/UCLA Peridier product was acquired and inspected, with a native 1 m DEM, a native 1.02694 m matching image and an 18 m paired CTX DEM. [The source pack](../GIS/OfficialSurfaceSources/Peridier2023/README.md) records exact selected-route intersections, source gaps, correlation artifacts and inter-product height differences. It is retained as a research layer rather than automatically overriding cleaner production terrain. Its nominally finer spacing does not establish better accuracy. In particular, its full footprint misses the selected Balanced route by approximately 284 m, and its height registration differs from the old CTX product by tens of metres at the sampled points. This evidence does not justify a claim of 1 m coverage along the full 199 km route.

## Production fixes

- Matching HiRISE/CTX orthophotos can now stream independently of geometry while the astronaut moves. Previously the production surface callers explicitly disabled imagery in elevation requests, leaving new matching orthophotos unloaded.
- Future-station image requests populate caches only. The current geographic station selects and activates the finest available matching image, within the existing two-image-job limit. Optional images do not add another blocking entry stage.
- Native source spacing is read from the actual image window. A known, verified Jezero A-image URL is preferred, with the catalog C product as fallback; no filename substitution is assumed for unverified products.
- Valid dark pixels are retained. Both shaders now use source alpha for nodata, instead of suppressing low luminance. The old mask attenuated 3.402% of valid pixels in the previous Jezero C crop.
- Exact image bounds remain doubles until they are subtracted from each mesh's own geographic origin. A cancellation-free relative geodesic calculation maps both planner and ground fragments into native image pixels. The previous global float UV subtraction had approximately metre-sized steps near this longitude, larger than the new 0.254 m pixels. Each terrain snapshots its own frame so an asynchronous refresh cannot borrow another view's origin.
- Planner zoom also requests matching native orthophotos independently of CTX, with one bounded eight-second job and current-view-only activation. This fixes the same missing-photograph issue before helmet entry.

The source providers, physical 1:1 height scale and team routing algorithms remain shared between planning and walking. Coarser sources elsewhere cannot resolve small pits or individual boulders. The illustrated rock scatter is not a survey of actual rock positions, and source cell spacing is not a claim of absolute measurement accuracy.

## Verification

The native desktop review passed against bundled sources with persistent caches disabled. All five routes are available for the new optional scarp approach. First-use planning took 1.735 s in this run. At four independently chosen points, visible planning triangles differed from the shared measured provider by at most 0.164 m. At the eye-level scarp, the near ground collider retained a 66.498 m rise versus the provider's 66.561 m, a 0.063 m rendering/interpolation difference. This measures renderer agreement with the source, not absolute NASA measurement accuracy.

The review verified the exact original A-image URL, 3072-square crop, actual 0.254138 m spacing, texture binding and local registration in both views. A real 1.70 m eye and one-metre scene units were retained. Both very dark and bright opaque source-pixel self-checks passed. Screenshots and the portable report are included in `outputs`; the optional diagnostic runs with `--mars-hirise-native-review` and emits `HIRISE_NATIVE_REVIEW_PASSED`.

The final desktop release passed all three checks with exit code 0: `HIRISE_NATIVE_REVIEW_PASSED`, `REPORTED199_PASSED`, and `MARSPATH_RUNTIME_TEST_PASSED`. The 199 km check covers bounded crack-free adaptive planning geometry, original source heights, matching rim/floor/plateau stations, exact endpoints and the two-minute presentation. Release logs are `work/native-hirise-release-build.log`, `work/native-hirise-release-review.log`, `work/native-hirise-release-199.log` and `work/native-hirise-release-smoke.log` (machine-local logs are ignored by Git).

Open `MarsPathUnity/Builds/Windows/MarsPath.exe`, choose **HIRISE / 1 m TERRAIN EXAMPLE**, then **VIEW ROUTES** and **ENTER HELMET DEMO** or **START FULL MISSION**. This intentionally selected example demonstrates the actual high-resolution source footprint. Other selected locations retain their own source coverage and coordinates.

Evidence: [planner](../outputs/MarsPathHiRiseNativePlanner.png), [initial visor](../outputs/MarsPathHiRiseNativeRidgeVisor.png), [same-site scarp view](../outputs/MarsPathHiRiseNativeScarpVisor.png), [chase view](../outputs/MarsPathHiRiseNativeScarpChase.png), [native acceptance report](../outputs/NativeHiRiseReview.txt).
