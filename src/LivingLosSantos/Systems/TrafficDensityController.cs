using System;
using GTA.Native;
using LivingLosSantos.Core;

namespace LivingLosSantos.Systems
{
    /// <summary>
    /// Applies Rockstar's per-frame ambient population multipliers.
    /// These scale the game-configured population; the separate TrafficManager
    /// provides additional vehicles above the normal population.
    /// </summary>
    internal static class TrafficDensityController
    {
        public static void ApplyFrame(ModConfig config)
        {
            if (config == null || !config.Enabled)
                return;

            if (config.TrafficEnabled)
            {
                float moving = Clamp01(config.TrafficDensityMultiplier);
                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, moving);
                Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, moving);
                Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME,
                    Clamp01(config.ParkedVehicleDensity));
            }

            Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME,
                Clamp01(config.PedDensityMultiplier));
            Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME,
                Clamp01(config.ScenarioPedDensityMultiplier),
                Clamp01(config.ScenarioPedDensityMultiplier));
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
