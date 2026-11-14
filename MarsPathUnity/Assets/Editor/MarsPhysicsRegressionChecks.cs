using System;
using UnityEditor;
using UnityEngine;

namespace MarsPath.Editor
{
    /// <summary>Independent dimensional checks, numerical balances and hazard geometry.</summary>
    public static class MarsPhysicsRegressionChecks
    {
        private static int checks;

        public static void CheckAndBuildWindows()
        {
            Run();
            MarsTeamRoutingCheck.RunChecks();
            MarsRadioRegressionChecks.Run();
            MarsPathProjectSetup.BuildWindowsPrototype();
        }

        [MenuItem("MarsPath/Check EVA Physics And Hazards")]
        public static void Run()
        {
            checks=0;
            var model=new MarsEvaPhysics.Profile();model.Validate();MarsEvaPhysics.Default.Validate();
            Near(MarsEvaPhysics.OxygenGramsPerStpdLitre,1.4276,.001,"STPD oxygen density in g/L");
            // Independent Weir calculation for 0.5 L/min O2 and 0.425 L/min CO2.
            double knownWatts=(3.941*.5+1.106*.425)*4184/60;
            Near(MarsEvaPhysics.OxygenStpdLitresPerMinute(knownWatts,.85),.5,1e-12,"Weir inverse");
            Near(model.BatteryWattHours,446.88,.001,"Nominal V times Ah is Wh, not kWh");
            var flat=MarsEvaPhysics.Evaluate(.7,0,0,model);
            var climb=MarsEvaPhysics.Evaluate(.7,15,0,model);
            var descent=MarsEvaPhysics.Evaluate(.7,-15,0,model);
            double expectedVertical=122*3.71*.7*Math.Sin(Math.PI/12);
            Near(climb.verticalMechanicalWatts,expectedVertical,.0001,"Mechanical climbing work m g v sin(theta)");
            Near(descent.verticalMechanicalWatts,-expectedVertical,.0001,"Descent has negative external work");
            Require(climb.metabolicWatts>descent.metabolicWatts&&descent.metabolicWatts>flat.metabolicWatts,"Declared controlled-descent cost stays positive");
            Require(descent.electricalWatts>0&&descent.oxygenGramsPerHour>0,"Walking downhill does not recharge the suit");
            Near(climb.heatRejectionWatts,climb.metabolicWatts-climb.verticalMechanicalWatts-
                .5*climb.airDensityKgPerCubicMetre*.7*Math.Pow(.7,3),1e-8,"Heat accounts for external mechanical work");
            var earth=new MarsEvaPhysics.Profile{gravityMps2=(float)MarsEvaPhysics.StandardGravity};
            Require(MarsEvaPhysics.Evaluate(.7,0,0,earth).metabolicWatts>flat.metabolicWatts,"Weight-dependent walking term responds to gravity");
            Near(MarsEvaPhysics.Evaluate(0,0,0,model).metabolicWatts,139,1e-9,"Standing still retains awake metabolism");
            Require(flat.metabolicWatts>0.378*MarsEvaPhysics.Evaluate(.7,0,0,earth).metabolicWatts,"Whole metabolism is not scaled by gravity ratio");
            Near(MarsEvaPhysics.AmbientPressure(0,model),610,1e-9,"Reference datum pressure");
            Require(MarsEvaPhysics.AmbientPressure(5000,model)<610&&MarsEvaPhysics.AmbientPressure(-3000,model)>610,"Barometric pressure sign");
            Reject(()=>MarsEvaPhysics.Evaluate(-1,0),"Negative speed rejected");
            Reject(()=>MarsEvaPhysics.Evaluate(.7,90),"Vertical grade outside model domain");
            Reject(()=>new MarsEvaPhysics.Profile{oxygenCapacityKg=0}.Validate(),"Zero capacity rejected");
            CheckTimeAndReserves(model,flat);
            CheckRoutingFloor();
            CheckHazards();
            Debug.Log($"MARSPATH_PHYSICS_CHECKS_PASSED: {checks} assertions; signed mechanical work, Weir/STPD conversion, time integration, reserves, graph lower bounds, DEM gradients, rock corridor geometry.");
            Debug.Log($"MARSPATH_PHYSICS_REFERENCE: flat@0.7m/s {flat.metabolicWatts:F3} W metabolic / {flat.electricalWatts:F3} W electrical / {flat.oxygenGramsPerHour:F3} gO2/h; +15deg {climb.metabolicWatts:F3} W / {climb.electricalWatts:F3} W / {climb.oxygenGramsPerHour:F3} gO2/h; -15deg {descent.metabolicWatts:F3} W / {descent.electricalWatts:F3} W / {descent.oxygenGramsPerHour:F3} gO2/h.");
        }

        private static MarsEvaSimulation Suit(MarsEvaPhysics.Profile model)
        {
            var suit=new MarsEvaSimulation();
            suit.Reset(new TelemetryData{oxygenPercent=100,suitPowerPercent=100,heartRateBpm=78},model);
            return suit;
        }

        private static void CheckTimeAndReserves(MarsEvaPhysics.Profile model,MarsEvaPhysics.Demand flat)
        {
            var single=Suit(model);single.AdvancePhysical(600,.7,0,0);
            Near(single.OxygenPercent,100-flat.oxygenGramsPerHour/6/(model.oxygenCapacityKg*1000)*100,.0001,"Oxygen mass balance");
            Near(single.PowerPercent,100-flat.electricalWatts/6/model.BatteryWattHours*100,.0001,"Electrical energy balance");
            foreach(int multiplier in new[]{1,30,50})
            {
                var pieces=Suit(model);double remaining=600;
                while(remaining>1e-8){float step=(float)Math.Min(remaining,multiplier/60.0);pieces.AdvancePhysical(step,.7,0,0);remaining-=step;}
                Near(pieces.OxygenPercent,single.OxygenPercent,.03,"Oxygen partition invariance x"+multiplier);
                Near(pieces.PowerPercent,single.PowerPercent,.03,"Energy partition invariance x"+multiplier);
            }
            var stopped=Suit(model);stopped.AdvancePhysical(1000000,.7,15,0);
            Require(stopped.ReserveReached,"Long integration stops at a reserve");
            Require(stopped.OxygenPercent>=model.oxygenReserveFraction*100-.001&&stopped.PowerPercent>=model.batteryReserveFraction*100-.001,"No reserve overshoot");
            float before=stopped.ElapsedSeconds;stopped.AdvancePhysical(600,.7,15,0);
            Near(stopped.ElapsedSeconds,before,.01,"No additional mission time after reserve stop");
            Require(stopped.ElapsedSeconds<1000000,"Clipped resource time is returned to playback");
        }

        private static void CheckRoutingFloor()
        {
            // Old 200 W heuristic wrongly preferred the direct 0.01 kWh edge,
            // and pruned the feasible cheap detour before its final zero-cost edge.
            var graph=new MarsGraphRouting.Graph(3);graph.x[1]=100;graph.x[2]=200;
            graph.AddEdge(0,new MarsGraphRouting.Edge(2,200,0,4,.01));
            graph.AddEdge(0,new MarsGraphRouting.Edge(1,100,0,2,.001));
            graph.AddEdge(1,new MarsGraphRouting.Edge(2,100,0,2,0));
            var result=MarsGraphRouting.Solve(graph,0,2,MarsGraphRouting.Objective.Energy);
            Require(result.Available&&result.exact,"Energy route exists with low-power edges");
            Near(result.totals.energy,.001,1e-12,"Energy A* retains admissible lower bound");
            var limits=new MarsRoutingOptions{ConstraintsEnabled=true,MaxRisk=1,MaxTimeMinutes=5,MaxEnergyKwh=.002f};
            result=MarsGraphRouting.SolveConstrained(graph,0,2,limits);
            Require(result.Available&&result.exact,"RCSP does not discard a feasible low-power path");
            Near(result.totals.energy,.001,1e-12,"RCSP energy budget");
        }

        private static void CheckHazards()
        {
            Near(MarsHazardPhysics.GradientDegrees(.1,0),Math.Atan(.1)*180/Math.PI,1e-12,"10 percent grade differs from 10 degrees");
            Near(MarsHazardPhysics.GradientDegrees(.3,.4),Math.Atan(.5)*180/Math.PI,1e-12,"Two-axis DEM gradient");
            Near(MarsEvaPhysics.SignedGradeDegrees(-10,100),-Math.Atan(.1)*180/Math.PI,1e-12,"Signed track descent");
            Vector3 origin=Vector3.zero,forward=Vector3.forward,center=new Vector3(1,0,6);
            bool accepted=MarsHazardPhysics.InRockCorridor(origin,forward,center,.5f,1,out float clearance,out _);
            Require(accepted,"Rock ahead inside widening corridor");
            Near(clearance,Math.Sqrt(37)-.5,.00001,"Rock clearance is to footprint boundary");
            Require(!MarsHazardPhysics.InRockCorridor(origin,forward,new Vector3(0,0,-2),1,1,out _,out _),"Rock behind is excluded");
            Require(!MarsHazardPhysics.InRockCorridor(origin,forward,new Vector3(8,0,3),.5f,1,out _,out _),"Off-corridor rock is excluded");
            Require(!MarsHazardPhysics.InRockCorridor(origin,forward,new Vector3(0,0,30),.5f,1,out _,out _),"Distant rock is excluded");
            Quaternion turn=Quaternion.Euler(0,123,0);
            Require(MarsHazardPhysics.InRockCorridor(origin,turn*forward,turn*center,.5f,1,out float turned,out _),"Corridor rotates with route heading");
            Near(turned,clearance,.00001,"Rotation preserves physical clearance");
            Require(MarsHazardPhysics.InRockCorridor(origin,forward,center/10,.05f,10,out float scaled,out _),"Map scale preserves acceptance");
            Near(scaled,clearance,.00001,"Map scale preserves metre distance");
            Require(MarsHazardPhysics.IsRockField(4,1.2f,12),"Declared field threshold");
            Require(!MarsHazardPhysics.IsRockField(3,2,5)&&!MarsHazardPhysics.IsRockField(4,1,5)&&!MarsHazardPhysics.IsRockField(4,2,13),"Count, footprint and range all required");
        }

        private static void Require(bool condition,string name)
        {checks++;if(!condition)throw new Exception("EVA physics regression: "+name);}
        private static void Near(double actual,double expected,double tolerance,string name)
        {Require(MarsEvaPhysics.Finite(actual)&&Math.Abs(actual-expected)<=tolerance,$"{name}: {actual:R} expected {expected:R}");}
        private static void Reject(Action action,string name)
        {bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Require(rejected,name);}
    }
}
