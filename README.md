# Living Los Santos

A ScriptHookVDotNet 3 mod for **GTA V Legacy (single-player)**. It provides an in-game settings menu and random roadside events. Ambient traffic is managed by GTA's own population system, not by manually spawning replacement cars.

## Current features

- Seven possible broken-down vehicles: Asea, Blista, Emperor, Premier, Regina, Sadler, and Stanier.
- Rural-road preference using GTA zone names, including Grand Senora Desert, Sandy Shores, Grapeseed, Paleto, Great Chaparral, Tongva Valley, Mount Chiliad, and other countryside areas.
- Searches major road nodes and places vehicles a few metres to the side of the road, rather than in the middle of a traffic lane.
- More frequent event rolls: every 30 seconds with a 75% chance when the active-event limit allows it.
- Up to three active roadside scenes. Each vehicle remains for about four minutes.
- Configurable event spawn distances, shoulder offset, rural search attempts, and rural-area preference.
- In-game LemonUI settings menu, opened with **F10**, with live toggles, moving/parked traffic density sliders, presets, and automatic config saving.
- Frame-based native controls scale Rockstar's moving, parked and pedestrian populations.
- A separate, bounded additive traffic manager preloads optional vehicle models asynchronously, chooses valid road nodes ahead of the player, rejects visible/occupied/steep/off-road nodes, and creates a driver for each extra vehicle.
- Additional cars are spawned outside the camera view and are cleaned up only when distant and off-screen, in water, driverless, or expired. Vehicles taken by the player are no longer managed or deleted.
- File logging with fallback locations under the user's LocalAppData and Temp folders.

## Recommended traffic overhaul for this exact game build

For an actual increase in normal ambient traffic, use **Los Angeles Traffic (with DLC Cars) v2.3.1 — Legacy**:

https://www.nexusmods.com/gta5/mods/1505

The author lists support and testing for **GTA V Legacy 1.0.3889**, matching the build this installation has been using. The overhaul changes the population data consumed by Rockstar's own ambient system:

- `popcycle.dat` — regional/time-of-day population density.
- `popgroups.ymt` — vehicle and pedestrian group/model selection, including supported DLC traffic.
- `gameconfig.xml` — streaming/population capacities and other engine limits.

This is why it produces more natural traffic than a script that creates a few cars near the player. The game itself chooses when, where and which ambient vehicles appear. Living Los Santos then scales that population down when desired; a 100% setting means the full density configured by the installed game files.

**Important:** this traffic overhaul is a separate third-party mod and is not included in this ZIP. Install the **Legacy** version, not Enhanced. Follow the author's current readme and back up your files first. The installation uses OpenIV and a mods folder. The full version's listed requirements include Heap Limit Adjuster (configured to the author's recommended range), Script Hook V for DLC cars, and a trainer such as Simple Trainer for manual DLC spawning; Packfile Limit Adjuster is also recommended where needed. It may conflict with another custom `gameconfig.xml`, `popcycle.dat`, or traffic overhaul. Do not mix files from different game builds.

The mod page and author documentation:
- https://www.nexusmods.com/gta5/mods/1505
- https://www.nexusmods.com/gta5/mods/1505?tab=docs

## Scripted additive traffic and F10 controls

GTA's frame-based density natives scale the population but do not create new population slots. A multiplier of 1.0 is the normal/full configured density; lower values reduce it. The separate scripted manager adds a small number of extra NPC-driven vehicles on top of the ambient traffic. This is how we go beyond the vanilla population without pretending that a native multiplier above 1.0 is a reliable solution.

The additive manager follows the publicly described design of Legacy traffic scripts such as TrafficAddon: preload models, use road nodes outside the camera view, spawn a driver, and clean up vehicles that leave the active area. It is an independent implementation; no third-party source files or assets are included.

Presets:
- **Low:** moving 60%, parked 60%, 2 extra cars, 35% spawn chance every 28 seconds.
- **Realistic:** moving 85%, parked 80%, 4 extra cars, 65% spawn chance every 14 seconds.
- **Busy:** moving/parked/pedestrian density 100%, up to 7 extra cars, 80% spawn chance every 8 seconds.

The in-game GTA population density setting may also affect the final result. Start at the recommended setting in the external overhaul's readme, then adjust with F10.

## Requirements

- Grand Theft Auto V **Legacy** (not Enhanced)
- Script Hook V
- ScriptHookVDotNet 3 (build target: 3.7.0-nightly.206)
- .NET Framework 4.8 (for building)
- Visual Studio 2022 or another MSBuild-compatible IDE

## Build

1. Install Script Hook V and ScriptHookVDotNet 3 into the GTA V game directory.
2. Open the solution at `src/LivingLosSantos.sln` in Visual Studio.
3. Set `GtaVPath` to your GTA V installation if the default path does not match your setup.
4. Build the Release configuration.
5. Copy `LivingLosSantos.dll`, `LemonUI.SHVDN3.dll`, and `config.ini` into the GTA V scripts folder.

The GitHub Actions workflow builds against the pinned ScriptHookVDotNet 3.7.0-nightly.206 API, downloads LemonUI SHVDN3 2.2.0, and packages both runtime DLLs with config.ini and third-party notices.

## Installation

Place these files directly in the game's scripts folder:

    GTA V Legacy/
      scripts/
        LivingLosSantos.dll
        LemonUI.SHVDN3.dll
        config.ini

Do not put the DLL in a separate LivingLosSantos subfolder. ScriptHookVDotNet loads assemblies from the scripts directory.

## Event configuration

The packaged `config.ini` uses these defaults:

- Event selection check: every 30 seconds
- Chance per eligible check: 75%
- Maximum active roadside scenes: 3
- Vehicle search distance: 150–300 metres from the player
- Roadside offset: 2–6 metres beyond the detected road edge
- Rural-zone preference: 90% when both rural and non-rural road candidates are available
- Rural road search samples: 18 directions/positions

The event roll is probabilistic, so a roadside scene is not guaranteed at each check.

## Current limitations

This is an early milestone, not a full city-life simulation. Pedestrian rescue/repair behavior, mechanics, emergency response, collision scenes, venue visits, and scheduled NPC routines are not implemented yet. Extra traffic uses a configurable vehicle model pool. Add-on vehicle names work only when those models are already correctly installed in the game; unavailable models are skipped. The exact visual behavior and performance still need in-game testing.

## Performance and cleanup

- Density natives and menu processing run every frame; vehicle scanning/spawning and event selection run on a bounded timer.
- Additional traffic is capped at 10 vehicles, models are preloaded asynchronously one at a time, and each car must pass road-node, camera visibility, nearby-vehicle and slope checks.
- Only a limited number of roadside event entities can be active simultaneously.
- Event vehicles and pedestrians are removed after their lifetime or when the script is aborted.
- Logs are written to the game scripts folder when writable, with LocalAppData/Temp fallbacks.

## License

MIT. See [LICENSE](LICENSE).
