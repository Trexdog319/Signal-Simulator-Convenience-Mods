"""Builds download/SignalSimulatorMods-FullInstall.zip (BepInEx + all mods) from bin/ (run build.sh first).

Players extract the zip straight into the game folder.
"""
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BIN = ROOT / "bin"
OUT = ROOT / "download" / "SignalSimulatorMods-FullInstall.zip"
BEPINEX_ZIP = ROOT / "third_party" / "BepInEx_win_x64_5.4.23.2.zip"
MODS = ["FluidMovement", "RandomSecretSignals", "PerformanceTweaks", "SignalPlayback"]


def main():
    entries = []
    # BepInEx as shipped, minus its changelog (it would sit loose in the game folder).
    with zipfile.ZipFile(BEPINEX_ZIP) as bz:
        entries += [(i.filename, bz.read(i)) for i in bz.infolist() if not i.is_dir() and i.filename != "changelog.txt"]
    for mod in MODS:
        dll = BIN / f"SignalSim.{mod}.dll"
        if not dll.is_file():
            sys.exit(f"package: missing {dll} - run build.sh first")
        entries.append((f"BepInEx/plugins/{mod}/{dll.name}", dll.read_bytes()))
    entries.append(("INSTALL.txt", (ROOT / "packaging" / "INSTALL.txt").read_bytes()))

    OUT.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as z:
        for name, data in entries:
            z.writestr(name, data)
    print(f"wrote {OUT.relative_to(ROOT)} ({OUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
