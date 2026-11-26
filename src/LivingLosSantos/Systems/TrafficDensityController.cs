using System;
using GTA.Native;
using LivingLosSantos.Core;

namespace LivingLosSantos.Systems
{
    /// <summary>
    /// Scales Rockstar's ambient traffic population for the current frame.
    /// The actual population and regional/time-of-day distribution come from
    /// GTA's popcycle.dat, popgroups.ymt and gameconfig.xml.
    /// </summary>
    internal static class TrafficDensityController
    {
        public static void ApplyFrame(ModConfig config)
        {
            if (config == null || !config.Enabled || !config.TrafficEnabled)
                return;

            float movingDensity = Clamp01(config.TrafficDensityMultiplier);
            float parkedDensity = Clamp01(config.ParkedVehicleDensity);

            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, movingDensity);
            Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, movingDensity);
            Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, parkedDensity);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
