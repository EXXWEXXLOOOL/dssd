using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using LivingLosSantos.Core;
using LivingLosSantos.Systems.Events;

namespace LivingLosSantos.Systems
{
    internal sealed class CityEventManager
    {
        private static readonly HashSet<string> RuralZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BRADP", "BRADT", "BAYTRE", "CANNY", "CCREAK", "CHU", "CMSW",
            "DESRT", "ELGORL", "GALFISH", "GRAPES", "GREATC", "HARMO",
            "HUMLAB", "LACT", "LAGO", "MTCHIL", "MTGORDO", "MTJOSE",
            "NCHU", "PALCOV", "PALETO", "PALFOR", "PALHIGH", "PROCOB",
            "RTRAK", "SANCHIA", "SANDY", "SLAB", "TATAMO", "TONGVAH",
            "TONGVAV", "ZANCUDO", "ZQ_UAR"
        };

        private static readonly VehicleHash[] BreakdownVehicles =
        {
            VehicleHash.Asea,
            VehicleHash.Blista,
            VehicleHash.Emperor,
            VehicleHash.Premier,
            VehicleHash.Regina,
            VehicleHash.Sadler,
            VehicleHash.Stanier
        };

        private readonly ModConfig _config;
        private readonly Random _random = new Random();
        private readonly List<RoadsideBreakdownEvent> _activeEvents = new List<RoadsideBreakdownEvent>();
        private DateTime _nextCheck = DateTime.MinValue;
        private VehicleHash? _lastVehicleModel;

        public CityEventManager(ModConfig config)
        {
            _config = config;
        }

        public int ActiveEventCount { get { return _activeEvents.Count; } }

        public void Update(DateTime now)
        {
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (!_activeEvents[i].Update())
                        _activeEvents.RemoveAt(i);
                }
                catch (Exception ex)
                {
                    ModLog.Error("An active city event failed; it will be stopped.", ex);
                    _activeEvents[i].Stop();
                    _activeEvents.RemoveAt(i);
                }
            }

            if (!_config.CityEventsEnabled || now < _nextCheck)
                return;

            _nextCheck = now.AddSeconds(_config.EventCheckIntervalSeconds);
            if (_activeEvents.Count >= _config.MaxActiveEvents || _random.Next(100) >= _config.EventChancePercent)
                return;

            TryStartEvent();
        }

        private void TryStartEvent()
        {
            if (!_config.RoadsideBreakdownEnabled || _activeEvents.Count >= _config.MaxActiveEvents)
                return;

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists())
                return;

            Vector3 roadPosition;
            float roadHeading;
            string zoneName;
            bool rural;
            if (!TryFindRoadsideNode(player.Position, out roadPosition, out roadHeading, out zoneName, out rural))
            {
                ModLog.Info("No suitable roadside node found for a breakdown event near the player.");
                return;
            }

            float offset = RandomBetween(_config.RoadsideOffsetMinMeters, _config.RoadsideOffsetMaxMeters);
            double radians = roadHeading * Math.PI / 180.0;
            float sideX = (float)Math.Cos(radians);
            float sideY = (float)-Math.Sin(radians);
            float sideSign = _random.Next(2) == 0 ? -1f : 1f;

            Vector3 shoulderPosition = new Vector3(
                roadPosition.X + sideX * offset * sideSign,
                roadPosition.Y + sideY * offset * sideSign,
                roadPosition.Z);

            float spawnDistance = shoulderPosition.DistanceTo(player.Position);
            if (spawnDistance < _config.MinimumEventDistance - 20f ||
                spawnDistance > _config.MaximumEventDistance + 30f)
            {
                ModLog.Info("Rejected roadside node outside configured spawn distance.");
                return;
            }

            VehicleHash vehicleModel = ChooseBreakdownVehicle(rural);
            var cityEvent = new RoadsideBreakdownEvent(shoulderPosition, roadHeading, vehicleModel, zoneName);
            if (cityEvent.Start())
            {
                _activeEvents.Add(cityEvent);
                _lastVehicleModel = vehicleModel;
                ModLog.Info(string.Format(
                    "Started roadside breakdown: {0} in zone {1}, rural={2}, offset={3:0.0}m.",
                    vehicleModel, zoneName, rural, offset));
            }
        }

        private bool TryFindRoadsideNode(
            Vector3 playerPosition,
            out Vector3 selectedPosition,
            out float selectedHeading,
            out string selectedZone,
            out bool selectedIsRural)
        {
            bool hasAny = false;
            bool hasRural = false;
            Vector3 anyPosition = Vector3.Zero;
            Vector3 ruralPosition = Vector3.Zero;
            float anyHeading = 0f;
            float ruralHeading = 0f;
            string anyZone = string.Empty;
            string ruralZone = string.Empty;

            for (int i = 0; i < _config.RuralSearchAttempts; i++)
            {
                double angle = _random.NextDouble() * Math.PI * 2.0;
                float radius = RandomBetween(_config.MinimumEventDistance, _config.MaximumEventDistance);
                Vector3 candidate = new Vector3(
                    playerPosition.X + (float)Math.Cos(angle) * radius,
                    playerPosition.Y + (float)Math.Sin(angle) * radius,
                    playerPosition.Z);

                Vector3 nodePosition;
                float nodeHeading;
                bool found = TryGetVehicleNode(candidate, 0, out nodePosition, out nodeHeading);
                if (!found)
                    found = TryGetVehicleNode(candidate, 1, out nodePosition, out nodeHeading);
                if (!found)
                    continue;

                float distance = nodePosition.DistanceTo(playerPosition);
                if (distance < _config.MinimumEventDistance - 20f ||
                    distance > _config.MaximumEventDistance + 30f)
                    continue;

                string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, nodePosition.X, nodePosition.Y, nodePosition.Z);
                if (string.IsNullOrEmpty(zone))
                    zone = "UNKNOWN";

                if (!hasAny || _random.Next(3) == 0)
                {
                    hasAny = true;
                    anyPosition = nodePosition;
                    anyHeading = nodeHeading;
                    anyZone = zone;
                }

                if (IsRuralZone(zone) && (!hasRural || _random.Next(3) == 0))
                {
                    hasRural = true;
                    ruralPosition = nodePosition;
                    ruralHeading = nodeHeading;
                    ruralZone = zone;
                }
            }

            if (hasRural && (!hasAny || _random.Next(100) < _config.RuralSpawnPreferencePercent))
            {
                selectedPosition = ruralPosition;
                selectedHeading = ruralHeading;
                selectedZone = ruralZone;
                selectedIsRural = true;
                return true;
            }

            if (hasAny)
            {
                selectedPosition = anyPosition;
                selectedHeading = anyHeading;
                selectedZone = anyZone;
                selectedIsRural = IsRuralZone(anyZone);
                return true;
            }

            selectedPosition = Vector3.Zero;
            selectedHeading = 0f;
            selectedZone = string.Empty;
            selectedIsRural = false;
            return false;
        }

        private static bool TryGetVehicleNode(Vector3 candidate, int nodeType, out Vector3 nodePosition, out float heading)
        {
            OutputArgument outPosition = new OutputArgument();
            OutputArgument outHeading = new OutputArgument();

            bool found = Function.Call<bool>(
                Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                candidate.X, candidate.Y, candidate.Z,
                outPosition, outHeading, nodeType, 3f, 0);

            if (found)
            {
                nodePosition = outPosition.GetResult<Vector3>();
                heading = outHeading.GetResult<float>();
                return true;
            }

            nodePosition = Vector3.Zero;
            heading = 0f;
            return false;
        }

        private VehicleHash ChooseBreakdownVehicle(bool rural)
        {
            // Rural roads favour practical older cars and pickups, while still
            // allowing the full seven-model pool to appear in every region.
            VehicleHash[] ruralPool =
            {
                VehicleHash.Sadler, VehicleHash.Regina, VehicleHash.Emperor,
                VehicleHash.Asea, VehicleHash.Premier, VehicleHash.Stanier,
                VehicleHash.Blista
            };

            VehicleHash[] urbanPool =
            {
                VehicleHash.Blista, VehicleHash.Asea, VehicleHash.Premier,
                VehicleHash.Emperor, VehicleHash.Stanier, VehicleHash.Regina,
                VehicleHash.Sadler
            };

            VehicleHash[] pool = rural ? ruralPool : urbanPool;
            int index = _random.Next(pool.Length);
            VehicleHash selected = pool[index];

            if (_lastVehicleModel.HasValue && selected == _lastVehicleModel.Value)
                selected = pool[(index + 1) % pool.Length];

            return selected;
        }

        private static bool IsRuralZone(string zone)
        {
            return !string.IsNullOrEmpty(zone) && RuralZones.Contains(zone);
        }

        private float RandomBetween(float minimum, float maximum)
        {
            return minimum + (float)_random.NextDouble() * (maximum - minimum);
        }

        public void StopAll()
        {
            foreach (RoadsideBreakdownEvent cityEvent in _activeEvents)
                cityEvent.Stop();
            _activeEvents.Clear();
        }
    }
}
