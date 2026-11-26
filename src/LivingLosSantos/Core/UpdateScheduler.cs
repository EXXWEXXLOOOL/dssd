using System;

namespace LivingLosSantos.Core
{
    internal sealed class UpdateScheduler
    {
        private readonly int _intervalMs;
        private DateTime _nextRun = DateTime.MinValue;

        public UpdateScheduler(int intervalMs)
        {
            _intervalMs = Math.Max(100, intervalMs);
        }

        public bool IsDue(DateTime now)
        {
            if (now < _nextRun)
                return false;

            _nextRun = now.AddMilliseconds(_intervalMs);
            return true;
        }
    }
}
