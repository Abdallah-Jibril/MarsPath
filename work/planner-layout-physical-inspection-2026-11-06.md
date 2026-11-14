# Endpoint labels and physical crater inspection — 6 November 2026

Scope: globe endpoint layout and regional planning only. The user deferred helmet work.

The reported selection is start −26.13901°, 48.54480° to destination −22.25368°, 51.56192°. Separate 01 START / 02 DESTINATION badges now avoid one another and landmarks, remain inside the globe viewport, and retain leader lines to the original geographic anchors. Close and coincident picks retain two distinct labels.

The vertical conversion was already physical 1:1. The actual problem was spatial sampling: the 485.342 km planning region's 513×513 overview has 1,184.92 m cells, despite a local 20.617 m source. The local cached CTX window covers only 31.668×31.668 km. Its original valid heights range from −907.83 m to −61.83 m; these are a source-window range, not a formally delineated crater depth.

A bounded fine-source mesh now remains around the planning target even at overview scale (82.47 m cells in this case). INSPECT TERRAIN and double-click terrain focus a real local ground footprint; the inspected mesh uses up to 385×385 vertices (54.98 m cells in this case). The source's heights and physical horizontal/vertical units remain unchanged, with the existing three-cell display seam transition. Larger source gaps and coarser coverage retain their existing limitations.

The camera retains the previous overview and restores it on ROUTE OVERVIEW; Fit View resets the route frame. A ground-distance ruler follows the current view. Header badges distinguish source-image spacing, height-source spacing and local mesh spacing. Existing cached midpoint terrain is restored before initial route generation.

Independent original Float32 reference cells are recorded in `planner-crater-source-audit.json`, including source URL and raw SHA-256. The native review compared both provider and displayed mesh elevations at three well-supported source cells: −834.888, −68.041 and −318.367 m. Agreement was within 0.018 m in this exact probe; numerical agreement with source cells is not a claim of centimetre survey accuracy or global 20 m terrain coverage. The test uses this local official DTM cache; it is not an offline bundled global product.

Validation: Windows release build; `--mars-planner-geometry-review` for actual globe labels, same/edge layout cases, original source heights, finer geometry and overview restoration; general runtime smoke for route framing, zoom, long routes, navigation and existing mission behavior. Both checks passed. Screenshots are in `outputs/MarsPathEndpointLabelsFixed.png`, `outputs/MarsPathPhysicalPlannerOverview.png` and `outputs/MarsPathPhysicalCraterDetail.png`.
