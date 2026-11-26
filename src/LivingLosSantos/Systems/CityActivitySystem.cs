using System;
using LivingLosSantos.Core;

namespace LivingLosSantos.Systems
{
    internal enum CityPeriod
    {
        Night,
        Morning,
        Day,
        Evening
    }

    internal sealed class CityActivitySystem
    {
        private readonly ModConfig _config;

        public CityPeriod CurrentPeriod { get; private set; }
        public float TrafficMultiplier { get; private set; }
        public float ActivityMultiplier { get; private set; }

        public CityActivitySystem(ModConfig config)
        {
            _config = config;
            Update();
        }

        public void Update()
        {
            int hour = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_CLOCK_HOURS);
            if (hour >= _config.NightStart || hour < _config.MorningStart)
                CurrentPeriod = CityPeriod.Night;
            else if (hour < 10)
                CurrentPeriod = CityPeriod.Morning;
            else if (hour < _config.EveningStart)
                CurrentPeriod = CityPeriod.Day;
            else
                CurrentPeriod = CityPeriod.Evening;

            bool night = CurrentPeriod == CityPeriod.Night;
            TrafficMultiplier = night ? _config.NightTrafficMultiplier : _config.DayTrafficMultiplier;
            ActivityMultiplier = night ? _config.NightActivityMultiplier : _config.DayActivityMultiplier;
        }
    }
}
