using UnityEngine;

namespace MarsPath
{
    /// <summary>Elapsed-time EVA model. A route's total duration is never consumed on entry.</summary>
    public sealed class MarsEvaSimulation
    {
        public const float MaxEvaDistanceKm = 10f;
        public const float MaxEvaMinutes = 480f;
        public float ElapsedSeconds => (float)elapsedSeconds;
        public double ElapsedSecondsExact => elapsedSeconds;
        public float OxygenPercent { get; private set; }
        public float PowerPercent { get; private set; }
        public float MetabolicWatts { get; private set; }
        public float OxygenGramsPerHour { get; private set; }
        public float HeartRate { get; private set; }
        public float ElectricalWatts { get; private set; }
        public MarsEvaPhysics.Demand Demand { get; private set; }
        public MarsEvaPhysics.Profile ModelProfile => profile ?? MarsEvaPhysics.Default;
        private double oxygenKg, energyKwh, elapsedSeconds;
        private float restingHeartRate;
        private MarsEvaPhysics.Profile profile;
        public bool ReserveReached => OxygenPercent <= ModelProfile.oxygenReserveFraction*100+.001f
            || PowerPercent <= ModelProfile.batteryReserveFraction*100+.001f;
        public static bool CanWalk(RouteData route) => route != null && route.available
            && route.distanceKm <= MaxEvaDistanceKm && route.etaMinutes <= MaxEvaMinutes;

        public void Reset(TelemetryData initial)
        { Reset(initial,MarsEvaPhysics.Default); }

        public void Reset(TelemetryData initial,MarsEvaPhysics.Profile model)
        {
            if(initial==null||model==null)throw new System.ArgumentNullException(initial==null?nameof(initial):nameof(model));
            model.Validate(); profile=model;
            elapsedSeconds=0;oxygenKg=model.oxygenCapacityKg*(double)Mathf.Clamp01(initial.oxygenPercent/100f);
            energyKwh=model.BatteryWattHours/1000*Mathf.Clamp01(initial.suitPowerPercent/100f);
            restingHeartRate=initial.heartRateBpm;
            RefreshPhysical(0,0,0);
            OxygenPercent=Mathf.Clamp(initial.oxygenPercent,0,100);PowerPercent=Mathf.Clamp(initial.suitPowerPercent,0,100);
        }

        // Compatibility with older capture/check callers. Route ranking is not a
        // physiological input and must not feed back into modeled consumption.
        public void Advance(float seconds,float speedMetersPerSecond,float slopeDegrees,float energyScore)
        { AdvancePhysical(seconds,speedMetersPerSecond,slopeDegrees,0); }

        public void AdvancePhysical(double seconds,double speedMetersPerSecond,double signedGradeDegrees,double elevationMetres)
        {
            RefreshPhysical(speedMetersPerSecond,signedGradeDegrees,elevationMetres);
            if(!MarsEvaPhysics.Finite(seconds))throw new System.ArgumentException("Mission time must be finite.");
            if(seconds<=0)return;
            // Stop at the reserve boundary rather than continuing with an empty suit.
            double capacityKwh=ModelProfile.BatteryWattHours/1000;
            double oxygenRate=Demand.oxygenGramsPerHour/1000;
            double oxygenHours=(oxygenKg-ModelProfile.oxygenCapacityKg*(double)ModelProfile.oxygenReserveFraction)/oxygenRate;
            double electricalKw=Demand.electricalWatts/1000;
            double powerHours=(energyKwh-capacityKwh*ModelProfile.batteryReserveFraction)/electricalKw;
            double hours=System.Math.Min(seconds/3600.0,System.Math.Max(0,System.Math.Min(oxygenHours,powerHours)));
            elapsedSeconds+=hours*3600;
            oxygenKg-=oxygenRate*hours; energyKwh-=electricalKw*hours;
            OxygenPercent=Mathf.Clamp01((float)(oxygenKg/ModelProfile.oxygenCapacityKg))*100;PowerPercent=Mathf.Clamp01((float)(energyKwh/capacityKwh))*100;
        }
        private void RefreshPhysical(double speed,double signedGrade,double elevation)
        {
            Demand=MarsEvaPhysics.Evaluate(speed,signedGrade,elevation,ModelProfile);
            MetabolicWatts=(float)Demand.metabolicWatts;ElectricalWatts=(float)Demand.electricalWatts;
            OxygenGramsPerHour=(float)Demand.oxygenGramsPerHour;
            // No measured heart-rate sensor or validated HR prediction is present.
            HeartRate=restingHeartRate;
        }
    }
}
