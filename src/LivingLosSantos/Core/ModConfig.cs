using System;
using System.Globalization;
using System.IO;

namespace LivingLosSantos.Core
{
    internal sealed class ModConfig
    {
        public bool Enabled = true;
        public bool DebugMode = false;
        public bool ShowNotifications = true;
        public int UpdateIntervalMs = 1000;
        public float MinimumEventDistance = 150f;
        public float MaximumEventDistance = 300f;
        public int MaxActiveEvents = 3;
        public float RoadsideOffsetMinMeters = 2.0f;
        public float RoadsideOffsetMaxMeters = 6.0f;
        public int RuralSearchAttempts = 18;
        public int RuralSpawnPreferencePercent = 90;
        public int RoadsideHikerChancePercent = 20;
        public bool RoadsideHikerEnabled = true;
        public bool LivingCityEnabled = true;
        public int MorningStart = 6;
        public int EveningStart = 19;
        public int NightStart = 23;
        public float DayActivityMultiplier = 1.0f;
        public float NightActivityMultiplier = 0.65f;
        public bool CityEventsEnabled = true;
        public int EventCheckIntervalSeconds = 30;
        public int EventChancePercent = 75;
        public bool RoadsideBreakdownEnabled = true;

        // Scales the population authored by GTA's popcycle/popgroups data.
        // 1.0 means leave the installed game population at its full configured level.
        // The native multiplier cannot raise the game above its population-data limits.
        public bool TrafficEnabled = true;
        public float TrafficDensityMultiplier = 1.0f;
        public float ParkedVehicleDensity = 1.0f;
        public float PedDensityMultiplier = 1.0f;
        public float ScenarioPedDensityMultiplier = 1.0f;

        // Extra scripted traffic is additive: it creates a small, bounded number
        // of vehicles ahead of the player, outside the camera view.
        public bool AdditionalTrafficEnabled = true;
        public int MaxAdditionalTrafficVehicles = 4;
        public int TrafficSpawnIntervalSeconds = 14;
        public int TrafficSpawnChancePercent = 65;
        public int TrafficSpawnMinDistanceMeters = 105;
        public int TrafficSpawnMaxDistanceMeters = 165;
        public int TrafficDespawnDistanceMeters = 340;
        public int TrafficMaxSlopePercent = 10;
        public int TrafficMinimumNodeDensity = 2;
        public bool TrafficOnlyMainRoads = true;
        public string TrafficVehicleModels =
            "asea,asterope,buffalo,cavalcade,comet2,dubsta,fugitive,gauntlet,granger,ingot,intruder,jackal,oracle,premier,primo,ruiner,sabregt,schafter2,stanier,stratum,tailgater,washington,jugular,komoda,sugoi,sultan3,paragon,pariah,italigto,vagner,neo,drafter,growler,cypher,vstr,tenf,tenf2,zr350";

        public static ModConfig Load(string path)
        {
            var config = new ModConfig();
            if (!File.Exists(path))
            {
                config.Save(path);
                return config;
            }

            string section = string.Empty;
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                config.Apply(section, key, value);
            }

            config.Clamp();
            return config;
        }

        private void Apply(string section, string key, string value)
        {
            bool boolean;
            int integer;
            float number;
            if (bool.TryParse(value, out boolean))
            {
                if (section == "General" && key == "Enabled") Enabled = boolean;
                else if (section == "General" && key == "DebugMode") DebugMode = boolean;
                else if (section == "General" && key == "ShowNotifications") ShowNotifications = boolean;
                else if (section == "LivingCity" && key == "Enabled") LivingCityEnabled = boolean;
                else if (section == "CityEvents" && key == "Enabled") CityEventsEnabled = boolean;
                else if (section == "CityEvents" && key == "RoadsideBreakdownEnabled") RoadsideBreakdownEnabled = boolean;
                else if (section == "CityEvents" && key == "RoadsideHikerEnabled") RoadsideHikerEnabled = boolean;
                else if (section == "Traffic" && key == "Enabled") TrafficEnabled = boolean;
                else if (section == "Traffic" && key == "AdditionalTrafficEnabled") AdditionalTrafficEnabled = boolean;
                else if (section == "Traffic" && key == "OnlyMainRoads") TrafficOnlyMainRoads = boolean;
                return;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
            {
                if (section == "Performance" && key == "UpdateIntervalMs") UpdateIntervalMs = integer;
                else if (section == "Performance" && key == "MaxActiveEvents") MaxActiveEvents = integer;
                else if (section == "LivingCity" && key == "MorningStart") MorningStart = integer;
                else if (section == "LivingCity" && key == "EveningStart") EveningStart = integer;
                else if (section == "LivingCity" && key == "NightStart") NightStart = integer;
                else if (section == "CityEvents" && key == "EventCheckIntervalSeconds") EventCheckIntervalSeconds = integer;
                else if (section == "CityEvents" && key == "EventChancePercent") EventChancePercent = integer;
                else if (section == "CityEvents" && key == "RuralSearchAttempts") RuralSearchAttempts = integer;
                else if (section == "CityEvents" && key == "RuralSpawnPreferencePercent") RuralSpawnPreferencePercent = integer;
                else if (section == "CityEvents" && key == "RoadsideHikerChancePercent") RoadsideHikerChancePercent = integer;
                else if (section == "Traffic" && key == "MaxAdditionalVehicles") MaxAdditionalTrafficVehicles = integer;
                else if (section == "Traffic" && key == "SpawnIntervalSeconds") TrafficSpawnIntervalSeconds = integer;
                else if (section == "Traffic" && key == "SpawnChancePercent") TrafficSpawnChancePercent = integer;
                else if (section == "Traffic" && key == "SpawnMinDistanceMeters") TrafficSpawnMinDistanceMeters = integer;
                else if (section == "Traffic" && key == "SpawnMaxDistanceMeters") TrafficSpawnMaxDistanceMeters = integer;
                else if (section == "Traffic" && key == "DespawnDistanceMeters") TrafficDespawnDistanceMeters = integer;
                else if (section == "Traffic" && key == "MaxSlopePercent") TrafficMaxSlopePercent = integer;
                else if (section == "Traffic" && key == "MinimumNodeDensity") TrafficMinimumNodeDensity = integer;
                return;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                if (section == "Performance" && key == "MinimumEventDistance") MinimumEventDistance = number;
                else if (section == "Performance" && key == "MaximumEventDistance") MaximumEventDistance = number;
                else if (section == "Performance" && key == "RoadsideOffsetMinMeters") RoadsideOffsetMinMeters = number;
                else if (section == "Performance" && key == "RoadsideOffsetMaxMeters") RoadsideOffsetMaxMeters = number;
                else if (section == "LivingCity" && key == "DayActivityMultiplier") DayActivityMultiplier = number;
                else if (section == "LivingCity" && key == "NightActivityMultiplier") NightActivityMultiplier = number;
                else if (section == "Traffic" && (key == "DensityMultiplier" || key == "VanillaDensity")) TrafficDensityMultiplier = number;
                else if (section == "Traffic" && key == "ParkedDensity") ParkedVehicleDensity = number;
                else if (section == "Traffic" && key == "PedDensity") PedDensityMultiplier = number;
                else if (section == "Traffic" && key == "ScenarioPedDensity") ScenarioPedDensityMultiplier = number;
            }

            if (section == "Traffic" && key == "Models")
                TrafficVehicleModels = value;
        }

        private void Clamp()
        {
            UpdateIntervalMs = Math.Max(250, Math.Min(10000, UpdateIntervalMs));
            MinimumEventDistance = Math.Max(50f, Math.Min(1000f, MinimumEventDistance));
            MaximumEventDistance = Math.Max(MinimumEventDistance + 20f, Math.Min(1000f, MaximumEventDistance));
            RoadsideOffsetMinMeters = Math.Max(1f, Math.Min(8f, RoadsideOffsetMinMeters));
            RoadsideOffsetMaxMeters = Math.Max(RoadsideOffsetMinMeters + 0.5f, Math.Min(12f, RoadsideOffsetMaxMeters));
            MaxActiveEvents = Math.Max(0, Math.Min(10, MaxActiveEvents));
            RuralSearchAttempts = Math.Max(4, Math.Min(40, RuralSearchAttempts));
            RuralSpawnPreferencePercent = Math.Max(0, Math.Min(100, RuralSpawnPreferencePercent));
            RoadsideHikerChancePercent = Math.Max(0, Math.Min(100, RoadsideHikerChancePercent));
            EventCheckIntervalSeconds = Math.Max(10, Math.Min(600, EventCheckIntervalSeconds));
            EventChancePercent = Math.Max(0, Math.Min(100, EventChancePercent));
            MorningStart = Math.Max(0, Math.Min(23, MorningStart));
            EveningStart = Math.Max(0, Math.Min(23, EveningStart));
            NightStart = Math.Max(0, Math.Min(23, NightStart));

            TrafficDensityMultiplier = Math.Max(0f, Math.Min(1f, TrafficDensityMultiplier));
            ParkedVehicleDensity = Math.Max(0f, Math.Min(1f, ParkedVehicleDensity));
            PedDensityMultiplier = Math.Max(0f, Math.Min(1f, PedDensityMultiplier));
            ScenarioPedDensityMultiplier = Math.Max(0f, Math.Min(1f, ScenarioPedDensityMultiplier));

            MaxAdditionalTrafficVehicles = Math.Max(0, Math.Min(10, MaxAdditionalTrafficVehicles));
            TrafficSpawnIntervalSeconds = Math.Max(5, Math.Min(90, TrafficSpawnIntervalSeconds));
            TrafficSpawnChancePercent = Math.Max(0, Math.Min(100, TrafficSpawnChancePercent));
            TrafficSpawnMinDistanceMeters = Math.Max(70, Math.Min(250, TrafficSpawnMinDistanceMeters));
            TrafficSpawnMaxDistanceMeters = Math.Max(TrafficSpawnMinDistanceMeters + 20,
                Math.Min(300, TrafficSpawnMaxDistanceMeters));
            TrafficDespawnDistanceMeters = Math.Max(TrafficSpawnMaxDistanceMeters + 50,
                Math.Min(700, TrafficDespawnDistanceMeters));
            TrafficMaxSlopePercent = Math.Max(1, Math.Min(35, TrafficMaxSlopePercent));
            TrafficMinimumNodeDensity = Math.Max(0, Math.Min(15, TrafficMinimumNodeDensity));
            if (string.IsNullOrWhiteSpace(TrafficVehicleModels))
                TrafficVehicleModels = "asea,asterope,buffalo,stanier,tailgater";
        }

        public void Save(string path)
        {
            Clamp();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string text =
                "[General]\n" +
                "Enabled=" + Enabled.ToString().ToLowerInvariant() + "\n" +
                "DebugMode=" + DebugMode.ToString().ToLowerInvariant() + "\n" +
                "ShowNotifications=" + ShowNotifications.ToString().ToLowerInvariant() + "\n\n" +
                "[Performance]\n" +
                "UpdateIntervalMs=" + UpdateIntervalMs.ToString(CultureInfo.InvariantCulture) + "\n" +
                "MinimumEventDistance=" + MinimumEventDistance.ToString("0.##", CultureInfo.InvariantCulture) + "\n" +
                "MaximumEventDistance=" + MaximumEventDistance.ToString("0.##", CultureInfo.InvariantCulture) + "\n" +
                "MaxActiveEvents=" + MaxActiveEvents.ToString(CultureInfo.InvariantCulture) + "\n" +
                "RoadsideOffsetMinMeters=" + RoadsideOffsetMinMeters.ToString("0.##", CultureInfo.InvariantCulture) + "\n" +
                "RoadsideOffsetMaxMeters=" + RoadsideOffsetMaxMeters.ToString("0.##", CultureInfo.InvariantCulture) + "\n\n" +
                "[LivingCity]\n" +
                "Enabled=" + LivingCityEnabled.ToString().ToLowerInvariant() + "\n" +
                "MorningStart=" + MorningStart.ToString(CultureInfo.InvariantCulture) + "\n" +
                "EveningStart=" + EveningStart.ToString(CultureInfo.InvariantCulture) + "\n" +
                "NightStart=" + NightStart.ToString(CultureInfo.InvariantCulture) + "\n" +
                "DayActivityMultiplier=" + DayActivityMultiplier.ToString("0.##", CultureInfo.InvariantCulture) + "\n" +
                "NightActivityMultiplier=" + NightActivityMultiplier.ToString("0.##", CultureInfo.InvariantCulture) + "\n\n" +
                "[CityEvents]\n" +
                "Enabled=" + CityEventsEnabled.ToString().ToLowerInvariant() + "\n" +
                "EventCheckIntervalSeconds=" + EventCheckIntervalSeconds.ToString(CultureInfo.InvariantCulture) + "\n" +
                "EventChancePercent=" + EventChancePercent.ToString(CultureInfo.InvariantCulture) + "\n" +
                "RoadsideBreakdownEnabled=" + RoadsideBreakdownEnabled.ToString().ToLowerInvariant() + "\n" +
                "RoadsideHikerEnabled=" + RoadsideHikerEnabled.ToString().ToLowerInvariant() + "\n" +
                "RoadsideHikerChancePercent=" + RoadsideHikerChancePercent.ToString(CultureInfo.InvariantCulture) + "\n" +
                "RuralSearchAttempts=" + RuralSearchAttempts.ToString(CultureInfo.InvariantCulture) + "\n" +
                "RuralSpawnPreferencePercent=" + RuralSpawnPreferencePercent.ToString(CultureInfo.InvariantCulture) + "\n\n" +
                "[Traffic]\n" +
                "Enabled=" + TrafficEnabled.ToString().ToLowerInvariant() + "\n" +
                "DensityMultiplier=" + TrafficDensityMultiplier.ToString("0.##", CultureInfo.InvariantCulture) + "\n" +
                "ParkedDensity=" + ParkedVehicleDensity.ToString("0.##", CultureInfo.InvariantCulture) + "\n" +
                "PedDensity=" + PedDensityMultiplier.ToString("0.##", CultureInfo.InvariantCulture) + "\n" +
                "ScenarioPedDensity=" + ScenarioPedDensityMultiplier.ToString("0.##", CultureInfo.InvariantCulture) + "\n" +
                "AdditionalTrafficEnabled=" + AdditionalTrafficEnabled.ToString().ToLowerInvariant() + "\n" +
                "MaxAdditionalVehicles=" + MaxAdditionalTrafficVehicles.ToString(CultureInfo.InvariantCulture) + "\n" +
                "SpawnIntervalSeconds=" + TrafficSpawnIntervalSeconds.ToString(CultureInfo.InvariantCulture) + "\n" +
                "SpawnChancePercent=" + TrafficSpawnChancePercent.ToString(CultureInfo.InvariantCulture) + "\n" +
                "SpawnMinDistanceMeters=" + TrafficSpawnMinDistanceMeters.ToString(CultureInfo.InvariantCulture) + "\n" +
                "SpawnMaxDistanceMeters=" + TrafficSpawnMaxDistanceMeters.ToString(CultureInfo.InvariantCulture) + "\n" +
                "DespawnDistanceMeters=" + TrafficDespawnDistanceMeters.ToString(CultureInfo.InvariantCulture) + "\n" +
                "MaxSlopePercent=" + TrafficMaxSlopePercent.ToString(CultureInfo.InvariantCulture) + "\n" +
                "MinimumNodeDensity=" + TrafficMinimumNodeDensity.ToString(CultureInfo.InvariantCulture) + "\n" +
                "OnlyMainRoads=" + TrafficOnlyMainRoads.ToString().ToLowerInvariant() + "\n" +
                "Models=" + TrafficVehicleModels + "\n";

            File.WriteAllText(path, text);
        }
    }
}
