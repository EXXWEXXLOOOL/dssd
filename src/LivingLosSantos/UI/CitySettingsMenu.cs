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
        private readonly NativeSliderItem _movingTrafficItem;
        private readonly NativeSliderItem _parkedTrafficItem;
        private bool _updatingControls;
        private int _presetIndex = 1;

        private static readonly string[] PresetNames = { "LOW", "REALISTIC", "BUSY" };

        public CitySettingsMenu(ModConfig config, Action save)
        {
            _config = config;
            _save = save;
            _pool = new ObjectPool();
            _menu = new NativeMenu("LIVING LOS SANTOS", "City simulation settings");

            var modEnabled = new NativeCheckboxItem("Mod enabled", "Master switch for all Living Los Santos systems.", _config.Enabled);
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

            _menu.Add(new NativeSeparatorItem("AMBIENT TRAFFIC"));

            var trafficEnabled = new NativeCheckboxItem(
                "Traffic control",
                "Apply density multipliers to GTA's normal population system. Off leaves game traffic untouched.",
                _config.TrafficEnabled);
            trafficEnabled.Activated += delegate
            {
                _config.TrafficEnabled = trafficEnabled.Checked;
                Save();
            };
            _menu.Add(trafficEnabled);

            _movingTrafficItem = new NativeSliderItem(
                "Moving traffic",
                "Scale of the traffic configured by GTA and any installed traffic overhaul. 100% is full configured density.",
                100,
                (int)Math.Round(_config.TrafficDensityMultiplier * 100f));
            _movingTrafficItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.TrafficDensityMultiplier = _movingTrafficItem.Value / 100f;
                SetSliderTitle(_movingTrafficItem, _movingTrafficItem.Value + "%");
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_movingTrafficItem, ((int)Math.Round(_config.TrafficDensityMultiplier * 100f)) + "%");
            _menu.Add(_movingTrafficItem);

            _parkedTrafficItem = new NativeSliderItem(
                "Parked vehicles",
                "Scale the parked-car population defined by the game files.",
                100,
                (int)Math.Round(_config.ParkedVehicleDensity * 100f));
            _parkedTrafficItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.ParkedVehicleDensity = _parkedTrafficItem.Value / 100f;
                SetSliderTitle(_parkedTrafficItem, _parkedTrafficItem.Value + "%");
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_parkedTrafficItem, ((int)Math.Round(_config.ParkedVehicleDensity * 100f)) + "%");
            _menu.Add(_parkedTrafficItem);

            _presetItem = new NativeItem("Traffic preset", "Choose Low, Realistic or Busy. Presets only scale the game's own traffic.");
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
                _config.TrafficDensityMultiplier = 0.60f;
                _config.ParkedVehicleDensity = 0.60f;
            }
            else if (_presetIndex == 1)
            {
                _config.TrafficDensityMultiplier = 0.85f;
                _config.ParkedVehicleDensity = 0.80f;
            }
            else
            {
                _config.TrafficDensityMultiplier = 1.0f;
                _config.ParkedVehicleDensity = 1.0f;
            }

            _movingTrafficItem.Value = (int)Math.Round(_config.TrafficDensityMultiplier * 100f);
            _parkedTrafficItem.Value = (int)Math.Round(_config.ParkedVehicleDensity * 100f);
            SetSliderTitle(_movingTrafficItem, _movingTrafficItem.Value + "%");
            SetSliderTitle(_parkedTrafficItem, _parkedTrafficItem.Value + "%");
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
