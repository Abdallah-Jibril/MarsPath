# Measured surface and team routing — 6 November 2026

The walking horizon now has a source-adaptive context grid out to 60 km, with more samples around nearby measured rims. The source fingerprint checks the complete neighborhood, so finer off-axis elevation revisions rebuild the surface too. Streaming remains bounded to two geometry and two image jobs, with current/next stations and relief-ranked nearby requests. Entry geometry and close imagery share a five-second opportunity; refinement continues during playback.

Native CTX voids caused coarse fallback heights to form false cones. Original arrays remain unchanged. A separate bounded IDW display estimate handles only enclosed eight-connected voids of at most 512 cells and 400 m bounding-box diagonal. Actual equal/finer measurements take precedence. `GAP ESTIMATE` identifies estimated support; routing rejects those nodes and checks proposed edge interiors too. Larger/open gaps retain context fallbacks. H5271 original native 50 m HRSC adds real west-crater context. Its catalog contains the source URL, projection, exact crop and hash.

The real QGIS 3.44.15 project has seven native sources, Horn slope and 1:1 hillshade, relative paths, coordinate markers and two bookmarks. `GIS/terrain-audit.json` records every source hash and unchanged cell comparisons. Optional display-estimate masks independently match the runtime's 6,884 west-crater cells and 155 recent-corridor cells. Original slope/hillshade do not include estimated cells.

The user confirmed only algorithm specifications existed. New C# searches implement 3D-distance A*, lexicographic risk/distance Dijkstra, balanced and energy A*, ordered science stages, and RCSP multi-label A* with lower-bound/Pareto pruning. Resource budgets start disabled/blank. All objectives use one nonnegative edge model. Risk is quantized at 1e-9 risk·km for stable lexicographic ties. Label-cap results explicitly report bounded/approximate status. Pareto flags compare the five computed alternatives, not the full frontier.

Proposed path edges are checked against intermediate measured-source heights and void estimates. Invalid edges are removed before another bounded solve (up to 16 passes). Source-aware sampling is capped at 64 intervals per edge; this remains a sampled graph, especially for planetary routes, and is not a certification of every metre. The west-crater reference's dense profile maximum declined from 30.5° to 23.6° under the 25° routing limit after edge validation. Graph spacing is shown in route details.

## Validation

- Windows release build succeeded with Unity 6000.6.5f1.
- `TEAM_ROUTING_PASSED`: 40 randomized small graphs compared against independent simple-path enumeration; shortest, lexicographic risk, balance, energy, RCSP budgets, ordered stages, infeasible/bounded status, and unchanged/bounded native void policy.
- `MARSPATH_RUNTIME_TEST_PASSED`: local/continental/dateline/polar selections, exact original endpoints, full route, 120-second presentation, smooth local motion, arrows and camera bounds.
- `SURFACE_RELIEF_PASSED`: normal streaming enabled, real user budget recomputation, both visor/chase cameras, rendered height/provider agreement, bounded jobs, geographic endpoints, and measured horizon. Entry cases were 0.695 s and 3.141 s with existing local data/cache; these are not cold-network guarantees.
- Native screenshots are saved in `outputs/MarsPathRelief*.png` and `outputs/MarsPathTeamRoute*.png`.

## Remaining boundaries

Separate C++ deployment, external hazard/science layers and empirical model calibration are still pending. The implementation is C# in Unity. Individual small rocks are illustrative and grayscale orbital colorization is presentation shading. HiRISE/CTX metre-scale elevation coverage is regional, not global. Quest/OpenXR hardware integration and comfort/performance testing remain open.
