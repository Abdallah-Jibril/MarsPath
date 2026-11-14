# Team algorithm and source audit

**Superseded baseline:** the table below intentionally records the pre-change audit. On 6 November the user confirmed there was no existing engine source, only algorithm names. `MarsGraphRouting.cs` now implements shortest A*, lexicographic risk/distance Dijkstra, balanced and energy A*, ordered science stages and shared-budget RCSP multi-label A* with Pareto/lower-bound pruning. `MarsTeamRoutingCheck.cs` independently enumerates 40 random small graphs and checks constraints, stages and bounded-search status. This implementation is C# inside Unity; it is not the report's separate C++ service. Default slope is 25 degrees. Route Limits and solver statuses are exposed in the desktop UI. Model calibration and external hazard layers remain open.

Reviewed on 6 November 2026 against the team's local `Roles/Algorithms.txt`, `Roles/resources -nasa.txt`, `Roles/Solution Implementation.txt`, and `First Stage Requires/MarsPath Report.pdf`. This records the pre-change Unity baseline inspected at commit `abe64ae`. Algorithm integration undertaken after this audit can supersede the table below.

## Agreed specification

The report's Path Planning Algorithms table specifies shortest-route A*, safest-route lexicographic Dijkstra, balanced A* with combined nonnegative costs, resource-constrained multi-label A*, and ordering of up to six selected science stops. Its initial slope threshold is configurable and starts at 25 degrees. The Roles algorithm document further specifies Pareto-dominance and lower-bound pruning, Pareto alternatives, stage-expanded ordered science waypoints and shared budgets for all legs.

The report describes DEM to QGIS to terrain grid to C++ routing to `routes.json`, consumed by Unity and Mission Control. QGIS is intended to align layers, mark missing cells and extract slope analysis. The source role document also asks for comparing Mars Trek CSV profiles and HiRISE heights at identical coordinates and compatible vertical datums.

## Current Unity implementation

| Agreed item | Current state in `MarsRoutePlanner.cs` |
| --- | --- |
| Eight-connected DEM graph | Implemented with cardinal and diagonal neighbors, measured node heights and diagonal corner checks. |
| Shortest route | FASTEST currently uses the same weighted A* solver, including nonzero risk and grade penalties. It does not select the pure 3D-distance objective described in the report. |
| Safest route | SAFEST increases A* risk/grade weights. The specified lexicographic Dijkstra solver is absent. |
| Balanced | Weighted A* is present, with prototype coefficients. |
| Resource-constrained optimum | Risk/time/energy budgets, multi-label state and Pareto pruning are absent. |
| Science stops | Three generated candidate points attract the SCIENCE cost. User-selected visits and exhaustive order search up to six stops are absent. |
| Risk layers | Grid slope is used. The full roughness, uncertainty, external hazard and obstacle-proximity density in the specification has not been integrated. |
| Slope threshold | Unity currently blocks cells above 35 degrees. The report's initial setting is 25 degrees. |
| Exactness and testing | The Unity runtime has smoke/regression checks. The Roles claim of 40 randomized brute-force cases could not be traced to algorithm engine/test source in the inspected project folders. |
| C++ integration | Current Unity client computes routes internally and reads bundled sample mission data. The report's external C++ `routes.json` engine has not been wired up. |

`MarsRouteFacts.cs` samples the actual selected geographic track for height statistics and source coverage. Suit oxygen/electrical use and route risk/science scores are prototype models. These values need the shared team engine/parameter contract before their meaning matches the agreed routing specification.

The terrain-rendering work preserves the existing Unity routing logic. The newly generated QGIS project independently verifies original height cells and derived slopes. The next algorithm integration step requires locating or receiving the team's C++ engine and its tests, JSON schema, weights and budgets. No such engine was found in the inspected repository or `Desktop/Nasa Hackthon` files.
