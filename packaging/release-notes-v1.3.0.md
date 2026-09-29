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

## What's new: Save Files

Keep several separate games. On the main menu, **SAVE: ...** (under NEW GAME) opens the save list:

- **Select** a save, then press **CONTINUE** to play it.
- **+ New save file** makes an empty one. Select it and press **NEW GAME** to start a separate game.
- **Rename** or **Delete** any save. Deleting asks you to confirm first.

Saves never affect each other, and each one keeps its own difficulty. Your existing save becomes "Save 1" and
stays in the game's normal save file, so it still works if you remove the mod.

## Changed: Performance Tweaks start off

The performance tweaks no longer turn on by themselves. Press **F9** in-game to turn them on (and again to
turn them off). To have them on every time the game starts, set `StartOn = true` in
`BepInEx/config/signalsim.performancetweaks.cfg`.

The movement, radio signal and signal playback mods are unchanged.

Includes [BepInEx 5.4.23.2](https://github.com/BepInEx/BepInEx), the mod loader these mods run on.
