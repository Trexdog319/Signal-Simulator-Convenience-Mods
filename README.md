# Signal Simulator Convenience Mods

A set of improvements I would've liked to have in Signal Simulator: better player and golf kart movement,
and control over how often special radio signals show up.

## Install

1. **[Download the mods](https://github.com/Trexdog319/Signal-Simulator-Convenience-Mods/raw/main/download/SignalSimulatorMods-FullInstall.zip)**
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

## Settings

All the main options are in-game under **Options → Gameplay**. For more detail (key bindings, jump height,
crouch speed and so on), edit the files in `BepInEx/config/` inside the game folder.

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

`build.sh` compiles the mods and installs them into your game. `package.py` then rebuilds the download zip in
`download/`. The source is in `FluidMovement/`, `RandomSecretSignals/` and `Shared/`.
