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
        private Vehicle _vehicle;
        private DateTime _expiresAt;

        public RoadsideBreakdownEvent(Vector3 position, float heading)
        {
            _position = position;
            _heading = heading;
        }

        public bool Start()
        {
            try
            {
                Model model = new Model(VehicleHash.Asea);
                if (!model.IsInCdImage || !model.IsValid)
                    return false;

                model.Request(1000);
                if (!model.IsLoaded)
                    return false;

                _vehicle = World.CreateVehicle(model, _position, _heading);
                model.MarkAsNoLongerNeeded();

                if (_vehicle == null || !_vehicle.Exists())
                    return false;

                _vehicle.IsPersistent = true;
                _vehicle.EngineHealth = 120f;
                _vehicle.IsDriveable = false;
                _expiresAt = DateTime.Now.AddMinutes(3);
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("Could not create roadside breakdown event.", ex);
                Cleanup();
                return false;
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
