using UnityEngine;

namespace MarsPath
{
    /// <summary>Elapsed-time EVA model. A route's total duration is never consumed on entry.</summary>
    public sealed class MarsEvaSimulation
    {
        public const float MaxEvaDistanceKm = 10f;
        public const float MaxEvaMinutes = 480f;
        public float ElapsedSeconds { get; private set; }
        public float OxygenPercent { get; private set; }
        public float PowerPercent { get; private set; }
        public float MetabolicWatts { get; private set; }
        public float OxygenGramsPerHour { get; private set; }
        public float HeartRate { get; private set; }
        private float oxygenKg, energyKwh, restingHeartRate;
        public bool ReserveReached => OxygenPercent <= 20.001f || PowerPercent <= 15.001f;
        public static bool CanWalk(RouteData route) => route != null && route.available
            && route.distanceKm <= MaxEvaDistanceKm && route.etaMinutes <= MaxEvaMinutes;

        public void Reset(TelemetryData initial)
        {
            ElapsedSeconds=0; oxygenKg=0.8f*initial.oxygenPercent/100f;
            energyKwh=4f*initial.suitPowerPercent/100f; restingHeartRate=initial.heartRateBpm;
            Refresh(0,0,100); OxygenPercent=initial.oxygenPercent; PowerPercent=initial.suitPowerPercent;
        }
        public void Advance(float seconds,float speedMetersPerSecond,float slopeDegrees,float energyScore)
        {
            Refresh(speedMetersPerSecond,slopeDegrees,energyScore);
            if(seconds<=0)return;
            // Stop at the reserve boundary rather than continuing with an empty suit.
            float oxygenHours=(oxygenKg-0.8f*0.20f)/Mathf.Max(0.001f,OxygenGramsPerHour/1000f);
            float electricalKw=0.20f+MetabolicWatts*0.00045f;
            float powerHours=(energyKwh-4f*0.15f)/electricalKw;
            float hours=Mathf.Min(seconds/3600f,Mathf.Max(0,Mathf.Min(oxygenHours,powerHours)));
            ElapsedSeconds+=hours*3600f;
            oxygenKg-=OxygenGramsPerHour/1000f*hours; energyKwh-=electricalKw*hours;
            OxygenPercent=Mathf.Clamp01(oxygenKg/0.8f)*100f; PowerPercent=Mathf.Clamp01(energyKwh/4f)*100f;
        }
        private void Refresh(float speed,float slope,float energy)
        {
            float exertion=1+Mathf.Clamp01(slope/20f)*0.38f+Mathf.Clamp((speed-0.75f)*0.22f,0,0.18f)+(100-energy)/100f*0.16f;
            MetabolicWatts=267f*exertion; OxygenGramsPerHour=76f*exertion+5f;
            HeartRate=Mathf.Clamp(restingHeartRate+(exertion-1)*72f,70,155);
        }
    }
}
