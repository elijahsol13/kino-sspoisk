"""Build the manual ZIP and native macOS DMG from the seven extension assets.

The Windows EXE is compiled on Windows by the GitHub Actions workflow.
"""
from __future__ import annotations

import shutil
import subprocess
import sys
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
DIST = ROOT / "dist"
ASSETS = ("manifest.json", "icon.png", "links.js", "popup.html", "popup.js", "content.js", "content.css")


def build_manual_zip() -> Path:
    archive = DIST / "Kino-SSpoisk-manual.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for name in ASSETS:
            z.write(ROOT / name, "Kino-SSpoisk/" + name)
        z.writestr("КАК_УСТАНОВИТЬ.txt", (ROOT / "MANUAL_INSTALL.txt").read_text(encoding="utf-8"))
    return archive


def build_mac() -> Path | None:
    if sys.platform != "darwin":
        return None
    app = DIST / "Установить Kino-SSpoisk.app"
    if app.exists():
        shutil.rmtree(app)
    executable = app / "Contents" / "MacOS" / "KinoSSpoiskInstaller"
    executable.parent.mkdir(parents=True)
    subprocess.run(["/usr/bin/clang", "-O2", "-arch", "arm64", "-arch", "x86_64", "-mmacosx-version-min=12.0", "-framework", "Cocoa", str(ROOT / "installer" / "macos.m"), "-o", str(executable)], check=True)
    plist = app / "Contents" / "Info.plist"
    plist.write_text("""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>Установить Kino-SSpoisk</string>
<key>CFBundleDisplayName</key><string>Установить Kino-SSpoisk</string>
<key>CFBundleExecutable</key><string>KinoSSpoiskInstaller</string>
<key>CFBundleIdentifier</key><string>app.kinosspoisk.installer</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1.0</string>
<key>CFBundleVersion</key><string>1</string>
<key>LSMinimumSystemVersion</key><string>12.0</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
""", encoding="utf-8")
    resources = app / "Contents" / "Resources" / "extension"
    resources.mkdir(parents=True)
    for asset in ASSETS:
        shutil.copy2(ROOT / asset, resources / asset)
    # Ad-hoc signing protects the local bundle structure; does not notarize it.
    subprocess.run(["/usr/bin/codesign", "--force", "--deep", "--sign", "-", str(app)], check=True)
    staging = DIST / ".dmg-staging"
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir()
    shutil.copytree(app, staging / app.name, symlinks=True)
    image = DIST / "Kino-SSpoisk-macOS.dmg"
    if image.exists():
        image.unlink()
    try:
        subprocess.run(["/usr/bin/hdiutil", "create", "-volname", "Kino SSpoisk", "-srcfolder", str(staging), "-format", "UDZO", "-ov", str(image)], check=True)
    finally:
        shutil.rmtree(staging)
    return image


if __name__ == "__main__":
    DIST.mkdir(exist_ok=True)
    manual = build_manual_zip()
    print("Manual ZIP:", manual, manual.stat().st_size, "bytes")
    mac = build_mac()
    if mac:
        print("macOS DMG:", mac, mac.stat().st_size, "bytes")
