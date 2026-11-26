using System;
using GTA;
using GTA.Math;
using GTA.Native;
using LivingLosSantos.Core;
using LivingLosSantos.Systems;

namespace LivingLosSantos.Systems.Events
{
    internal sealed class RoadsideBreakdownEvent : ICityEvent
    {
        private readonly Vector3 _position;
        private readonly float _heading;
        private readonly VehicleHash _vehicleModel;
        private readonly string _zoneName;
        private Vehicle _vehicle;
        private DateTime _expiresAt;

        public RoadsideBreakdownEvent(Vector3 position, float heading, VehicleHash vehicleModel, string zoneName)
        {
            _position = position;
            _heading = heading;
            _vehicleModel = vehicleModel;
            _zoneName = zoneName;
        }

        public bool Start()
        {
            Model model = new Model(_vehicleModel);
            try
            {
                if (!model.IsInCdImage || !model.IsValid)
                    return false;

                model.Request(2000);
                if (!model.IsLoaded)
                {
                    ModLog.Warn("Timed out loading breakdown vehicle model: " + _vehicleModel);
                    return false;
                }

                _vehicle = Vehicle.Create(model, _position, _heading);
                if (_vehicle == null || !_vehicle.Exists())
                    return false;

                if (Function.Call<bool>(Hash.IS_POINT_ON_ROAD,
                    _vehicle.Position.X, _vehicle.Position.Y, _vehicle.Position.Z, 0))
                {
                    ModLog.Warn("Rejected breakdown spawn because the created vehicle is still on the road: " + _vehicleModel);
                    Cleanup();
                    return false;
                }

                Function.Call<bool>(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, _vehicle.Handle);
                _vehicle.IsPersistent = true;
                _vehicle.EngineHealth = 35f;
                _vehicle.IsUndriveable = true;
                _expiresAt = DateTime.Now.AddMinutes(4);

                ModLog.Info(string.Format("Spawned broken-down {0} on the shoulder in {1}.", _vehicleModel, _zoneName));
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("Could not create roadside breakdown event for " + _vehicleModel + ".", ex);
                Cleanup();
                return false;
            }
            finally
            {
                model.MarkAsNoLongerNeeded();
            }
        }

        public bool Update()
        {
            if (_vehicle == null || !_vehicle.Exists() || DateTime.Now >= _expiresAt)
            {
                Cleanup();
                return false;
            }

            return true;
        }

        public void Stop()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            try
            {
                if (_vehicle != null && _vehicle.Exists())
                    _vehicle.Delete();
            }
            catch (Exception ex)
            {
                ModLog.Warn("Event cleanup encountered an issue: " + ex.Message);
            }
            finally
            {
                _vehicle = null;
            }
        }
    }
}
