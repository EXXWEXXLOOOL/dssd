# Living Los Santos

A ScriptHookVDotNet 3 mod for **GTA V Legacy (single-player)** that makes Los Santos feel more active through ambient NPC behavior, traffic variety, and configurable city events.

## Current status

The repository is being bootstrapped. The first milestone is a stable, configurable runtime foundation and a small set of ambient city events. More advanced NPC schedules, venue visits, and persistent world simulation will be added incrementally.

## Requirements

- Grand Theft Auto V **Legacy** (not Enhanced)
- Script Hook V
- ScriptHookVDotNet 3
- .NET Framework 4.8 developer pack (for building)
- Visual Studio 2022 or another MSBuild-compatible IDE

## Build

1. Install Script Hook V and ScriptHookVDotNet 3 into the GTA V game directory.
2. Open `src/LivingLosSantos.sln` in Visual Studio.
3. Set the path to your GTA V installation in the project reference properties if the default path does not match your setup.
4. Build the `Release` configuration.
5. Copy `LivingLosSantos.dll` and the `config.ini` file into `GTA V/scripts/LivingLosSantos/` (create the folder if needed).

The project references ScriptHookVDotNet assemblies from the game installation's `scripts` directory. They are not redistributed in this repository.

## Installation

Copy the built DLL to:

```
GTA V/scripts/LivingLosSantos/LivingLosSantos.dll
```

Copy `config.ini` beside it. ScriptHookVDotNet loads the script assembly from the scripts directory; if your setup requires a flat scripts folder, place the DLL directly in `GTA V/scripts/` and keep the configuration in `GTA V/scripts/LivingLosSantos/`.

## Planned systems

- **Core:** lifecycle, logging, configuration, safe tick scheduling
- **Living city:** time-of-day activity profiles, ambient population/traffic tuning, lightweight NPC routines
- **City events:** weighted event selection, cooldowns, distance checks, cleanup and event lifecycle
- **Debug:** diagnostics and configurable event testing

## Performance and safety principles

- Never continuously scan the entire world on every game tick.
- Use bounded update intervals and spawn limits.
- Do not spawn events close to the player or during missions by default.
- Clean up entities created by the mod when an event ends or the script is aborted.
- Keep all gameplay-affecting features configurable.

## License

MIT. See [LICENSE](LICENSE).
