using System;
using System.IO;
using System.Windows.Forms;
using GTA;
using LivingLosSantos.Core;
using LivingLosSantos.Systems;
using LivingLosSantos.UI;

namespace LivingLosSantos
{
    public sealed class LivingLosSantosScript : Script
    {
        private ModConfig _config;
        private UpdateScheduler _scheduler;
        private CityActivitySystem _cityActivity;
        private CityEventManager _cityEvents;
        private TrafficManager _trafficManager;
        private CitySettingsMenu _settingsMenu;
        private string _configPath;
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
            KeyDown += OnKeyDown;
            Aborted += OnAborted;
            // Traffic density natives and LemonUI need one update every rendered frame.
            Interval = 0;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_initializationAttempted)
            {
                _initializationAttempted = true;
                ModLog.Info("First game tick received; initializing mod.");
                Initialize();
            }

            if (!_initialized)
                return;

            try
            {
                if (_settingsMenu != null)
                    _settingsMenu.Process();

                if (!_config.Enabled)
                {
                    if (_trafficManager != null)
                        _trafficManager.Update(DateTime.Now);
                    return;
                }

                // GTA frame-based density controls must be applied on every frame.
                TrafficDensityController.ApplyFrame(_config);

                DateTime now = DateTime.Now;
                if (!_scheduler.IsDue(now))
                    return;

                if (_config.LivingCityEnabled)
                    _cityActivity.Update();

                _trafficManager.Update(now);

                if (_config.CityEventsEnabled)
                    _cityEvents.Update(now);
            }
            catch (Exception ex)
            {
                ModLog.Error("Unhandled update error.", ex);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.F10 || !_initialized || _settingsMenu == null)
                return;

            _settingsMenu.Visible = !_settingsMenu.Visible;
        }

        private void Initialize()
        {
            try
            {
                _configPath = ResolveConfigPath();
                ModLog.Info("Loading configuration from: " + _configPath);

                _config = ModConfig.Load(_configPath);
                _scheduler = new UpdateScheduler(_config.UpdateIntervalMs);
                _cityActivity = new CityActivitySystem(_config);
                _cityEvents = new CityEventManager(_config);
                _trafficManager = new TrafficManager(_config);
                _settingsMenu = new CitySettingsMenu(_config, SaveConfig);
                _initialized = true;

                ModLog.Info("Living Los Santos initialized. Press F10 to open settings.");
            }
            catch (Exception ex)
            {
                _initialized = false;
                ModLog.Error("Initialization failed. Script will not retry every frame; restart the game after fixing the reported error.", ex);
            }
        }

        private void SaveConfig()
        {
            try
            {
                if (!string.IsNullOrEmpty(_configPath) && _config != null)
                {
                    _config.Save(_configPath);
                    ModLog.Info("Settings saved to " + _configPath + ".");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("Could not save config.ini.", ex);
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
                if (_trafficManager != null)
                    _trafficManager.StopAll();
                if (_settingsMenu != null)
                    _settingsMenu.Visible = false;
                ModLog.Info("Living Los Santos stopped; active city events cleaned up.");
            }
            catch (Exception ex)
            {
                ModLog.Error("Shutdown cleanup failed.", ex);
            }
        }
    }
}
