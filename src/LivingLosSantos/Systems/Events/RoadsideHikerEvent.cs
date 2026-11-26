using System;
using GTA;
using GTA.Math;
using GTA.Native;
using LivingLosSantos.Core;
using LivingLosSantos.Systems;

namespace LivingLosSantos.Systems.Events
{
    internal sealed class RoadsideHikerEvent : ICityEvent
    {
        private static readonly string[] PedModels =
        {
            "a_m_y_hiker_01",
            "a_f_y_hiker_01",
            "a_m_m_hillbilly_01"
        };

        private readonly Vector3 _position;
        private readonly float _heading;
        private readonly string _zoneName;
        private readonly Random _random = new Random();
        private Ped _ped;
        private DateTime _expiresAt;

        public RoadsideHikerEvent(Vector3 position, float heading, string zoneName)
        {
            _position = position;
            _heading = heading;
            _zoneName = zoneName;
        }

        public bool Start()
        {
            string modelName = PedModels[_random.Next(PedModels.Length)];
            Model model = new Model(modelName);

            try
            {
                if (!model.IsInCdImage || !model.IsValid)
                    return false;

                model.Request(2000);
                if (!model.IsLoaded)
                {
                    ModLog.Warn("Timed out loading roadside hiker model: " + modelName);
                    return false;
                }

                _ped = Ped.Create(model, _position, (_heading + 180f) % 360f);
                if (_ped == null || !_ped.Exists())
                    return false;

                if (Function.Call<bool>(Hash.IS_POINT_ON_ROAD,
                    _ped.Position.X, _ped.Position.Y, _ped.Position.Z, 0))
                {
                    ModLog.Warn("Rejected hiker spawn because the pedestrian is still on the road.");
                    Cleanup();
                    return false;
                }

                _ped.IsPersistent = true;
                _ped.BlockPermanentEvents = true;
                _ped.KeepTaskWhenMarkedAsNoLongerNeeded = true;
                _ped.Task.StartScenarioInPlace("WORLD_HUMAN_STAND_MOBILE", -1, true);
                _expiresAt = DateTime.Now.AddMinutes(3);

                ModLog.Info("Spawned roadside hiker " + modelName + " in " + _zoneName + ".");
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("Could not create roadside hiker event.", ex);
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
            if (_ped == null || !_ped.Exists() || DateTime.Now >= _expiresAt)
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
                if (_ped != null && _ped.Exists())
                    _ped.Delete();
            }
            catch (Exception ex)
            {
                ModLog.Warn("Hiker event cleanup encountered an issue: " + ex.Message);
            }
            finally
            {
                _ped = null;
            }
        }
    }
}
