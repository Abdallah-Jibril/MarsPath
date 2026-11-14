using System;
using UnityEngine;

namespace MarsPath
{
    /// <summary>
    /// A dimensionally explicit planning estimate, not a validated Mars physiology model.
    /// Physical balances and the Weir conversion are separated from tunable locomotion
    /// and electrical-load assumptions. See docs/PHYSICS_AND_HAZARDS.md.
    /// </summary>
    public static class MarsEvaPhysics
    {
        public const double StandardGravity = 9.80665; // m/s^2, conventional reference
        public const double GasConstant = 8.31446261815324; // J/(mol K)
        public const double OxygenMolarMass = .0319988; // kg/mol
        public const double CarbonDioxideMolarMass = .04401; // kg/mol, atmosphere approximation
        public const double StpdTemperature = 273.15; // K, physiological STPD convention
        public const double StpdPressure = 101325; // Pa, NOT Martian ambient pressure
        public const double JoulesPerKilocalorie = 4184;
        public const double NominalPlanningSpeed = .70; // m/s, conservative demo setting

        [Serializable]
        public sealed class Profile
        {
            public string name = "Mars EVA engineering estimate v1";
            public float gravityMps2 = 3.71f;
            public float bodyMassKg = 82;
            public float suitMassKg = 40;
            public float nominalAwakeMetabolicWatts = 139;
            public float flatWalkingJoulesPerKgMetre = 2;
            public float gravityDependentWalkingFraction = .5f;
            public float suitMobilityJoulesPerMetre = 60;
            public float positiveWorkEfficiency = .25f;
            public float negativeWorkMetabolicRatio = .8f;
            public float respiratoryQuotient = .85f;
            public float oxygenCapacityKg = .8f;
            public float batteryNominalVolts = 16.8f;
            public float batteryAmpHours = 26.6f;
            public float oxygenLeakGramsPerHour = 0;
            public float baseElectricalWatts = 60;
            public float pumpWattsPer300WattsHeat = 15;
            public float maximumPumpWatts = 45;
            public float oxygenReserveFraction = .20f;
            public float batteryReserveFraction = .15f;
            public float datumAmbientPressurePa = 610;
            public float ambientTemperatureKelvin = 210;
            public float dragCoefficient = 1;
            public float frontalAreaSquareMetres = .7f;
            public string parameterNotice = "Concept-suit assumptions; reference battery is legacy EMU 2000-series, not a certified Mars suit.";

            public double BatteryWattHours => batteryNominalVolts * (double)batteryAmpHours;

            public void Validate()
            {
                foreach (double value in new double[] { gravityMps2, bodyMassKg, suitMassKg,
                    nominalAwakeMetabolicWatts, flatWalkingJoulesPerKgMetre,
                    gravityDependentWalkingFraction, suitMobilityJoulesPerMetre,
                    positiveWorkEfficiency, negativeWorkMetabolicRatio, respiratoryQuotient,
                    oxygenCapacityKg, batteryNominalVolts, batteryAmpHours, oxygenLeakGramsPerHour,
                    baseElectricalWatts, pumpWattsPer300WattsHeat, maximumPumpWatts,
                    oxygenReserveFraction, batteryReserveFraction, datumAmbientPressurePa,
                    ambientTemperatureKelvin, dragCoefficient, frontalAreaSquareMetres })
                    if (!Finite(value) || value < 0) throw new ArgumentException("EVA parameters require finite nonnegative values.");
                if (gravityMps2 <= 0 || bodyMassKg <= 0 || nominalAwakeMetabolicWatts <= 0 ||
                    oxygenCapacityKg <= 0 || BatteryWattHours <= 0 || baseElectricalWatts <= 0 ||
                    ambientTemperatureKelvin <= 0 || datumAmbientPressurePa <= 0 ||
                    positiveWorkEfficiency <= 0 || positiveWorkEfficiency > 1 ||
                    gravityDependentWalkingFraction > 1 || respiratoryQuotient < .7 || respiratoryQuotient > 1 ||
                    oxygenReserveFraction >= 1 || batteryReserveFraction >= 1)
                    throw new ArgumentException("EVA profile is outside its declared parameter domain.");
            }
        }

        public readonly struct Demand
        {
            public readonly double metabolicWatts, electricalWatts, oxygenGramsPerHour;
            public readonly double verticalMechanicalWatts, flatMetabolicWatts, suitMobilityWatts;
            public readonly double heatRejectionWatts, ambientPressurePa, airDensityKgPerCubicMetre;
            public readonly double oxygenStpdLitresPerMinute;
            public Demand(double metabolic, double electrical, double oxygen, double vertical,
                double flat, double mobility, double heat, double pressure, double density, double oxygenVolume)
            {
                metabolicWatts=metabolic; electricalWatts=electrical; oxygenGramsPerHour=oxygen;
                verticalMechanicalWatts=vertical; flatMetabolicWatts=flat; suitMobilityWatts=mobility;
                heatRejectionWatts=heat; ambientPressurePa=pressure; airDensityKgPerCubicMetre=density;
                oxygenStpdLitresPerMinute=oxygenVolume;
            }
        }

        private static Profile defaultProfile;
        public static Profile Default
        {
            get
            {
                if (defaultProfile != null) return defaultProfile;
                var loaded = new Profile();
                TextAsset asset = Resources.Load<TextAsset>("Science/MarsEvaModel");
                if (asset != null) JsonUtility.FromJsonOverwrite(asset.text, loaded);
                loaded.Validate();
                defaultProfile=loaded;
                return defaultProfile;
            }
        }

        // Numerically kg/m^3 equals g/L. Ideal-gas STPD density supplies a
        // consistent volume-to-mass conversion, never a breathing-air source.
        public static double OxygenGramsPerStpdLitre =>
            OxygenMolarMass * StpdPressure / (GasConstant * StpdTemperature);

        public static double CaloricEquivalentJoulesPerLitre(double rq) =>
            JoulesPerKilocalorie * (3.941 + 1.106 * rq);

        public static double OxygenStpdLitresPerMinute(double metabolicWatts, double rq) =>
            metabolicWatts * 60 / CaloricEquivalentJoulesPerLitre(rq);

        public static double AmbientPressure(double elevationMetres, Profile profile = null)
        {
            profile = profile ?? Default;
            // Isothermal CO2-dominated reference atmosphere, NOT a weather observation.
            double exponent = -profile.gravityMps2 * elevationMetres * CarbonDioxideMolarMass /
                (GasConstant * profile.ambientTemperatureKelvin);
            return profile.datumAmbientPressurePa * Math.Exp(Math.Max(-20, Math.Min(20, exponent)));
        }

        public static Demand Evaluate(double speedMetresPerSecond, double signedGradeDegrees,
            double elevationMetres = 0, Profile profile = null)
        {
            profile = profile ?? Default;
            if (!Finite(speedMetresPerSecond) || speedMetresPerSecond < 0 ||
                !Finite(signedGradeDegrees) || Math.Abs(signedGradeDegrees) >= 90 || !Finite(elevationMetres))
                throw new ArgumentException("EVA motion requires finite speed, signed grade and elevation in SI units.");
            double angle=signedGradeDegrees*Math.PI/180, mass=profile.bodyMassKg+profile.suitMassKg;
            double horizontalSpeed=speedMetresPerSecond*Math.Cos(angle);
            double verticalPower=mass*profile.gravityMps2*speedMetresPerSecond*Math.Sin(angle);
            // Reduced gravity changes weight, not mass/inertia or basal metabolism.
            // This explicit support/inertia split is a tunable engineering assumption.
            double gravityScale=1-profile.gravityDependentWalkingFraction+
                profile.gravityDependentWalkingFraction*profile.gravityMps2/StandardGravity;
            double flat=profile.flatWalkingJoulesPerKgMetre*mass*horizontalSpeed*gravityScale;
            double mobility=profile.suitMobilityJoulesPerMetre*speedMetresPerSecond;
            double gradeCost=verticalPower>=0 ? verticalPower/profile.positiveWorkEfficiency
                : -verticalPower*profile.negativeWorkMetabolicRatio;
            double pressure=AmbientPressure(elevationMetres,profile);
            double density=pressure*CarbonDioxideMolarMass/(GasConstant*profile.ambientTemperatureKelvin);
            // Still-air drag only. Wind, weather and detailed suit thermal control are not modeled.
            double dragPower=.5*density*profile.dragCoefficient*profile.frontalAreaSquareMetres*
                speedMetresPerSecond*speedMetresPerSecond*speedMetresPerSecond;
            double metabolic=profile.nominalAwakeMetabolicWatts+flat+mobility+gradeCost+
                dragPower/profile.positiveWorkEfficiency;
            double heat=Math.Max(0,metabolic-verticalPower-dragPower);
            // Pump load is a declared proxy, not metabolic work drawn from a battery.
            double pump=Math.Min(profile.maximumPumpWatts,profile.pumpWattsPer300WattsHeat*heat/300);
            double electrical=profile.baseElectricalWatts+pump;
            double oxygenVolume=OxygenStpdLitresPerMinute(metabolic,profile.respiratoryQuotient);
            double oxygen=oxygenVolume*60*OxygenGramsPerStpdLitre+profile.oxygenLeakGramsPerHour;
            return new Demand(metabolic,electrical,oxygen,verticalPower,flat,mobility,heat,pressure,density,oxygenVolume);
        }

        public static double SignedGradeDegrees(double riseMetres, double horizontalMetres) =>
            Math.Atan2(riseMetres,Math.Max(.001,horizontalMetres))*180/Math.PI;

        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
