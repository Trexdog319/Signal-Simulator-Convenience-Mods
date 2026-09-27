## ⬇️ Download

Under **Assets** below, download **`SignalSimulatorMods-FullInstall.zip`**.
(You don't need the "Source code" files. GitHub adds those automatically.)

## Install

1. In Steam, right-click **Signal Simulator → Manage → Browse local files**.
2. Extract everything from the zip into that folder (`winhttp.dll` should sit next to `SignalSimulator.exe`).
   If you're updating from v1.0.0, let it overwrite the old files.
3. Start the game.

**To uninstall**, delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and the `BepInEx` folder.
Your game files are never changed.

## What's new: Performance Tweaks

Makes the game run better on lower-end PCs by cutting graphics work that is hard to see.

- **F9** turns all the tweaks on or off so you can compare. **F10** shows an FPS counter.
- Shadows: cheaper soft shadows, a shorter shadow distance, and no shadows from lamps.
- **Fix:** the in-game **Shadows: Off** setting now really turns shadows off (before, shadows were still
  being rendered).
- Weather: cheaper clouds, fog and reflections.
- The monitor screens in the base refresh 10 times a second instead of every frame.
- The colorblind filter no longer runs when it is set to Normal.
- Grass, trees and terrain are drawn in less detail further away. Inside the base, objects keep full detail
  so nothing pops in.
- The game drops to 10 FPS while you're alt-tabbed out.

Everything can be adjusted or turned off in `BepInEx/config/signalsim.performancetweaks.cfg`, which is
created the first time you start the game. The in-game graphics options still work, and these tweaks apply
on top of them.

The movement and radio signal mods from v1.0.0 are unchanged.

Includes [BepInEx 5.4.23.2](https://github.com/BepInEx/BepInEx), the mod loader these mods run on.
