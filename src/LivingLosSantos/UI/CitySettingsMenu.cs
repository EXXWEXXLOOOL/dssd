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
        private readonly NativeSliderItem _pedDensityItem;
        private readonly NativeSliderItem _maxAdditionalItem;
        private readonly NativeSliderItem _spawnIntervalItem;
        private readonly NativeSliderItem _spawnChanceItem;
        private bool _updatingControls;
        private int _presetIndex = 2;

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

            _pedDensityItem = new NativeSliderItem(
                "Pedestrian density",
                "Scale ambient pedestrians. 100% is the game's normal configured density.",
                100,
                (int)Math.Round(_config.PedDensityMultiplier * 100f));
            _pedDensityItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.PedDensityMultiplier = _pedDensityItem.Value / 100f;
                SetSliderTitle(_pedDensityItem, _pedDensityItem.Value + "%");
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_pedDensityItem, ((int)Math.Round(_config.PedDensityMultiplier * 100f)) + "%");
            _menu.Add(_pedDensityItem);

            _menu.Add(new NativeSeparatorItem("EXTRA AI TRAFFIC"));

            var extraTrafficEnabled = new NativeCheckboxItem(
                "Add extra AI cars",
                "Spawn a few additional NPC-driven cars ahead of you, outside the camera view.",
                _config.AdditionalTrafficEnabled);
            extraTrafficEnabled.Activated += delegate
            {
                _config.AdditionalTrafficEnabled = extraTrafficEnabled.Checked;
                Save();
            };
            _menu.Add(extraTrafficEnabled);

            _maxAdditionalItem = new NativeSliderItem(
                "Maximum extra cars",
                "Maximum number of additional scripted cars alive around you.",
                10,
                _config.MaxAdditionalTrafficVehicles);
            _maxAdditionalItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.MaxAdditionalTrafficVehicles = _maxAdditionalItem.Value;
                SetSliderTitle(_maxAdditionalItem, _maxAdditionalItem.Value.ToString());
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_maxAdditionalItem, _config.MaxAdditionalTrafficVehicles.ToString());
            _menu.Add(_maxAdditionalItem);

            _spawnIntervalItem = new NativeSliderItem(
                "Spawn interval",
                "Seconds between checks for another additional car.",
                60,
                _config.TrafficSpawnIntervalSeconds);
            _spawnIntervalItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.TrafficSpawnIntervalSeconds = Math.Max(5, _spawnIntervalItem.Value);
                _updatingControls = true;
                _spawnIntervalItem.Value = _config.TrafficSpawnIntervalSeconds;
                _updatingControls = false;
                SetSliderTitle(_spawnIntervalItem, _config.TrafficSpawnIntervalSeconds + "s");
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_spawnIntervalItem, _config.TrafficSpawnIntervalSeconds + "s");
            _menu.Add(_spawnIntervalItem);

            _spawnChanceItem = new NativeSliderItem(
                "Spawn chance",
                "Chance to add a car on each eligible spawn check.",
                100,
                _config.TrafficSpawnChancePercent);
            _spawnChanceItem.ValueChanged += delegate
            {
                if (_updatingControls) return;
                _config.TrafficSpawnChancePercent = _spawnChanceItem.Value;
                SetSliderTitle(_spawnChanceItem, _spawnChanceItem.Value + "%");
                MarkCustomPreset();
                Save();
            };
            SetSliderTitle(_spawnChanceItem, _config.TrafficSpawnChancePercent + "%");
            _menu.Add(_spawnChanceItem);

            _presetItem = new NativeItem("Traffic preset", "Choose Low, Realistic or Busy: normal population + additional AI cars.");
            DetectPreset();
            _presetItem.AltTitle = _presetIndex < 0 ? "CUSTOM" : PresetNames[_presetIndex];
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

        private void DetectPreset()
        {
            if (_config.AdditionalTrafficEnabled &&
                _config.MaxAdditionalTrafficVehicles == 2 &&
                _config.TrafficSpawnIntervalSeconds == 28 &&
                _config.TrafficSpawnChancePercent == 35 &&
                Math.Abs(_config.TrafficDensityMultiplier - 0.60f) < 0.01f &&
                Math.Abs(_config.ParkedVehicleDensity - 0.60f) < 0.01f &&
                Math.Abs(_config.PedDensityMultiplier - 0.65f) < 0.01f)
                _presetIndex = 0;
            else if (_config.AdditionalTrafficEnabled &&
                _config.MaxAdditionalTrafficVehicles == 4 &&
                _config.TrafficSpawnIntervalSeconds == 14 &&
                _config.TrafficSpawnChancePercent == 65 &&
                Math.Abs(_config.TrafficDensityMultiplier - 0.85f) < 0.01f &&
                Math.Abs(_config.ParkedVehicleDensity - 0.80f) < 0.01f &&
                Math.Abs(_config.PedDensityMultiplier - 0.85f) < 0.01f)
                _presetIndex = 1;
            else if (_config.AdditionalTrafficEnabled &&
                _config.MaxAdditionalTrafficVehicles == 7 &&
                _config.TrafficSpawnIntervalSeconds == 8 &&
                _config.TrafficSpawnChancePercent == 80 &&
                Math.Abs(_config.TrafficDensityMultiplier - 1.0f) < 0.01f &&
                Math.Abs(_config.ParkedVehicleDensity - 1.0f) < 0.01f &&
                Math.Abs(_config.PedDensityMultiplier - 1.0f) < 0.01f)
                _presetIndex = 2;
            else
                _presetIndex = -1;
        }

        private void NextPreset()
        {
            _presetIndex = _presetIndex < 0 ? 0 : (_presetIndex + 1) % PresetNames.Length;
            _updatingControls = true;

            if (_presetIndex == 0)
            {
                _config.TrafficDensityMultiplier = 0.60f;
                _config.ParkedVehicleDensity = 0.60f;
                _config.PedDensityMultiplier = 0.65f;
                _config.ScenarioPedDensityMultiplier = 0.65f;
                _config.MaxAdditionalTrafficVehicles = 2;
                _config.TrafficSpawnIntervalSeconds = 28;
                _config.TrafficSpawnChancePercent = 35;
            }
            else if (_presetIndex == 1)
            {
                _config.TrafficDensityMultiplier = 0.85f;
                _config.ParkedVehicleDensity = 0.80f;
                _config.PedDensityMultiplier = 0.85f;
                _config.ScenarioPedDensityMultiplier = 0.85f;
                _config.MaxAdditionalTrafficVehicles = 4;
                _config.TrafficSpawnIntervalSeconds = 14;
                _config.TrafficSpawnChancePercent = 65;
            }
            else
            {
                _config.TrafficDensityMultiplier = 1.0f;
                _config.ParkedVehicleDensity = 1.0f;
                _config.PedDensityMultiplier = 1.0f;
                _config.ScenarioPedDensityMultiplier = 1.0f;
                _config.MaxAdditionalTrafficVehicles = 7;
                _config.TrafficSpawnIntervalSeconds = 8;
                _config.TrafficSpawnChancePercent = 80;
            }

            _movingTrafficItem.Value = (int)Math.Round(_config.TrafficDensityMultiplier * 100f);
            _parkedTrafficItem.Value = (int)Math.Round(_config.ParkedVehicleDensity * 100f);
            _pedDensityItem.Value = (int)Math.Round(_config.PedDensityMultiplier * 100f);
            _maxAdditionalItem.Value = _config.MaxAdditionalTrafficVehicles;
            _spawnIntervalItem.Value = _config.TrafficSpawnIntervalSeconds;
            _spawnChanceItem.Value = _config.TrafficSpawnChancePercent;
            SetSliderTitle(_movingTrafficItem, _movingTrafficItem.Value + "%");
            SetSliderTitle(_parkedTrafficItem, _parkedTrafficItem.Value + "%");
            SetSliderTitle(_pedDensityItem, _pedDensityItem.Value + "%");
            SetSliderTitle(_maxAdditionalItem, _maxAdditionalItem.Value.ToString());
            SetSliderTitle(_spawnIntervalItem, _spawnIntervalItem.Value + "s");
            SetSliderTitle(_spawnChanceItem, _spawnChanceItem.Value + "%");
            _presetItem.AltTitle = PresetNames[_presetIndex];

            _updatingControls = false;
            Save();
        }

        private void MarkCustomPreset()
        {
            _presetItem.AltTitle = "CUSTOM";
            _presetIndex = -1;
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
