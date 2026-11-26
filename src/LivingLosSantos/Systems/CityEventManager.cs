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

        private readonly ModConfig _config;
        private readonly Random _random = new Random();
        private readonly List<ICityEvent> _activeEvents = new List<ICityEvent>();
        private DateTime _nextCheck = DateTime.MinValue;
        private VehicleHash? _lastVehicleModel;

        private sealed class RoadsideCandidate
        {
            public Vector3 Position;
            public float Heading;
            public string Zone;
            public bool IsRural;
            public float Distance;
            public float ExtraOffset;
        }

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
            if (_activeEvents.Count >= _config.MaxActiveEvents)
                return;

            bool breakdownEnabled = _config.RoadsideBreakdownEnabled;
            bool hikerEnabled = _config.RoadsideHikerEnabled;
            if (!breakdownEnabled && !hikerEnabled)
                return;

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists())
                return;

            RoadsideCandidate roadside;
            if (!TryFindRoadsideCandidate(player.Position, out roadside))
            {
                ModLog.Info("No safe roadside position found; skipped the event instead of spawning on the road.");
                return;
            }

            bool chooseHiker = roadside.IsRural &&
                hikerEnabled &&
                _random.Next(100) < _config.RoadsideHikerChancePercent;

            ICityEvent cityEvent = null;
            if (chooseHiker)
            {
                var hiker = new RoadsideHikerEvent(roadside.Position, roadside.Heading, roadside.Zone);
                if (hiker.Start())
                    cityEvent = hiker;
                else
                    ModLog.Warn("Roadside hiker scene failed to start; trying a breakdown vehicle instead.");
            }

            if (cityEvent == null && breakdownEnabled)
            {
                VehicleHash vehicleModel = ChooseBreakdownVehicle(roadside.IsRural);
                var breakdown = new RoadsideBreakdownEvent(
                    roadside.Position, roadside.Heading, vehicleModel, roadside.Zone);

                if (breakdown.Start())
                {
                    cityEvent = breakdown;
                    _lastVehicleModel = vehicleModel;
                    ModLog.Info(string.Format(
                        "Started roadside breakdown: {0} in zone {1}, rural={2}, {3:0}m from player, {4:0.0}m beyond road edge.",
                        vehicleModel, roadside.Zone, roadside.IsRural, roadside.Distance, roadside.ExtraOffset));
                }
            }

            if (cityEvent != null)
            {
                _activeEvents.Add(cityEvent);
                if (chooseHiker && cityEvent is RoadsideHikerEvent)
                    ModLog.Info("Started rural roadside hiker event in " + roadside.Zone + ".");
            }
        }

        private bool TryFindRoadsideCandidate(Vector3 playerPosition, out RoadsideCandidate selected)
        {
            var allCandidates = new List<RoadsideCandidate>();
            var ruralCandidates = new List<RoadsideCandidate>();

            for (int i = 0; i < _config.RuralSearchAttempts; i++)
            {
                double angle = _random.NextDouble() * Math.PI * 2.0;
                float radius = RandomBetween(_config.MinimumEventDistance, _config.MaximumEventDistance);
                Vector3 sample = new Vector3(
                    playerPosition.X + (float)Math.Cos(angle) * radius,
                    playerPosition.Y + (float)Math.Sin(angle) * radius,
                    playerPosition.Z);

                Vector3 roadNode;
                float roadHeading;
                bool found = TryGetVehicleNode(sample, 0, out roadNode, out roadHeading);
                if (!found)
                    found = TryGetVehicleNode(sample, 1, out roadNode, out roadHeading);
                if (!found)
                    continue;

                string zone = Function.Call<string>(
                    Hash.GET_NAME_OF_ZONE, roadNode.X, roadNode.Y, roadNode.Z);
                if (string.IsNullOrEmpty(zone))
                    zone = "UNKNOWN";

                Vector3 shoulder;
                float extraOffset;
                if (!TryGetSafeShoulderPosition(roadNode, roadHeading, out shoulder, out extraOffset))
                    continue;

                float distance = shoulder.DistanceTo(playerPosition);
                if (distance < _config.MinimumEventDistance - 25f ||
                    distance > _config.MaximumEventDistance + 35f)
                    continue;

                var candidate = new RoadsideCandidate
                {
                    Position = shoulder,
                    Heading = roadHeading,
                    Zone = zone,
                    IsRural = IsRuralZone(zone),
                    Distance = distance,
                    ExtraOffset = extraOffset
                };

                allCandidates.Add(candidate);
                if (candidate.IsRural)
                    ruralCandidates.Add(candidate);
            }

            if (ruralCandidates.Count > 0 &&
                (allCandidates.Count == ruralCandidates.Count ||
                 _random.Next(100) < _config.RuralSpawnPreferencePercent))
            {
                selected = ruralCandidates[_random.Next(ruralCandidates.Count)];
                return true;
            }

            if (allCandidates.Count > 0)
            {
                selected = allCandidates[_random.Next(allCandidates.Count)];
                return true;
            }

            selected = null;
            return false;
        }

        private bool TryGetSafeShoulderPosition(
            Vector3 roadNode,
            float roadHeading,
            out Vector3 shoulderPosition,
            out float usedOffset)
        {
            float[] headings =
            {
                roadHeading,
                (roadHeading + 180f) % 360f
            };

            // GET_ROAD_BOUNDARY_USING_HEADING returns the edge of the road.
            // We move out from that edge, then reject anything the game still
            // considers part of a road. If neither side has safe space, do not spawn.
            foreach (float heading in headings)
            {
                using (OutputArgument boundaryArgument = new OutputArgument())
                {
                    bool boundaryFound = Function.Call<bool>(
                        Hash.GET_ROAD_BOUNDARY_USING_HEADING,
                        roadNode.X, roadNode.Y, roadNode.Z, heading, boundaryArgument);

                    if (!boundaryFound)
                        continue;

                    Vector3 boundary = boundaryArgument.GetResult<Vector3>();
                    float dx = boundary.X - roadNode.X;
                    float dy = boundary.Y - roadNode.Y;
                    float length = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (length < 0.5f || length > 25f)
                        continue;

                    dx /= length;
                    dy /= length;

                    float offset = _config.RoadsideOffsetMinMeters;
                    while (offset <= _config.RoadsideOffsetMaxMeters + 0.01f)
                    {
                        Vector3 candidate = new Vector3(
                            boundary.X + dx * offset,
                            boundary.Y + dy * offset,
                            boundary.Z);

                        if (!Function.Call<bool>(
                            Hash.IS_POINT_ON_ROAD, candidate.X, candidate.Y, candidate.Z, 0))
                        {
                            using (OutputArgument groundArgument = new OutputArgument())
                            {
                                bool groundFound = Function.Call<bool>(
                                    Hash.GET_GROUND_Z_FOR_3D_COORD,
                                    candidate.X, candidate.Y, candidate.Z + 25f,
                                    groundArgument, false);

                                if (groundFound)
                                {
                                    candidate.Z = groundArgument.GetResult<float>();

                                    if (!Function.Call<bool>(
                                        Hash.IS_POINT_ON_ROAD, candidate.X, candidate.Y, candidate.Z, 0))
                                    {
                                        shoulderPosition = candidate;
                                        usedOffset = offset;
                                        return true;
                                    }
                                }
                            }
                        }

                        offset += 1.0f;
                    }
                }
            }

            shoulderPosition = Vector3.Zero;
            usedOffset = 0f;
            return false;
        }

        private static bool TryGetVehicleNode(
            Vector3 candidate, int nodeType, out Vector3 nodePosition, out float heading)
        {
            using (OutputArgument positionArgument = new OutputArgument())
            using (OutputArgument headingArgument = new OutputArgument())
            {
                bool found = Function.Call<bool>(
                    Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                    candidate.X, candidate.Y, candidate.Z,
                    positionArgument, headingArgument, nodeType, 3f, 0);

                if (found)
                {
                    nodePosition = positionArgument.GetResult<Vector3>();
                    heading = headingArgument.GetResult<float>();
                    return true;
                }
            }

            nodePosition = Vector3.Zero;
            heading = 0f;
            return false;
        }

        private VehicleHash ChooseBreakdownVehicle(bool rural)
        {
            VehicleHash[] ruralPool =
            {
                VehicleHash.Sadler, VehicleHash.Sadler, VehicleHash.Sadler,
                VehicleHash.Regina, VehicleHash.Regina,
                VehicleHash.Emperor, VehicleHash.Emperor,
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
            VehicleHash selectedModel = pool[index];

            if (_lastVehicleModel.HasValue && selectedModel == _lastVehicleModel.Value)
                selectedModel = pool[(index + 1) % pool.Length];

            return selectedModel;
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
            foreach (ICityEvent cityEvent in _activeEvents)
                cityEvent.Stop();

            _activeEvents.Clear();
        }
    }
}
