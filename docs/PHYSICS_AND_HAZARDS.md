# EVA Physics and Terrain Warnings

**MarsPath / LAGRANGE V / engineering model revision 1 / 14 November 2026**

This note documents the implemented equations, units, source references, assumptions, and validation boundaries. It replaces the earlier heuristic that mapped a route's energy score and absolute slope directly to suit consumption. The previous percentages were calculated estimates, not measured telemetry or a NASA-calibrated Mars EVA model.

The current implementation is a **physics-informed planning estimate**. Conservation of mass and energy, gravitational work, terrain geometry, and indirect calorimetry are established relationships. The walking coefficients, concept-suit electrical loads, capacities, and warning policies require calibration. No human has supplied validation measurements for this application on Mars.

## 1. Inputs and their provenance

| Input | Current value / units | Classification |
| --- | --- | --- |
| Elevation \(z\) | Metres above the source vertical reference | Measured-product estimate from the active DEM, including source uncertainty |
| Track grade \(\theta\) | Signed degrees, positive uphill | Derived from successive source elevations and metric horizontal distance |
| Surface gradient | Degrees, direction of steepest local slope | DEM finite difference over a declared source support |
| Planning speed | Base 0.70 m/s, reduced by the time model on slopes | Demonstration assumption; not an astronaut gait measurement |
| Reference Mars gravity \(g_M\) | 3.71 m/s² | Conventional Mars reference, consistent with ESA's approximately 3.711 m/s²; not a local gravity-field solution |
| Earth reference \(g_E\) | 9.80665 m/s² | Standard gravity convention |
| Person mass | 82 kg | Reference crewmember size used in NASA OCHMO-TB-047 |
| Suit plus carried mass | 40 kg | Configurable concept assumption; not a specified flight-qualified Mars suit |
| Nominal awake metabolism \(M_0\) | 139 W | NASA reference nominal awake activity, **not** basal metabolism or a live reading |
| Respiratory quotient \(RQ\) | 0.85 | Nominal reference; actual value depends on activity and diet |
| Oxygen capacity | 0.8 kg | Concept assumption; replace with the selected suit's specification |
| Battery | 16.8 V × 26.6 Ah = 446.88 Wh | **Legacy EMU 2000-series reference**, not a modern Mars suit specification |
| Oxygen / battery reserves | 20% / 15% of configured capacity | Demonstration operating policy |
| Rock footprints and heights | Currently rendered meshes, physical metres | Illustrative models; **not** surveyed real Mars boulders |

The source DEM's **post spacing is not its absolute accuracy**. A 1 m grid or quarter-metre photograph does not establish the height, shape, or location of every small rock. Terrain data sources and coverage are documented in [DATA_SOURCES.md](DATA_SOURCES.md).

## 2. Geometry, gravity, and motion

For consecutive locations with horizontal surface separation \(d_h\) and height difference \(\Delta z\):

\[
\theta=\operatorname{atan2}(\Delta z,d_h),\qquad
\ell=\sqrt{d_h^2+\Delta z^2}.
\]

Distance is computed in the Mars geographic frame, not an Earth CRS. The 3D segment length \(\ell\) and a segment duration determine speed along the ground \(v\). Horizontal and vertical velocities are:

\[
v_h=v\cos\theta,\qquad v_z=v\sin\theta.
\]

Mass does not change with gravity. A total 122 kg person-and-suit system weighs approximately **452.6 N** at the configured Mars reference gravity, versus 1196.4 N at standard Earth gravity. Inertia and physiological baseline demand remain; multiplying all human oxygen use by 0.38 is unjustified.

The mechanical work and vertical power are:

\[
W_{vertical}=m g_M\Delta z,\qquad P_{vertical}=m g_M v\sin\theta.
\]

For a 10 m ascent, \(W_{vertical}=4526.2\) J. This is mechanical work, not battery depletion and not the full food-energy cost of walking.

## 3. Explicit locomotion estimate

The application estimates metabolic power in watts:

\[
M=M_0+P_{flat}+P_{mobility}+P_{grade}+\frac{P_{drag}}{\eta_+}.
\]

\[
P_{flat}=c_f m v_h\left[(1-f_g)+f_g\frac{g_M}{g_E}\right],
\qquad P_{mobility}=c_s v.
\]

\[
P_{grade}=\begin{cases}
P_{vertical}/\eta_+ & P_{vertical}\geq0,\\
k_-(-P_{vertical}) & P_{vertical}<0.
\end{cases}
\]

| Coefficient | Value | Meaning and limitation |
| --- | --- | --- |
| \(c_f\) | 2 J/(kg·m) | Representative flat walking cost coefficient, assumed |
| \(f_g\) | 0.5 | Assumed fraction of flat walking demand sensitive to support weight |
| \(c_s\) | 60 J/m | Assumed pressure-suit mobility work per metre |
| \(\eta_+\) | 0.25 | Assumed positive-work metabolic efficiency |
| \(k_-\) | 0.8 | Assumed metabolic cost per joule of controlled negative mechanical work |

These coefficients are **not a published calibrated Mars walking equation**. The support/inertia split makes the reduced-gravity assumption explicit. Partial-gravity walking research motivates separating gravity-dependent mechanical work from other costs; it does not supply or validate these particular coefficients.

Controlled descent still consumes oxygen. The current conservative descent model adds braking effort; it does not reproduce the empirically observed full speed-and-grade metabolic curve, and may overestimate some gentle descents. Walking downhill never recharges the battery. Running, fatigue, individual fitness, load distribution, traction, and transient acceleration require additional calibration.

## 4. Oxygen: indirect calorimetry and a mass balance

The simplified non-protein **Weir equation** relates metabolic energy rate to respiratory gas exchange:

\[
\dot E_{met}[\mathrm{kcal/min}]=3.941\dot V_{O_2}+1.106\dot V_{CO_2},
\quad RQ=\frac{\dot V_{CO_2}}{\dot V_{O_2}}.
\]

Gas volumes are litres per minute under the physiological **STPD** convention: dry gas at 273.15 K and 101325 Pa. They are not litres measured in the ambient Martian atmosphere. Substituting \(RQ\) and converting joules to kilocalories gives:

\[
\dot V_{O_2}[\mathrm{L_{STPD}/min}]
=\frac{60M}{4184(3.941+1.106RQ)}.
\]

The ideal-gas volume-to-mass conversion is:

\[
\rho_{O_2,STPD}=\frac{p_{STPD}\,\mathcal M_{O_2}}{R T_{STPD}}
\approx1.42763\ \mathrm{g/L}.
\]

Here \(\mathcal M_{O_2}=0.0319988\) kg/mol. Numerically, kg/m³ equals g/L. The mass demand is:

\[
\dot m_{O_2}[\mathrm{g/h}]
=60\rho_{O_2,STPD}\dot V_{O_2}+\dot m_{leak}.
\]

At the configured \(RQ=0.85\), with leakage set to zero:

\[
\dot m_{O_2}\approx0.251656\,M\quad[\mathrm{g/h}],\qquad
m_{O_2,new}[\mathrm g]=m_{O_2,old}[\mathrm g]-\frac{\dot m_{O_2}[\mathrm{g/h}]}{3600}\Delta t[\mathrm s].
\]

NASA's brief uses 0.85 for nominal activity and 0.95 for exercise. Fixed 0.85 is a declared simplification, not an exercise gas measurement. Weir's equation converts the **assumed metabolic demand** to oxygen demand; using the equation does not validate the preceding gait model.

The assumed closed-loop suit supplies its own breathable oxygen. Actual usable duration must also include suit leakage, purges, reserve gas, regulator behavior, CO₂-removal capacity, water/cooling consumables, and relevant suit limits. Leakage currently defaults to zero because no measured leak specification is supplied. The displayed value is metabolic oxygen plus this configured loss, not the circulating ventilation flow.

## 5. Electrical energy is separate from metabolism

The physical battery balance is:

\[
E_{cap}[\mathrm{Wh}]=V_{nom}[\mathrm V]\,C[\mathrm{Ah}],\qquad
E_{new}=E_{old}-P_{electric}\frac{\Delta t}{3600}.
\]

No term directly subtracts the person's metabolic watts from the electrical battery. The body obtains metabolic energy from biochemical processes. Fans, pumps, electronics, and communication consume electrical energy.

Current concept-suit load assumptions are:

\[
Q_{body}=\max(0,M-P_{vertical}-P_{drag}),
\]

\[
P_{electric}=60+\min\left(45,15\frac{Q_{body}}{300}\right)\quad[\mathrm W].
\]

The 60 W base, 15 W per 300 W of body heat, and 45 W pump cap are **configurable design assumptions**. \(Q_{body}\) is a bookkeeping estimate for heat production, not cooling-system capability. Electrical power is not equal to rejected thermal power: a coolant pump can transport substantially more heat than its motor consumes. The proxy does not prove that any specific suit can reject this heat in the Martian environment.

Battery percentage uses remaining Wh divided by configured nominal Wh. It is an energy-based state estimate, not a measured voltage/state-of-charge curve. Temperature, battery aging, voltage sag, conversion loss, and actual load specifications are not yet characterized. The previous arbitrary 4 kWh capacity has been replaced with an explicitly identified legacy reference.

## 6. Mars atmosphere and excluded environmental effects

For still-air drag and context, a CO₂-dominated, isothermal reference atmosphere is used:

\[
p(z)=p_0\exp\left(-\frac{g_M\mathcal M_{CO_2}z}{RT}\right),\quad
\rho(z)=\frac{p(z)\mathcal M_{CO_2}}{RT},\quad
P_{drag}=\frac12\rho C_D A v^3.
\]

Defaults are \(p_0=610\) Pa, \(T=210\) K, \(\mathcal M_{CO_2}=0.04401\) kg/mol, \(C_D=1\), and \(A=0.7\) m². These are reference approximations, **not real-time weather at the chosen location**. Gravity is held constant at the reference value. At 0.7 m/s at the reference datum, still-air drag is about 0.002 W, far below locomotion demand.

Mars's atmosphere is unbreathable; low outside pressure does not reduce tissue oxygen requirements in proportion to ambient pressure. The suit must maintain an appropriate internal oxygen partial pressure. That regulator/pressure system is not simulated here.

Ambient temperature is not treated as skin temperature. Thermal radiation, sunlight and shadow, convection, dust, wind, heat storage, body temperature, and cooling-water limits are **not solved**. A future thermal solver would need the actual suit materials and boundary conditions. Therefore this model does not certify thermal safety or full EVA survivability.

## 7. Worked example from the implemented profile

Assume 82 kg body mass, 40 kg suit/cargo, 0.70 m/s **along the surface**, reference datum atmosphere, \(RQ=0.85\), and the coefficients above.

| Segment | Metabolic power | Electrical power | O₂ demand | O₂ capacity used in one hour | Battery capacity used in one hour |
| --- | ---: | ---: | ---: | ---: | ---: |
| Level | 298.7 W | 74.9 W | 75.2 g/h | 9.40 percentage points | 16.77 percentage points |
| +15° ascent | 622.7 W | 87.0 W | 156.7 g/h | 19.59 percentage points | 19.48 percentage points |
| −15° descent | 360.3 W | 82.1 W | 90.7 g/h | 11.33 percentage points | 18.38 percentage points |

These are **model examples, not astronaut measurements**. The same route can have multiple grades: the application integrates segment demands instead of applying one mean absolute slope. Planning and playback use the same rate calculator. Graph-edge integration, the resampled route profile, and live small time steps have different sampling supports, so small numerical differences remain.

The route's 0–100 ranking score is never used as an oxygen-demand input. The time estimate remains an assumed model: \(t_{edge}=\ell/(42\ \mathrm{m/min})[1+0.025|\theta_{deg}|]\). The profile total divides the estimated duration over 3D sampled segment lengths. Live playback integrates at no more than five mission seconds per step. Seek reconstruction is bounded to at most 4096 integration steps.

## 8. Slope warning from measured elevations

For a position on the measured DEM, samples one native post spacing to each side estimate the terrain gradient. The half-support \(h\) is at least 1 m:

\[
g_x=\frac{z(x+h,y)-z(x-h,y)}{2h},\quad
g_y=\frac{z(x,y+h)-z(x,y-h)}{2h},\quad
\alpha=\tan^{-1}\sqrt{g_x^2+g_y^2}.
\]

The signed track slope is derived along travel direction; the steepest surface slope \(\alpha\) also detects cross-slopes. These are different quantities. A 10% grade means \(\tan^{-1}(0.10)\approx5.71°\), **not 10°**.

The warning scans the current position and eight positions at 1 m increments ahead, taking the maximum DEM slope. It activates above **10°**. The configurable default graph limit is **25°**. Both numbers are demonstration policies, not NASA-certified astronaut limits. Their different roles are intentional: one gives early caution; the other excludes graph transitions.

Decorative microrelief, texture shading, and a display exaggeration must not create a measured slope warning. The new warning uses the source-height provider. With coarse DEM support, a local eight-metre warning cannot resolve eight-metre terrain; source support remains visible in details. Invalid or unrecognized small-scale hazards remain possible.

An ideal rigid-block traction relation illustrates why slope alone cannot establish safety:

\[
F_{downslope}=mg\sin\alpha,\quad F_{normal}=mg\cos\alpha,\quad
F_{friction,max}=\mu mg\cos\alpha,
\qquad \tan\alpha\leq\mu.
\]

Gravity cancels in that simplified ratio. Reduced gravity does not automatically make every steep slope safe. Mars regolith cohesion, sinkage, boot traction, surface layers, dynamic balance, and suit mobility are not measured by this demo. The application does not infer \(\mu\) from a photograph.

## 9. Rock warning: rendered obstacle geometry

The current detector queries the **rendered illustrative rock models**, not computer vision, a rover sensor, or an orbital rock catalogue. Its alert is geometrically consistent with objects visible in the simulation; it does not establish real rock locations at those coordinates.

For horizontal astronaut position \(p\), heading unit vector \(f\), rock centre \(c\), and footprint radius \(r\):

\[
d=c-p,\quad a=d\cdot f,\quad b^2=\|d\|^2-a^2,
\quad d_{clear}=\max(0,\|d\|-r).
\]

Here \(a\) is forward distance, \(b\) is lateral distance, and clearance is distance to the horizontal footprint boundary. All world coordinates are converted to physical metres.

The implemented rules are:

1. Exclude rocks behind the observer. The look-ahead range is 16 m plus the rock radius.
2. Exclude models under 0.10 m exposed height or 0.12 m footprint radius.
3. Require the footprint to intersect the widening corridor \(b\leq r+\operatorname{clamp}(0.53a,0.9,4.8)\), with distances in metres.
4. Sample the terrain at three fractions along the eye-to-rock-top line to reject some hidden rocks. This is approximate occlusion, not a complete visibility ray trace.
5. **Rock field:** at least four accepted models, summed footprint \(\sum\pi r_i^2\geq1.2\) m², and nearest clearance at most 12 m.
6. **Single boulder:** at least 0.22 m exposed height and 0.24 m radius, clearance at most 11 m, and lateral distance within \(r+1.9\) m.

The footprint sum may contain overlap. It is a **threshold feature**, not measured ground-cover fraction, true debris density, or a probability. These size, count, and distance thresholds are interface policy assumptions.

Rocks use repeatable geographic seeds and clustered procedural placement. Shapes/materials are informed by NASA rover photographs, but exact positions, individual sizes, and rock types are illustrative. Such meshes do not enter the scientific route graph as surveyed obstacles. A future real rock hazard layer would require validated detections, registration, uncertainty, and source provenance.

## 10. Time, reserves, and demonstration behavior

- Resource loss follows **simulated mission seconds**, not frame count. ×30 advances thirty mission seconds per real second, and consumes thirty times the corresponding resource demand for a constant state.
- In two-minute mode, 120 active seconds present the complete Jezero route or a continuous preview outside the sector. The presentation timer pauses with movement. The suit model uses represented mission time. In the full-route Jezero demo, the view can progress after the model reaches a reserve boundary, and the model holds its reserve values.
- Controller release or pause freezes the **simulation clock**. It does not imply an astronaut can stop breathing by releasing a trigger. A stationary state still has nonzero demand when mission time advances.
- Resource integration stops exactly at the first configured reserve boundary, rather than continuing at zero oxygen or power.
- Expedition mode assumes resupply every four mission hours. Those support locations are not verified. Reserve exhaustion can stop a leg earlier. This assumption is unsuitable for an operational route approval.
- Heart rate is not predicted from exertion in the new model. There is no live pulse sensor; the compatibility field retains the scenario's input baseline and is omitted from the main demand display.

## 11. Implementation and tests

| File | Role |
| --- | --- |
| `Assets/Resources/Science/MarsEvaModel.json` | Shared versioned parameter profile, Windows and Quest |
| `Assets/Scripts/Data/MarsEvaPhysics.cs` | Mechanical work, atmosphere approximation, Weir conversion, electrical demand |
| `Assets/Scripts/Core/MarsEvaSimulation.cs` | Mass/energy integration, reserve boundaries, numerical precision |
| `Assets/Scripts/Data/MarsRoutePlanner.cs` | Signed per-edge electrical demand for routing |
| `Assets/Scripts/Data/MarsRouteFacts.cs` | Signed route-profile integration for oxygen and electrical estimates |
| `Assets/Scripts/Core/MarsFullJourney.cs` | Live and seek integration at source locations |
| `Assets/Scripts/Data/MarsHazardPhysics.cs` | Source-gradient support, warning policy, rock footprint geometry |
| `Assets/Scripts/Visualization/MarsSurfaceRockScatter.cs` | Rendered obstacle list and approximate occlusion |
| `Assets/Editor/MarsPhysicsRegressionChecks.cs` | Independent balances, units, time scaling, reserve and geometry checks |

Paths in this table are relative to `MarsPathUnity`. The energy A* heuristic and constrained-search pruning now use the actual graph's minimum energy per metre. They no longer assume a 200 W minimum, which would overestimate the new concept-suit loads and could incorrectly discard a feasible route.

Run `MarsPath.Editor.MarsPhysicsRegressionChecks.Run` in the editor or in a Unity batch execution. `CheckAndBuildWindows` additionally runs the existing randomized routing checks, encrypted radio regression, and Windows build. Passing numerical checks establishes internal software consistency, **not physiological or flight validation**. Both platform packages require rebuilding after shared-code changes; physical Quest performance and audibility still need a device trial.

## 12. Primary references

1. [NASA OCHMO-TB-047, Crew Survivability](https://www.nasa.gov/wp-content/uploads/2024/07/ochmo-tb-047-crew-survivability.pdf), 1 July 2024, printed page 3: reference crewmember, nominal awake power, nominal/exercise RQ. The brief is reference material, not a waiver or certification.
2. [Weir, 1949, *New methods for calculating metabolic rate with special reference to protein metabolism*](https://pubmed.ncbi.nlm.nih.gov/15394301/), DOI 10.1113/jphysiol.1949.sp004363. [Chen et al., 2018](https://pmc.ncbi.nlm.nih.gov/articles/PMC5916490/), equation 4, explicitly presents the simplified non-protein form used here.
3. [NASA EMU Hardware Data Book, JSC-E-DAA-TN55224](https://www.nasa.gov/wp-content/uploads/2017/02/esoc-13_rev._v_emu_hardware_data_book_jsc-e-daa-tn55224.pdf), section 2.6, printed page 2-78: 2000-series battery reference. Do not confuse it with the 3000-series battery's different voltage range.
4. [ESA, Meet Mars](https://www.esa.int/Science_Exploration/Human_and_Robotic_Exploration/Exploration/ExoMars/Meet_Mars/%28language%29/eng-GB): approximate gravity and CO₂-dominated atmosphere. [NASA Glenn, Mars](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/mars/) gives planetary and atmospheric context. The implemented atmospheric constants are a simplified reference, not a local weather feed.
5. [Cavagna, Willems and Heglund, 2000, *The role of gravity in human walking*](https://pmc.ncbi.nlm.nih.gov/articles/PMC2270143/): partial-gravity external-work and optimal-speed experiments. External mechanical work is not identical to metabolic cost; these results do not calibrate the concept-suit coefficients.
6. [NASA partial-gravity traverse considerations, ICES 2025](https://ntrs.nasa.gov/api/citations/20250006519/downloads/ICES_final.pdf?attachment=true): partial-gravity and suited-locomotion research context. This application has not been validated against its participant measurements.

The defensible project claim is: **MarsPath compares routes on measured terrain and estimates resource demand using explicit physical balances and documented physiological conversion, with openly declared concept-suit and locomotion assumptions.** It does not certify a route as safe for a real Mars EVA.
