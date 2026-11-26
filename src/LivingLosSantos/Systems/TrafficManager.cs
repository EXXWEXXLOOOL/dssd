using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using LivingLosSantos.Core;

namespace LivingLosSantos.Systems
{
    /// <summary>
    /// Adds a bounded layer of scripted traffic on top of Rockstar's ambient traffic.
    /// Cars are selected from the configured model pool, preloaded asynchronously,
    /// spawned on valid road nodes outside the camera view, and cleaned up safely.
    /// </summary>
    internal sealed class TrafficManager
    {
        private const int NormalDrivingStyle = 786603;
        private const int RoadFlagOffRoad = 1;
        private const int RoadFlagSwitchedOff = 8;
        private const int RoadFlagTunnelOrInterior = 16;
        private const int RoadFlagDeadEnd = 32;
        private const int RoadFlagWater = 1024;

        private static readonly string[] RuralZones =
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
        private readonly List<ManagedTrafficVehicle> _active = new List<ManagedTrafficVehicle>();
        private readonly HashSet<string> _unavailableModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> _recentModels = new Queue<string>();

        private string[] _modelNames = new string[0];
        private Model _preloadedModel;
        private string _preloadedModelName;
        private DateTime _preloadStartedAt;
        private DateTime _lastModelRequestAt;
        private DateTime _nextModelLoadAt = DateTime.MinValue;
        private DateTime _nextSpawnAt = DateTime.MinValue;

        private sealed class SpawnCandidate
        {
            public Vector3 Position;
            public float Heading;
            public float RoadDensity;
        }

        private sealed class ManagedTrafficVehicle
        {
            public Vehicle Vehicle;
            public Ped Driver;
            public DateTime SpawnedAt;
            public DateTime LastSampleAt;
            public DateTime LastMovedAt;
            public DateTime LastRecoveryAt;
            public Vector3 LastPosition;
        }

        public TrafficManager(ModConfig config)
        {
            _config = config;
            RefreshModelNames();
        }

        public int ActiveVehicleCount
        {
            get { return _active.Count; }
        }

        public void Update(DateTime now)
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists())
                return;

            Cleanup(player, now);

            if (!_config.Enabled || !_config.AdditionalTrafficEnabled ||
                _config.MaxAdditionalTrafficVehicles <= 0 || IsUnsafeGameState())
            {
                ReleasePreloadedModel();
                return;
            }

            RetireExcessVehicles(GetEffectiveMaxVehicles(player, now));

            if (_active.Count >= GetEffectiveMaxVehicles(player, now))
                return;

            EnsurePreloadedModel(now);

            if (now < _nextSpawnAt || _preloadedModel == null || !_preloadedModel.IsLoaded)
                return;

            _nextSpawnAt = now.AddSeconds(GetEffectiveSpawnInterval(player, now));
            if (_random.Next(100) >= _config.TrafficSpawnChancePercent)
                return;

            SpawnCandidate candidate;
            if (!TryFindSpawnCandidate(player, out candidate))
            {
                ModLog.Info("Extra traffic spawn skipped: no safe, off-screen road node found ahead of the player.");
                return;
            }

            if (SpawnVehicle(candidate, now))
                ReleasePreloadedModel();
        }

        private bool IsUnsafeGameState()
        {
            return Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) ||
                   Function.Call<bool>(Hash.IS_INTERIOR_SCENE);
        }

        private void RefreshModelNames()
        {
            _modelNames = (_config.TrafficVehicleModels ?? string.Empty)
                .Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (_modelNames.Length == 0)
                _modelNames = new[] { "asea", "asterope", "buffalo", "stanier", "tailgater" };
        }

        private void EnsurePreloadedModel(DateTime now)
        {
            if (_preloadedModel != null)
            {
                if (_preloadedModel.IsLoaded)
                    return;

                if (now - _preloadStartedAt > TimeSpan.FromSeconds(12))
                {
                    ModLog.Warn("Timed out preloading traffic model " + _preloadedModelName + "; skipping it.");
                    _unavailableModels.Add(_preloadedModelName);
                    ReleasePreloadedModel();
                    _nextModelLoadAt = now.AddSeconds(1);
                    return;
                }

                if (now - _lastModelRequestAt >= TimeSpan.FromSeconds(1))
                {
                    _preloadedModel.Request();
                    _lastModelRequestAt = now;
                }
                return;
            }

            if (now < _nextModelLoadAt)
                return;

            for (int attempt = 0; attempt < _modelNames.Length; attempt++)
            {
                string[] available = _modelNames
                    .Where(x => !_unavailableModels.Contains(x) &&
                        !_recentModels.Contains(x, StringComparer.OrdinalIgnoreCase))
                    .ToArray();

                if (available.Length == 0)
                    available = _modelNames.Where(x => !_unavailableModels.Contains(x)).ToArray();

                if (available.Length == 0)
                    break;

                string name = available[_random.Next(available.Length)];

                Model model = new Model(name);
                if (!model.IsInCdImage || !model.IsValid || !model.IsVehicle)
                {
                    _unavailableModels.Add(name);
                    ModLog.Warn("Traffic vehicle model is not available in this installation: " + name);
                    continue;
                }

                model.Request();
                _preloadedModel = model;
                _preloadedModelName = name;
                _preloadStartedAt = now;
                _lastModelRequestAt = now;
                return;
            }

            if (_unavailableModels.Count >= _modelNames.Length)
            {
                // Retry periodically: an add-on vehicle may be installed while the game is closed.
                _unavailableModels.Clear();
                _nextModelLoadAt = now.AddSeconds(30);
                ModLog.Warn("No traffic vehicle models are currently loadable; will retry in 30 seconds.");
            }
        }

        private void ReleasePreloadedModel()
        {
            try
            {
                if (_preloadedModel != null)
                    _preloadedModel.MarkAsNoLongerNeeded();
            }
            catch
            {
                // Releasing a failed/streamed-out model must not interrupt the game loop.
            }
            finally
            {
                _preloadedModel = null;
                _preloadedModelName = null;
            }
        }

        private bool TryFindSpawnCandidate(Ped player, out SpawnCandidate selected)
        {
            int referenceHandle = player.Handle;
            int playerVehicleHandle = Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, player.Handle, false);
            if (playerVehicleHandle != 0)
                referenceHandle = playerVehicleHandle;

            Vector3 playerPosition = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, referenceHandle, true);
            Vector3 forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, referenceHandle);
            float forwardLength = (float)Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y);
            if (forwardLength < 0.01f)
            {
                selected = null;
                return false;
            }

            forward.X /= forwardLength;
            forward.Y /= forwardLength;

            float baseAngle = (float)Math.Atan2(forward.Y, forward.X);
            int nodeType = _config.TrafficOnlyMainRoads ? 0 : 1;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                // Look ahead with a moderate spread, never in a full circle around the player.
                float angle = baseAngle + RandomBetween(-0.80f, 0.80f);
                float distance = RandomBetween(_config.TrafficSpawnMinDistanceMeters,
                    _config.TrafficSpawnMaxDistanceMeters);

                Vector3 sample = new Vector3(
                    playerPosition.X + (float)Math.Cos(angle) * distance,
                    playerPosition.Y + (float)Math.Sin(angle) * distance,
                    playerPosition.Z);

                Vector3 roadNode;
                float roadHeading;
                if (!TryGetVehicleNode(sample, nodeType, out roadNode, out roadHeading))
                    continue;

                Vector3 delta = roadNode - playerPosition;
                float horizontalDistance = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
                if (horizontalDistance < _config.TrafficSpawnMinDistanceMeters - 15f ||
                    horizontalDistance > _config.TrafficSpawnMaxDistanceMeters + 25f)
                    continue;

                float forwardDot = (delta.X * forward.X + delta.Y * forward.Y) /
                    Math.Max(0.01f, horizontalDistance);
                if (forwardDot < 0.48f)
                    continue;

                // Avoid road nodes that are already occupied or in the camera frustum.
                if (Function.Call<bool>(Hash.IS_SPHERE_VISIBLE,
                    roadNode.X, roadNode.Y, roadNode.Z + 1.5f, 7.5f))
                    continue;

                if (Function.Call<bool>(Hash.IS_ANY_VEHICLE_NEAR_POINT,
                    roadNode.X, roadNode.Y, roadNode.Z, 18f))
                    continue;

                if (!Function.Call<bool>(Hash.IS_POINT_ON_ROAD,
                    roadNode.X, roadNode.Y, roadNode.Z, 0))
                    continue;

                float roadDensity;
                if (!TryGetRoadProperties(roadNode, out roadDensity))
                    continue;

                if (roadDensity < _config.TrafficMinimumNodeDensity)
                    continue;

                if (!IsSuitableRoadNode(roadNode, roadHeading))
                    continue;

                selected = new SpawnCandidate
                {
                    Position = roadNode,
                    Heading = roadHeading,
                    RoadDensity = roadDensity
                };
                return true;
            }

            selected = null;
            return false;
        }

        private bool IsSuitableRoadNode(Vector3 node, float heading)
        {
            float groundZ;
            if (!TryGetGroundZ(node.X, node.Y, node.Z + 20f, out groundZ))
                return false;

            // Sample the surface a few metres along the road and reject steep ramps.
            float radians = heading * (float)Math.PI / 180f;
            float dx = -(float)Math.Sin(radians) * 6f;
            float dy = (float)Math.Cos(radians) * 6f;
            float aheadZ;
            float behindZ;

            if (!TryGetGroundZ(node.X + dx, node.Y + dy, node.Z + 20f, out aheadZ) ||
                !TryGetGroundZ(node.X - dx, node.Y - dy, node.Z + 20f, out behindZ))
                return false;

            float slopePercent = Math.Abs(aheadZ - behindZ) / 12f * 100f;
            if (slopePercent > _config.TrafficMaxSlopePercent)
                return false;

            return true;
        }

        private static bool TryGetGroundZ(float x, float y, float z, out float groundZ)
        {
            using (OutputArgument groundArgument = new OutputArgument())
            {
                bool found = Function.Call<bool>(
                    Hash.GET_GROUND_Z_FOR_3D_COORD, x, y, z, groundArgument, false);

                groundZ = found ? groundArgument.GetResult<float>() : 0f;
                return found;
            }
        }

        private static bool TryGetRoadProperties(Vector3 position, out float density)
        {
            using (OutputArgument densityArgument = new OutputArgument())
            using (OutputArgument flagsArgument = new OutputArgument())
            {
                bool found = Function.Call<bool>(
                    Hash.GET_VEHICLE_NODE_PROPERTIES,
                    position.X, position.Y, position.Z,
                    densityArgument, flagsArgument);

                if (!found)
                {
                    density = 0f;
                    return false;
                }

                density = densityArgument.GetResult<int>();
                int flags = flagsArgument.GetResult<int>();
                int rejectFlags = RoadFlagOffRoad | RoadFlagSwitchedOff |
                    RoadFlagTunnelOrInterior | RoadFlagDeadEnd | RoadFlagWater;

                return (flags & rejectFlags) == 0;
            }
        }

        private static bool TryGetVehicleNode(
            Vector3 sample, int nodeType, out Vector3 position, out float heading)
        {
            using (OutputArgument positionArgument = new OutputArgument())
            using (OutputArgument headingArgument = new OutputArgument())
            {
                bool found = Function.Call<bool>(
                    Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING,
                    sample.X, sample.Y, sample.Z,
                    positionArgument, headingArgument,
                    nodeType, 3f, 0);

                if (found)
                {
                    position = positionArgument.GetResult<Vector3>();
                    heading = headingArgument.GetResult<float>();
                    return true;
                }
            }

            position = Vector3.Zero;
            heading = 0f;
            return false;
        }

        private bool SpawnVehicle(SpawnCandidate candidate, DateTime now)
        {
            if (_preloadedModel == null || !_preloadedModel.IsLoaded)
                return false;

            Vehicle vehicle = null;
            Ped driver = null;

            try
            {
                // The model has already been loaded asynchronously, so this call
                // returns immediately rather than blocking on streaming.
                vehicle = Vehicle.Create(_preloadedModel, candidate.Position, candidate.Heading);
                if (vehicle == null || !vehicle.Exists())
                    return false;

                bool placed = Function.Call<bool>(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle.Handle);
                if (!placed || Function.Call<bool>(Hash.IS_ENTITY_IN_WATER, vehicle.Handle))
                {
                    vehicle.Delete();
                    return false;
                }

                vehicle.IsPersistent = true;
                Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT, vehicle.Handle, CreateRandomPlate());

                driver = vehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);
                if (driver == null || !driver.Exists())
                {
                    vehicle.Delete();
                    return false;
                }

                driver.IsPersistent = true;
                driver.BlockPermanentEvents = true;
                driver.KeepTaskWhenMarkedAsNoLongerNeeded = true;

                Function.Call(Hash.SET_DRIVER_ABILITY, driver.Handle, 0.85f);
                Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver.Handle, 0.08f);
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle.Handle, true, true, false);

                float speed = GetCruiseSpeed(candidate.Position);
                Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER,
                    driver.Handle, vehicle.Handle, speed, NormalDrivingStyle);

                _active.Add(new ManagedTrafficVehicle
                {
                    Vehicle = vehicle,
                    Driver = driver,
                    SpawnedAt = now,
                    LastSampleAt = now,
                    LastMovedAt = now,
                    LastRecoveryAt = DateTime.MinValue,
                    LastPosition = vehicle.Position
                });

                RememberModel(_preloadedModelName);
                ModLog.Info(string.Format(
                    "Added scripted traffic vehicle {0}; road density {1:0}/15, active {2}/{3}.",
                    _preloadedModelName, candidate.RoadDensity,
                    _active.Count, _config.MaxAdditionalTrafficVehicles));

                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("Could not spawn scripted traffic model " + _preloadedModelName + ".", ex);
                if (driver != null && driver.Exists())
                    driver.Delete();
                if (vehicle != null && vehicle.Exists())
                    vehicle.Delete();
                return false;
            }
        }

        private float GetCruiseSpeed(Vector3 position)
        {
            string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, position.X, position.Y, position.Z);
            bool rural = !string.IsNullOrEmpty(zone) &&
                Array.IndexOf(RuralZones, zone.ToUpperInvariant()) >= 0;

            int hour = Function.Call<int>(Hash.GET_CLOCK_HOURS);
            if (rural)
                return hour >= 22 || hour < 6 ? 18f : 22f;

            return hour >= 22 || hour < 6 ? 15f : 17f;
        }

        private int GetEffectiveMaxVehicles(Ped player, DateTime now)
        {
            int max = _config.MaxAdditionalTrafficVehicles;
            Vector3 playerPosition = player.Position;
            string zone = Function.Call<string>(
                Hash.GET_NAME_OF_ZONE, playerPosition.X, playerPosition.Y, playerPosition.Z);
            bool rural = !string.IsNullOrEmpty(zone) &&
                Array.IndexOf(RuralZones, zone.ToUpperInvariant()) >= 0;

            int hour = Function.Call<int>(Hash.GET_CLOCK_HOURS);
            if (rural)
                max = Math.Max(1, max - 2);

            return Math.Max(0, max);
        }

        private int GetEffectiveSpawnInterval(Ped player, DateTime now)
        {
            int interval = _config.TrafficSpawnIntervalSeconds;
            Vector3 position = player.Position;
            string zone = Function.Call<string>(Hash.GET_NAME_OF_ZONE, position.X, position.Y, position.Z);
            bool rural = !string.IsNullOrEmpty(zone) &&
                Array.IndexOf(RuralZones, zone.ToUpperInvariant()) >= 0;

            int hour = Function.Call<int>(Hash.GET_CLOCK_HOURS);
            if (rural)
                interval = (int)Math.Round(interval * 1.5f);

            if ((hour >= 6 && hour <= 9) || (hour >= 16 && hour <= 19))
                interval = Math.Max(5, (int)Math.Round(interval * 0.8f));
            else if (hour < 5 || hour >= 23)
                interval = (int)Math.Round(interval * 1.3f);

            int playerVehicleHandle = Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, player.Handle, false);
            if (playerVehicleHandle != 0)
            {
                float speed = Function.Call<float>(Hash.GET_ENTITY_SPEED, playerVehicleHandle);
                if (speed > 32f)
                    interval = Math.Max(5, (int)Math.Round(interval * 0.85f));
            }

            return Math.Max(5, interval);
        }

        private void Cleanup(Ped player, DateTime now)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ManagedTrafficVehicle item = _active[i];
                if (item.Vehicle == null || !item.Vehicle.Exists())
                {
                    _active.RemoveAt(i);
                    continue;
                }

                bool playerUsing = Function.Call<bool>(
                    Hash.IS_PED_IN_VEHICLE, player.Handle, item.Vehicle.Handle, false);
                if (playerUsing)
                {
                    // The player has taken this vehicle; it is now theirs, so stop managing it.
                    _active.RemoveAt(i);
                    continue;
                }

                if (Function.Call<bool>(Hash.IS_ENTITY_IN_WATER, item.Vehicle.Handle))
                {
                    RemoveAt(i, false);
                    continue;
                }

                if (item.Driver == null || !item.Driver.Exists() ||
                    !Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, item.Driver.Handle, item.Vehicle.Handle, false))
                {
                    RemoveAt(i, false);
                    continue;
                }

                float distance = item.Vehicle.Position.DistanceTo(player.Position);
                bool offScreen = !item.Vehicle.IsOnScreen;
                bool tooFar = distance > _config.TrafficDespawnDistanceMeters;

                if (tooFar && offScreen)
                {
                    RemoveAt(i, false);
                    continue;
                }

                if (now - item.SpawnedAt > TimeSpan.FromMinutes(6) &&
                    distance > 120f && offScreen)
                {
                    RemoveAt(i, false);
                    continue;
                }

                if (now - item.LastSampleAt >= TimeSpan.FromSeconds(25))
                {
                    Vector3 current = item.Vehicle.Position;
                    if (current.DistanceTo(item.LastPosition) >= 4f)
                    {
                        item.LastMovedAt = now;
                        item.LastPosition = current;
                    }
                    item.LastSampleAt = now;
                }

                // Recover a genuinely stuck AI car once; never delete one while it is visible.
                if (now - item.LastMovedAt > TimeSpan.FromSeconds(55) &&
                    now - item.LastRecoveryAt > TimeSpan.FromSeconds(50) &&
                    !item.Vehicle.IsStoppedAtTrafficLights)
                {
                    Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER,
                        item.Driver.Handle, item.Vehicle.Handle, 16f, NormalDrivingStyle);
                    item.LastRecoveryAt = now;
                    item.LastMovedAt = now;
                    ModLog.Warn("Reissued driving task to a stalled ambient traffic vehicle.");
                }
            }
        }

        private void RetireExcessVehicles(int desiredCount)
        {
            int excess = _active.Count - Math.Max(0, desiredCount);
            if (excess <= 0)
                return;

            // Never make a visible car pop out. Retire only vehicles already outside the view.
            for (int i = _active.Count - 1; i >= 0 && excess > 0; i--)
            {
                ManagedTrafficVehicle item = _active[i];
                if (item.Vehicle == null || !item.Vehicle.Exists() || !item.Vehicle.IsOnScreen)
                {
                    RemoveAt(i, true);
                    excess--;
                }
            }
        }

        private void RememberModel(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            _recentModels.Enqueue(name);
            while (_recentModels.Count > 10)
                _recentModels.Dequeue();
        }

        private string CreateRandomPlate()
        {
            const string letters = "ABCDEFGHJKLMNPRSTUVWXYZ";
            const string digits = "0123456789";
            char[] plate = new char[7];
            for (int i = 0; i < plate.Length; i++)
            {
                string source = (i == 3 || i == 4 || i == 5) ? digits : letters;
                plate[i] = source[_random.Next(source.Length)];
            }
            return new string(plate);
        }

        private float RandomBetween(float minimum, float maximum)
        {
            return minimum + (float)_random.NextDouble() * (maximum - minimum);
        }

        public void StopAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                RemoveAt(i, true);

            _active.Clear();
            ReleasePreloadedModel();
        }

        private void RemoveAt(int index, bool preserveIfPlayerOccupied)
        {
            if (index < 0 || index >= _active.Count)
                return;

            ManagedTrafficVehicle item = _active[index];
            _active.RemoveAt(index);

            try
            {
                Ped player = Game.Player.Character;
                if (preserveIfPlayerOccupied && player != null && player.Exists() &&
                    item.Vehicle != null && item.Vehicle.Exists() &&
                    Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, player.Handle, item.Vehicle.Handle, false))
                    return;

                if (item.Driver != null && item.Driver.Exists())
                    item.Driver.Delete();

                if (item.Vehicle != null && item.Vehicle.Exists())
                    item.Vehicle.Delete();
            }
            catch (Exception ex)
            {
                ModLog.Warn("Extra traffic cleanup failed: " + ex.Message);
            }
        }
    }
}
