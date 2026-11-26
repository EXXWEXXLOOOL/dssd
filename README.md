# Living Los Santos

A ScriptHookVDotNet 3 mod for **GTA V Legacy (single-player)**. The current milestone adds an in-game settings menu, extra NPC-driven traffic, and random roadside events.

## Current features

- Seven possible broken-down vehicles: Asea, Blista, Emperor, Premier, Regina, Sadler, and Stanier.
- Rural-road preference using GTA zone names, including Grand Senora Desert, Sandy Shores, Grapeseed, Paleto, Great Chaparral, Tongva Valley, Mount Chiliad, and other countryside areas.
- Searches major road nodes and places vehicles a few metres to the side of the road, rather than in the middle of a traffic lane.
- More frequent event rolls: every 30 seconds with a 75% chance when the active-event limit allows it.
- Up to three active breakdown scenes. Each vehicle remains for about four minutes.
- Configurable spawn distances, shoulder offset, rural search attempts, and rural-area preference.
- In-game LemonUI settings menu, opened with **F10**, with live toggles, traffic sliders, presets, and automatic config saving.
- Additional AI-driven traffic: spawned road vehicles receive a wander-driving task and are cleaned up when they leave the area or expire.
- Traffic presets: Low, Realistic, and Busy, with a configurable cap of up to 10 extra vehicles.
- Vanilla traffic and parked-vehicle density controls. The GTA native multiplier is capped at 1.0 (normal density); extra AI vehicles provide the increase above normal.
- File logging with fallback locations under the user's LocalAppData and Temp folders.

Rural locations are preferred when a suitable rural road node is found within the event search radius. When the player is deep inside the city and no rural road is nearby, the system falls back to a suitable main road in the current area.

## Requirements

- Grand Theft Auto V **Legacy** (not Enhanced)
- Script Hook V
- ScriptHookVDotNet 3 (tested build target: 3.7.0-nightly.206)
- .NET Framework 4.8 (for building)
- Visual Studio 2022 or another MSBuild-compatible IDE

## Build

1. Install Script Hook V and ScriptHookVDotNet 3 into the GTA V game directory.
2. Open the solution at src/LivingLosSantos.sln in Visual Studio.
3. Set GtaVPath to your GTA V installation if the default path does not match your setup.
4. Build the Release configuration.
5. Copy LivingLosSantos.dll and config.ini into the GTA V scripts folder.

The GitHub Actions workflow builds against the pinned ScriptHookVDotNet 3.7.0-nightly.206 API, downloads LemonUI SHVDN3 2.2.0, and packages both runtime DLLs with config.ini and third-party notices.

## Installation

Place both files directly in the game's scripts folder:

    GTA V Legacy/
      scripts/
        LivingLosSantos.dll
        LemonUI.SHVDN3.dll
        config.ini

Do not put the DLL in a separate LivingLosSantos subfolder. ScriptHookVDotNet loads assemblies from the scripts directory.

## Configuration

The packaged config.ini uses these defaults:

- Event selection check: every 30 seconds
- Chance per eligible check: 75%
- Maximum active breakdown scenes: 3
- Vehicle search distance: 150–360 metres from the player
- Roadside offset: 5.5–8.5 metres from the road node
- Rural-zone preference: 90% when both rural and non-rural road candidates are available
- Rural road search samples: 18 directions/positions

The event roll is probabilistic, so a breakdown is not guaranteed at each check. In dense urban areas, the system may use a nearby city road if no countryside road node is available within range.

## Current limitations

This is an early milestone, not a full city-life simulation. Pedestrian rescue/repair behavior, mechanics, emergency response, collision scenes, venue visits, scheduled NPC routines, and GTA Online vehicle traffic are not implemented yet. Actual in-game spawn locations and timing still need player testing.

## Performance and cleanup

- Event selection and traffic spawning run on a bounded timer; only the frame-based GTA density natives and menu processing run every frame.
- Additional AI traffic is capped and uses road-node placement, a no-nearby-vehicle check, and a four-minute lifetime.
- Only a limited number of event vehicles can be active simultaneously.
- Vehicle models are requested only when an event is about to start.
- Event vehicles are removed after their lifetime or when the script is aborted.
- Logs are written to the game scripts folder when writable, with LocalAppData/Temp fallbacks.

## License

MIT. See [LICENSE](LICENSE).
