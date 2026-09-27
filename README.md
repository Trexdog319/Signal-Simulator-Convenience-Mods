# Signal Simulator Convenience Mods

A set of improvements I would've liked to have in Signal Simulator: better player and golf kart movement,
control over how often special radio signals show up, and performance tweaks for lower-end PCs.

## Install

1. **[Download the mods](https://github.com/Trexdog319/Signal-Simulator-Convenience-Mods/releases/latest/download/SignalSimulatorMods-FullInstall.zip)** (or get `SignalSimulatorMods-FullInstall.zip` from the [Releases page](https://github.com/Trexdog319/Signal-Simulator-Convenience-Mods/releases/latest))
2. In Steam, right-click **Signal Simulator → Manage → Browse local files**.
3. Extract everything from the zip into that folder (`winhttp.dll` should sit next to `SignalSimulator.exe`).
4. Start the game. The new settings are under **Options → Gameplay**.

The download includes [BepInEx](https://github.com/BepInEx/BepInEx), the mod loader these mods run on.
If you already have BepInEx 5, just take the `BepInEx/plugins` folder from the zip.

## What's included

**Movement**
| Key | Action |
|---|---|
| Space | Jump. Hold it to bunny hop and keep your speed. |
| Left Ctrl or C | Crouch |

- You can steer mid-air, including diagonally.
- Camera sway: a gentle walk sway and a lean when strafing. You can turn it off.
- Golf kart: smoother acceleration and steering, and it no longer loses all its speed on bumps. You can turn
  it off.

**Radio signals**
- **Random Event Signals**: secret event signals (UFO, Borg, Tripod, SpaceX and others) can show up at
  random, instead of only on specific real-world dates and times. Turn this off to get the original
  schedule back.
- **Radio Event Chance**: how often that happens (default 3%).
- **Radio Story Chance**: how often story signals show up (default 10%, same as the base game).

**Performance**

Makes the game run better on lower-end PCs by cutting graphics work that is hard to see, and fixes a few
settings that didn't do what they said.

| Key | Action |
|---|---|
| F9 | Turn all the performance tweaks on or off, to compare |
| F10 | Show an FPS counter |

- Shadows: cheaper soft shadows, a shorter shadow distance, and no shadows from lamps. The in-game
  **Shadows: Off** setting now really turns shadows off.
- Weather: cheaper clouds, fog and reflections.
- The monitor screens in the base refresh 10 times a second instead of every frame.
- The colorblind filter no longer runs when it is set to Normal.
- Grass, trees and terrain are drawn in less detail further away. Inside the base, objects keep full
  detail so nothing pops in.
- The game drops to 10 FPS while you're alt-tabbed out.

Everything can be adjusted or turned off in `BepInEx/config/signalsim.performancetweaks.cfg`. The in-game
graphics options still work, and these tweaks apply on top of them.

## Settings

The movement and radio options are in-game under **Options → Gameplay**. For more detail (key bindings,
jump height, crouch speed, performance settings and so on), edit the files in `BepInEx/config/` inside the
game folder.

## Uninstall

Delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and the `BepInEx` folder from the game
folder. The mods never change the game's own files.

To turn the mods off without uninstalling, set `enabled = false` in `doorstop_config.ini`.

## Building from source

For developers only. You need the game with BepInEx installed, Git Bash and Python.

```bash
./build.sh
```

```bash
python packaging/package.py
```

`build.sh` compiles the mods and installs them into your game. `package.py` then builds
`download/SignalSimulatorMods-FullInstall.zip`, which is the file to attach to a new GitHub release. The source
is in `FluidMovement/`, `RandomSecretSignals/`, `PerformanceTweaks/` and `Shared/`.
