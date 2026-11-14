# Route Planning Algorithms

## Implemented engine

MarsPath computes routes in C# inside Unity. The main files are `MarsPathUnity/Assets/Scripts/Data/MarsGraphRouting.cs` and `MarsRoutePlanner.cs` in the same folder. A separate C++ service in the original architecture remains a future integration.

Measured DEM samples define a terrain graph. Candidate edges account for geographic distance, elevation change, slope restrictions, valid-data support, and the selected modeled objective. The grid planner uses eight-connected movement and diagonal checks. Optimality is limited to the represented graph and solver conditions.

## Objectives

| Profile | Search | Objective |
| --- | --- | --- |
| Fastest | A* | Accumulated 3D edge distance. Displayed time uses a speed model. |
| Safest | Lexicographic Dijkstra | Modeled risk, then distance. This is not a measured astronaut-safety probability. |
| Balanced | A* | Distance plus nonnegative integrated risk. The current graph scalar cost is distance + `2000 × risk`. Calibration remains necessary. |
| Energy efficient | A* | Estimated energy accumulated along the track. |
| Science priority | Stage-expanded A* | Ordered candidate science stops, represented by a search stage. |
| Constrained Balanced | Multi-label A* for RCSP | Shared risk, time, and energy budgets with the route objective. |

Automatically proposed science locations remain illustrative until connected to validated scientific target layers.

## Constraints and pruning

- Default configurable slope threshold **25°**.
- Invalid terrain samples and transitions excluded.
- Resource labels retain risk, time, energy, stage, and position.
- Pareto dominance removes labels unable to improve another label at the same state.
- Lower bounds prune labels unable to meet remaining budgets.
- Ordered science stops and return-to-base are represented in routing options; some workflows need additional UI.
- Label caps protect responsiveness. A bounded-search result is not proof of physical infeasibility.

The interface's Pareto comparison evaluates the five calculated alternatives, rather than every possible nondominated Mars route.

## Route facts

`MarsRouteFacts.cs` samples the chosen geographic track for source-aware elevation statistics. The interface reports distance, estimated duration, sampled ascent/descent, maximum track grade, and elevation profile.

Slope and ascent depend on source support and sampling. Image sharpness is independent of height spacing. Oxygen, electrical use, science preference, and risk need mission calibration before operational interpretation.

## Verification

`MarsTeamRoutingCheck.cs` compares objectives and constraints with exhaustive solutions on 40 small randomized graphs, including stages, budgets, and bounded-search status.

```powershell
.\MarsPathUnity\Builds\Windows\MarsPath.exe --mars-team-routing-check -batchmode -nographics
```

Expected marker: `TEAM_ROUTING_PASSED`. The imported baseline passed previously; rerun after routing changes. `GIS/team-algorithm-status.md` labels its older audit table as superseded. Use the current source and this document for present behavior.
