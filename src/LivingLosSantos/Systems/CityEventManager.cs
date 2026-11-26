using System;
using System.Collections.Generic;
using GTA;
using LivingLosSantos.Core;
using LivingLosSantos.Systems.Events;

namespace LivingLosSantos.Systems
{
    internal sealed class CityEventManager
    {
        private readonly ModConfig _config;
        private readonly Random _random = new Random();
        private readonly List<RoadsideBreakdownEvent> _activeEvents = new List<RoadsideBreakdownEvent>();
        private DateTime _nextCheck = DateTime.MinValue;

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

            Vector3 direction = player.ForwardVector;
            Vector3 candidate = player.Position + direction * _config.MaximumEventDistance;
            Vector3 spawnPosition = World.GetNextPositionOnStreet(candidate);

            float distance = spawnPosition.DistanceTo(player.Position);
            if (distance < _config.MinimumEventDistance || distance > _config.MaximumEventDistance + 40f)
                return;

            var cityEvent = new RoadsideBreakdownEvent(spawnPosition, player.Heading);
            if (cityEvent.Start())
            {
                _activeEvents.Add(cityEvent);
                ModLog.Info("Started city event: roadside breakdown.");
            }
        }

        public void StopAll()
        {
            foreach (RoadsideBreakdownEvent cityEvent in _activeEvents)
                cityEvent.Stop();
            _activeEvents.Clear();
        }
    }
}
