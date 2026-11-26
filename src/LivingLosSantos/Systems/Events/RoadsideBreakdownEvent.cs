using System;
using GTA;
using GTA.Math;
using LivingLosSantos.Core;

namespace LivingLosSantos.Systems.Events
{
    internal sealed class RoadsideBreakdownEvent
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

                _vehicle = World.CreateVehicle(model, _position, _heading);
                if (_vehicle == null || !_vehicle.Exists())
                    return false;

                _vehicle.IsPersistent = true;
                _vehicle.EngineHealth = 35f;
                _vehicle.IsDriveable = false;
                _expiresAt = DateTime.Now.AddMinutes(4);

                ModLog.Info(string.Format("Spawned broken-down {0} at {1}.", _vehicleModel, _zoneName));
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
