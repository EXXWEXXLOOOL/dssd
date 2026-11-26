using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using LivingLosSantos.Core;

namespace LivingLosSantos.Systems
{
    internal sealed class TrafficManager
    {
        private static readonly string[] VehicleModels =
        {
            "asea", "asterope", "ingot", "intruder", "premier", "primo",
            "regina", "stanier", "stratum", "washington", "fugitive",
            "oracle", "tailgater", "schafter2", "buffalo", "jackal",
            "sentinel", "sentinel2", "felon", "felon2", "f620",
            "sultan", "kuruma", "penumbra", "landstalker", "granger",
            "seminole", "cavalcade", "dubsta", "patriot", "sadler",
            "bison", "bobcatxl", "speedo", "rumpo"
        };

        private const int DefaultDrivingStyle = 786603;
        private readonly ModConfig _config;
        private readonly Random _random = new Random();
        private readonly List<ManagedVehicle> _vehicles = new List<ManagedVehicle>();
        private DateTime _nextSpawnAt = DateTime.MinValue;

        private sealed class ManagedVehicle
        {
            public Vehicle Vehicle;
            public Ped Driver;
            public DateTime SpawnedAt;
        }

        public TrafficManager(ModConfig config)
        {
            _config = config;
        }

        public int ActiveVehicleCount { get { return _vehicles.Count; } }

        public void Update(DateTime now)
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists())
                return;

            CleanupDistantVehicles(player);

            while (_vehicles.Count > _config.MaxAdditionalTrafficVehicles)
                RemoveVehicleAt(0, true);

            if (_vehicles.Count >= _config.MaxAdditionalTrafficVehicles || now < _nextSpawnAt)
                return;

            _nextSpawnAt = now.AddSeconds(_config.TrafficSpawnIntervalSeconds);
            TrySpawnTrafficVehicle(player.Position);
        }

        private void CleanupDistantVehicles(Ped player)
        {
            for (int i = _vehicles.Count - 1; i >= 0; i--)
            {
                ManagedVehicle managed = _vehicles[i];
                if (managed.Vehicle == null || !managed.Vehicle.Exists())
                {
                    _vehicles.RemoveAt(i);
                    continue;
                }

                bool playerUsingVehicle = Function.Call<bool>(
                    Hash.IS_PED_IN_VEHICLE, player.Handle, managed.Vehicle.Handle, false);
                if (playerUsingVehicle)
                {
                    // A player may take one of our generated cars. Leave it untouched.
                    _vehicles.RemoveAt(i);
                    continue;
                }

                float distance = managed.Vehicle.Position.DistanceTo(player.Position);
                bool expired = DateTime.Now - managed.SpawnedAt > TimeSpan.FromMinutes(4);
                bool abandoned = managed.Driver == null || !managed.Driver.Exists() ||
                    !Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, managed.Driver.Handle, managed.Vehicle.Handle, false);

                if (distance > _config.TrafficDespawnDistanceMeters || expired || abandoned)
                    RemoveVehicleAt(i, false);
            }
        }

        private bool TrySpawnTrafficVehicle(Vector3 playerPosition)
        {
            Vector3 spawnPosition;
            float heading;
            if (!TryFindRoadNode(playerPosition, out spawnPosition, out heading))
                return false;

            string modelName = VehicleModels[_random.Next(VehicleModels.Length)];
            Model model = new Model(modelName);
            Vehicle vehicle = null;

            try
            {
                if (!model.IsInCdImage || !model.IsValid || !model.IsVehicle)
                    return false;

                vehicle = Vehicle.Create(model, spawnPosition, heading);
                if (vehicle == null || !vehicle.Exists())
                    return false;

                Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, vehicle.Handle);
                vehicle.IsPersistent = true;

                Ped driver = vehicle.CreateRandomPedOnSeat(VehicleSeat.Driver);
                if (driver == null || !driver.Exists())
                {
                    vehicle.Delete();
                    return false;
                }

                driver.IsPersistent = true;
                driver.BlockPermanentEvents = true;
                driver.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                Function.Call(Hash.SET_DRIVER_ABILITY, driver.Handle, 0.85f);
                Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver.Handle, 0.05f);
                Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER,
                    driver.Handle, vehicle.Handle, 19.0f, DefaultDrivingStyle);

                _vehicles.Add(new ManagedVehicle
                {
                    Vehicle = vehicle,
                    Driver = driver,
                    SpawnedAt = DateTime.Now
                });

                ModLog.Info("Added ambient AI traffic vehicle: " + modelName + ".");
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("Could not create an additional traffic vehicle: " + modelName + ".", ex);
                if (vehicle != null && vehicle.Exists())
                    vehicle.Delete();
                return false;
            }
            finally
            {
                model.MarkAsNoLongerNeeded();
            }
        }

        private bool TryFindRoadNode(Vector3 playerPosition, out Vector3 nodePosition, out float heading)
        {
            for (int attempt = 0; attempt < 18; attempt++)
            {
                double angle = _random.NextDouble() * Math.PI * 2.0;
                float radius = _config.TrafficSpawnMinDistanceMeters +
                    (float)_random.NextDouble() *
                    (_config.TrafficSpawnMaxDistanceMeters - _config.TrafficSpawnMinDistanceMeters);

                Vector3 sample = new Vector3(
                    playerPosition.X + (float)Math.Cos(angle) * radius,
                    playerPosition.Y + (float)Math.Sin(angle) * radius,
                    playerPosition.Z);

                if (!TryGetVehicleNode(sample, 0, out nodePosition, out heading) &&
                    !TryGetVehicleNode(sample, 1, out nodePosition, out heading))
                    continue;

                float distance = nodePosition.DistanceTo(playerPosition);
                if (distance < _config.TrafficSpawnMinDistanceMeters - 25f ||
                    distance > _config.TrafficSpawnMaxDistanceMeters + 25f)
                    continue;

                if (!Function.Call<bool>(Hash.IS_POINT_ON_ROAD,
                    nodePosition.X, nodePosition.Y, nodePosition.Z, 0))
                    continue;

                if (Function.Call<bool>(Hash.IS_ANY_VEHICLE_NEAR_POINT,
                    nodePosition.X, nodePosition.Y, nodePosition.Z, 14f))
                    continue;

                nodePosition.Z = GetGroundHeight(nodePosition);
                return true;
            }

            nodePosition = Vector3.Zero;
            heading = 0f;
            return false;
        }

        private static bool TryGetVehicleNode(Vector3 candidate, int nodeType, out Vector3 position, out float heading)
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
                    position = positionArgument.GetResult<Vector3>();
                    heading = headingArgument.GetResult<float>();
                    return true;
                }
            }

            position = Vector3.Zero;
            heading = 0f;
            return false;
        }

        private static float GetGroundHeight(Vector3 position)
        {
            using (OutputArgument groundArgument = new OutputArgument())
            {
                bool found = Function.Call<bool>(
                    Hash.GET_GROUND_Z_FOR_3D_COORD,
                    position.X, position.Y, position.Z + 30f,
                    groundArgument, false);

                return found ? groundArgument.GetResult<float>() : position.Z;
            }
        }

        public void StopAll()
        {
            for (int i = _vehicles.Count - 1; i >= 0; i--)
                RemoveVehicleAt(i, true);
            _vehicles.Clear();
        }

        private void RemoveVehicleAt(int index, bool preserveIfPlayerOccupied)
        {
            if (index < 0 || index >= _vehicles.Count)
                return;

            ManagedVehicle managed = _vehicles[index];
            _vehicles.RemoveAt(index);

            try
            {
                Ped player = Game.Player.Character;
                if (preserveIfPlayerOccupied && player != null && player.Exists() &&
                    managed.Vehicle != null && managed.Vehicle.Exists() &&
                    Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, player.Handle, managed.Vehicle.Handle, false))
                    return;

                if (managed.Driver != null && managed.Driver.Exists())
                    managed.Driver.Delete();

                if (managed.Vehicle != null && managed.Vehicle.Exists())
                    managed.Vehicle.Delete();
            }
            catch (Exception ex)
            {
                ModLog.Warn("Traffic vehicle cleanup failed: " + ex.Message);
            }
        }
    }

    internal static class TrafficDensityController
    {
        public static void ApplyFrame(ModConfig config, CityActivitySystem activity)
        {
            if (config == null || !config.Enabled || !config.TrafficEnabled)
                return;

            float timeFactor = config.LivingCityEnabled && activity != null
                ? activity.TrafficMultiplier
                : 1.0f;

            float vehicleDensity = Clamp01(config.VanillaTrafficDensity * timeFactor);
            float parkedDensity = Clamp01(config.ParkedVehicleDensity);

            // GTA's frame-based density natives top out at 1.0 (normal traffic).
            // TrafficManager adds extra AI-driven vehicles for density above stock.
            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, vehicleDensity);
            Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, vehicleDensity);
            Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, parkedDensity);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
