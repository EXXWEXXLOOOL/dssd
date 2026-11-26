using System;
using LemonUI;
using LemonUI.Menus;
using LivingLosSantos.Core;

namespace LivingLosSantos.UI
{
    internal sealed class CitySettingsMenu
    {
        private readonly ModConfig _config;
        private readonly Action _save;
        private readonly ObjectPool _pool;
        private readonly NativeMenu _menu;
        private readonly NativeItem _presetItem;
        private readonly NativeSliderItem _maxTrafficItem;
        private readonly NativeSliderItem _spawnIntervalItem;
        private readonly NativeSliderItem _vanillaDensityItem;
        private readonly NativeSliderItem _parkedDensityItem;
        private bool _updatingControls;
        private int _presetIndex = 1;

        private static readonly string[] PresetNames = { "LOW", "REALISTIC", "BUSY" };

        public CitySettingsMenu(ModConfig config, Action save)
        {
            _config = config;
            _save = save;
            _pool = new ObjectPool();
            _menu = new NativeMenu("LIVING LOS SANTOS", "City simulation settings");

            var modEnabled = new NativeCheckboxItem("Mod enabled", "Master switch for all simulation systems.", _config.Enabled);
            modEnabled.Activated += delegate
            {
                _config.Enabled = modEnabled.Checked;
                Save();
            };
            _menu.Add(modEnabled);

            var livingCity = new NativeCheckboxItem("Living city", "Enable the time-of-day activity profile.", _config.LivingCityEnabled);
            livingCity.Activated += delegate
            {
                _config.LivingCityEnabled = livingCity.Checked;
                Save();
            };
            _menu.Add(livingCity);

            var cityEvents = new NativeCheckboxItem("City events", "Enable roadside scenes and random events.", _config.CityEventsEnabled);
            cityEvents.Activated += delegate
            {
                _config.CityEventsEnabled = cityEvents.Checked;
                Save();
            };
            _menu.Add(cityEvents);

            _menu.Add(new NativeSeparatorItem("TRAFFIC"));

            var trafficEnabled = new NativeCheckboxItem("Enhanced traffic", "Add NPC-driven vehicles on nearby roads.", _config.TrafficEnabled);
            trafficEnabled.Activated += delegate
            {
                _config.TrafficEnabled = trafficEnabled.Checked;
                Save();
            };
            _menu.Add(trafficEnabled);

            _maxTrafficItem = new NativeSliderItem("Extra AI vehicles", "Maximum additional NPC-driven cars around the player.", 10, _config.MaxAdditionalTrafficVehicles);
            _maxTrafficItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.MaxAdditionalTrafficVehicles = _maxTrafficItem.Value;
                SetSliderTitle(_maxTrafficItem, _maxTrafficItem.Value.ToString());
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_maxTrafficItem, _config.MaxAdditionalTrafficVehicles.ToString());
            _menu.Add(_maxTrafficItem);

            _spawnIntervalItem = new NativeSliderItem("Spawn interval", "Seconds between attempts to add another traffic vehicle.", 60, _config.TrafficSpawnIntervalSeconds);
            _spawnIntervalItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.TrafficSpawnIntervalSeconds = Math.Max(5, _spawnIntervalItem.Value);
                SetSliderTitle(_spawnIntervalItem, _config.TrafficSpawnIntervalSeconds + "s");
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_spawnIntervalItem, _config.TrafficSpawnIntervalSeconds + "s");
            _menu.Add(_spawnIntervalItem);

            _vanillaDensityItem = new NativeSliderItem("Regular traffic", "Native ambient traffic multiplier. 100% is the GTA V normal maximum.", 100, (int)Math.Round(_config.VanillaTrafficDensity * 100f));
            _vanillaDensityItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.VanillaTrafficDensity = _vanillaDensityItem.Value / 100f;
                SetSliderTitle(_vanillaDensityItem, _vanillaDensityItem.Value + "%");
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_vanillaDensityItem, ((int)Math.Round(_config.VanillaTrafficDensity * 100f)) + "%");
            _menu.Add(_vanillaDensityItem);

            _parkedDensityItem = new NativeSliderItem("Parked vehicles", "Street parking density multiplier.", 100, (int)Math.Round(_config.ParkedVehicleDensity * 100f));
            _parkedDensityItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.ParkedVehicleDensity = _parkedDensityItem.Value / 100f;
                SetSliderTitle(_parkedDensityItem, _parkedDensityItem.Value + "%");
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_parkedDensityItem, ((int)Math.Round(_config.ParkedVehicleDensity * 100f)) + "%");
            _menu.Add(_parkedDensityItem);

            _presetItem = new NativeItem("Traffic preset", "Cycle between Low, Realistic and Busy traffic profiles.");
            _presetItem.AltTitle = PresetNames[_presetIndex];
            _presetItem.Activated += delegate { NextPreset(); };
            _menu.Add(_presetItem);

            var saveItem = new NativeItem("Save settings", "Write current options to config.ini.");
            saveItem.Activated += delegate { Save(); };
            _menu.Add(saveItem);

            _pool.Add(_menu);
        }

        public bool Visible
        {
            get { return _menu.Visible; }
            set { _menu.Visible = value; }
        }

        public void Process()
        {
            _pool.Process();
        }

        private void NextPreset()
        {
            _presetIndex = (_presetIndex + 1) % PresetNames.Length;
            _updatingControls = true;

            if (_presetIndex == 0)
            {
                _config.MaxAdditionalTrafficVehicles = 2;
                _config.TrafficSpawnIntervalSeconds = 24;
                _config.VanillaTrafficDensity = 0.85f;
                _config.ParkedVehicleDensity = 0.75f;
            }
            else if (_presetIndex == 1)
            {
                _config.MaxAdditionalTrafficVehicles = 4;
                _config.TrafficSpawnIntervalSeconds = 12;
                _config.VanillaTrafficDensity = 1.0f;
                _config.ParkedVehicleDensity = 1.0f;
            }
            else
            {
                _config.MaxAdditionalTrafficVehicles = 8;
                _config.TrafficSpawnIntervalSeconds = 7;
                _config.VanillaTrafficDensity = 1.0f;
                _config.ParkedVehicleDensity = 1.0f;
            }

            _maxTrafficItem.Value = _config.MaxAdditionalTrafficVehicles;
            _spawnIntervalItem.Value = _config.TrafficSpawnIntervalSeconds;
            _vanillaDensityItem.Value = (int)Math.Round(_config.VanillaTrafficDensity * 100f);
            _parkedDensityItem.Value = (int)Math.Round(_config.ParkedVehicleDensity * 100f);
            SetSliderTitle(_maxTrafficItem, _maxTrafficItem.Value.ToString());
            SetSliderTitle(_spawnIntervalItem, _spawnIntervalItem.Value + "s");
            SetSliderTitle(_vanillaDensityItem, _vanillaDensityItem.Value + "%");
            SetSliderTitle(_parkedDensityItem, _parkedDensityItem.Value + "%");
            _presetItem.AltTitle = PresetNames[_presetIndex];

            _updatingControls = false;
            Save();
        }

        private void MarkCustomPreset()
        {
            _presetItem.AltTitle = "CUSTOM";
            _presetIndex = 1;
        }

        private static void SetSliderTitle(NativeSliderItem item, string title)
        {
            item.AltTitle = title;
        }

        private void Save()
        {
            if (_save != null)
                _save();
        }
    }
}
