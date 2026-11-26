using System;
using System.IO;
using GTA;
using LivingLosSantos.Core;
using LivingLosSantos.Systems;

namespace LivingLosSantos
{
    public sealed class LivingLosSantosScript : Script
    {
        private ModConfig _config;
        private UpdateScheduler _scheduler;
        private CityActivitySystem _cityActivity;
        private CityEventManager _cityEvents;
        private bool _initialized;
        private bool _initializationAttempted;

        public LivingLosSantosScript()
        {
            // Create the log as soon as SHVDN constructs this script.
            try
            {
                string assemblyDirectory = Path.GetDirectoryName(typeof(LivingLosSantosScript).Assembly.Location);
                if (string.IsNullOrEmpty(assemblyDirectory))
                    assemblyDirectory = AppDomain.CurrentDomain.BaseDirectory;

                ModLog.Initialize(assemblyDirectory);
                ModLog.Info("Script instance constructed; waiting for first game tick.");
            }
            catch
            {
                // Diagnostics must never prevent SHVDN from loading the script.
            }

            Tick += OnTick;
            Aborted += OnAborted;
            Interval = 100;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_initializationAttempted)
            {
                _initializationAttempted = true;
                ModLog.Info("First game tick received; initializing mod.");
                Initialize();
            }

            if (!_initialized || !_config.Enabled || !_scheduler.IsDue(DateTime.Now))
                return;

            try
            {
                if (_config.LivingCityEnabled)
                    _cityActivity.Update();

                if (_config.CityEventsEnabled)
                    _cityEvents.Update(DateTime.Now);
            }
            catch (Exception ex)
            {
                ModLog.Error("Unhandled update error.", ex);
            }
        }

        private void Initialize()
        {
            try
            {
                string assemblyDirectory = Path.GetDirectoryName(typeof(LivingLosSantosScript).Assembly.Location);
                if (string.IsNullOrEmpty(assemblyDirectory))
                    assemblyDirectory = AppDomain.CurrentDomain.BaseDirectory;

                _config = ModConfig.Load(Path.Combine(assemblyDirectory, "config.ini"));
                _scheduler = new UpdateScheduler(_config.UpdateIntervalMs);
                _cityActivity = new CityActivitySystem(_config);
                _cityEvents = new CityEventManager(_config);
                _initialized = true;

                // Screen.ShowSubtitle in the installed SHVDN/GTA combination
                // throws from SHVDN.NativeMemory. Keep initialization independent
                // from the optional HUD notification; use the log as confirmation.
                ModLog.Info("Living Los Santos initialized. On-screen startup notification is disabled for compatibility.");
            }
            catch (Exception ex)
            {
                _initialized = false;
                ModLog.Error("Initialization failed. Script will not retry every frame; restart the game after fixing the reported error.", ex);
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try
            {
                if (_cityEvents != null)
                    _cityEvents.StopAll();
                ModLog.Info("Living Los Santos stopped; active event entities cleaned up.");
            }
            catch (Exception ex)
            {
                ModLog.Error("Shutdown cleanup failed.", ex);
            }
        }
    }
}
