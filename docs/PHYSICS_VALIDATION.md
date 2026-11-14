# EVA Physics Update — Software Validation

**14 November 2026 / Unity 6000.6.5f1 / Windows x64 and Quest ARM64**

The scientific model, assumptions, equations, and warning policies are defined in [PHYSICS_AND_HAZARDS.md](PHYSICS_AND_HAZARDS.md). These checks establish internal software consistency. They do not validate astronaut physiology, local rock positions, soil traction, cooling capability, or flight suitability.

## Results

| Check | Result | Evidence |
| --- | --- | --- |
| Independent physics and geometry checks | Passed, 46 assertions | `MARSPATH_PHYSICS_CHECKS_PASSED` |
| Reference routing objectives and constrained budgets | Passed | `MarsTeamRoutingCheck.RunChecks`: 40 randomized graphs versus independent simple-path enumeration, plus stage/budget/status checks |
| Low/zero electrical edge costs | Passed | Energy A* and RCSP retain the feasible 0.001 kWh path under a 0.002 kWh budget |
| Encrypted two-way radio receive/playback | Passed in software | `MARSPATH_RADIO_REGRESSION_PASSED`; physical capture/audibility is outside this check |
| Actual DEM route facts | Passed | `ROUTE_FACTS_PASSED`: selected-route identity, source samples, coverage, caching, different region, preserved walking frame |
| Windows full application flow | Passed | `MARSPATH_RUNTIME_TEST_PASSED` |
| EVA balances, reserves and geographic movement | Passed | `MARSPATH_EVA_REGRESSIONS_PASSED`, `MARSPATH_FULL_JOURNEY_PASSED` |
| Continuous two-minute presentation | Passed | `MARSPATH_PRESENTATION_PASSED` |
| Metre-scale walk | Passed | Eight seconds moved 12.80 m; `MARSPATH_WALKING_PASSED` |
| Frame-step stability and origin rebase | Passed | Demo steps approximately 0.02667 m at 60 Hz; mission steps approximately 0.01162 m; same-location elevation residual 0.0000 m; `MARSPATH_REGIONAL_VISUAL_PASSED` |
| Belva showcase and controller state flow | Passed | `JEZERO_SHOWCASE_PASSED`: measured relief, continuous midpoint/end, alternate route, hold/release state |
| Windows package | Built successfully | `MARSPATH_BUILD_COMPLETE` |
| Quest package | Built successfully | `MARSPATH_QUEST_BUILD_COMPLETE` |
| Physical acceptance of these rebuilt packages on Quest | Pending | Requires an on-device user trial; no hardware trial claimed for this revision |

The application-flow and showcase checks ran in batch mode. They verify geometry, state, timing, and the rendering-data configuration, rather than image quality or physical controller tracking through a worn headset.

## Independent numerical examples

At 0.70 m/s, 122 kg combined mass, the configured reference atmosphere and default profile:

| Grade | Metabolic W | Electrical W | Oxygen g/h |
| --- | ---: | ---: | ---: |
| 0° | 298.715 | 74.936 | 75.174 |
| +15° | 622.715 | 87.036 | 156.710 |
| −15° | 360.307 | 82.115 | 90.674 |

The independent checks include STPD oxygen density, inversion of the Weir relation, `V × Ah` units, `m g v sin(theta)`, signed external work, positive descent demand, nonzero stationary metabolism, barometric sign, input rejection, rate × elapsed-time balances, reserve clipping, and equivalent integration partitions at ×1/×30/×50.

Rock geometry checks cover forward/behind/off-corridor exclusion, horizontal footprint clearance, orientation invariance, map-scale conversion, and the joint count/footprint/distance field criteria. Gradient checks distinguish a percentage grade from an angle and combine both surface axes.

## Real showcase observations

The selected reference track is approximately 143.5 m, with sampled ascent 8.2 m and descent 25.4 m. The midpoint is exactly 50% and the endpoint geographic residual is zero at the test's precision. The source image is 0.25 m and the height grid remains 1 m. Five surface arrows remain visible.

At the midpoint, modeled reserves were approximately 95.548% oxygen and 93.333% battery, starting from 96% and 94%. Selecting alternative index 1 succeeded, and movement stopped on trigger release. These values are **model outputs**, not actual suit telemetry.

The corrected electrical objective can select a corridor close to another objective. Minimum pairwise showcase divergence was approximately 7.9 m. The test requires resolvable distinct geometry at the source-post scale; a former arbitrary 12 m visual target is not imposed on route costs.

## Maintained regression expectations

Several older checks still described earlier demo behavior. They were updated to the already implemented continuous preview contract: 120 seconds covers the full short track or the first 192 m, rather than completing a continental route. The long-track preview moves at 1.6 m/s, approximately 0.02667 m per 60 Hz frame. The stationary initial metabolic reference is 139 W, rather than the former walking heuristic above 200 W.

The geographic, arrow visibility, reserve, source, route identity, and movement-state checks were retained. Route geometry and optimization costs were not manipulated to satisfy a visual separation target.

## Reproduction

Close the editor before batch builds. Use the matching Unity installation and the project at `MarsPathUnity`.

```powershell
Unity.exe -batchmode -nographics -quit -buildTarget Win64 -projectPath <absolute-project-path> -executeMethod MarsPath.Editor.MarsPhysicsRegressionChecks.CheckAndBuildWindows -logFile <absolute-log-path>
& .\tools\build-quest.ps1
.\MarsPathUnity\Builds\Windows\MarsPath.exe -batchmode -nographics --mars-route-facts-check -logFile <absolute-log-path>
.\MarsPathUnity\Builds\Windows\MarsPath.exe -batchmode -nographics -logFile <absolute-log-path>
.\MarsPathUnity\Builds\Windows\MarsPath.exe -batchmode -nographics --mars-jezero-showcase-review -logFile <absolute-log-path>
```

Generated logs are retained locally under `tmp/mars-eva-physics/` and the Quest build folder. They are excluded from source Git. The checked-in test code and this record provide reproducible verification scope. Distribution hashes are recorded in [PHYSICS_PACKAGES.json](PHYSICS_PACKAGES.json); the older import package record remains unchanged.
