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
                string configPath = ResolveConfigPath();
                ModLog.Info("Loading configuration from: " + configPath);

                _config = ModConfig.Load(configPath);
                _scheduler = new UpdateScheduler(_config.UpdateIntervalMs);
                _cityActivity = new CityActivitySystem(_config);
                _cityEvents = new CityEventManager(_config);
                _initialized = true;

                ModLog.Info("Living Los Santos initialized. On-screen startup notification is disabled for compatibility.");
            }
            catch (Exception ex)
            {
                _initialized = false;
                ModLog.Error("Initialization failed. Script will not retry every frame; restart the game after fixing the reported error.", ex);
            }
        }

        private static string ResolveConfigPath()
        {
            string currentDirectory = Environment.CurrentDirectory;
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string assemblyDirectory = Path.GetDirectoryName(typeof(LivingLosSantosScript).Assembly.Location);

            // SHVDN shadow-copies script assemblies into LocalAppData\assembly\dl3.
            // Prefer the real GTA V scripts folder over the shadow-copy directory.
            string[] candidates =
            {
                Path.Combine(currentDirectory, "scripts", "config.ini"),
                Path.Combine(baseDirectory, "scripts", "config.ini"),
                Path.Combine(currentDirectory, "config.ini"),
                Path.Combine(baseDirectory, "config.ini"),
                Path.Combine(assemblyDirectory ?? currentDirectory, "config.ini")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            // If no config exists yet, create it in the real scripts folder where
            // the user expects to edit it, rather than in SHVDN's shadow cache.
            string scriptsDirectory = Path.Combine(currentDirectory, "scripts");
            if (Directory.Exists(scriptsDirectory))
                return Path.Combine(scriptsDirectory, "config.ini");

            scriptsDirectory = Path.Combine(baseDirectory, "scripts");
            if (Directory.Exists(scriptsDirectory))
                return Path.Combine(scriptsDirectory, "config.ini");

            return Path.Combine(assemblyDirectory ?? currentDirectory, "config.ini");
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
