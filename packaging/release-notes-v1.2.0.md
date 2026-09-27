## ⬇️ Download

Under **Assets** below, download **`SignalSimulatorMods-FullInstall.zip`**.
(You don't need the "Source code" files. GitHub adds those automatically.)

## Install

1. In Steam, right-click **Signal Simulator → Manage → Browse local files**.
2. Extract everything from the zip into that folder (`winhttp.dll` should sit next to `SignalSimulator.exe`).
   If you're updating from an older version, let it overwrite the old files.
3. Start the game.

**To uninstall**, delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and the `BepInEx` folder.
Your game files are never changed.

## What's new: Signal Playback

The laptop's signal database could only play a recorded signal from the start. Now it shows how long the
signal is and lets you replay any part of it.

- A seek bar with the elapsed and total time. The length shows as soon as you select a signal. Click or drag
  the bar to jump to any point, even before pressing PLAY.
- **Pause / Resume**, **skip back / forward 5 seconds**, and **Loop**.
- Keys while the database is open: Left / Right arrows to skip, P to pause.
- **Fix:** when a signal finishes, the button goes back to PLAY and the control panel sound comes back on.
  Before, it stayed on STOP and the control panel stayed muted. Picking another signal mid-playback no
  longer leaves the control panel muted either.

The skip length and keys can be changed in `BepInEx/config/signalsim.signalplayback.cfg`.

The movement, radio signal and performance mods from earlier releases are unchanged.

Includes [BepInEx 5.4.23.2](https://github.com/BepInEx/BepInEx), the mod loader these mods run on.
