"""Check installer payload and bundle integrity without installing anything."""
from __future__ import annotations

import plistlib
import subprocess
import sys
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DIST = ROOT / 'dist'
ASSETS = ('manifest.json', 'icon.png', 'links.js', 'popup.html', 'popup.js', 'content.js', 'content.css')


class InstallerPackagingTests(unittest.TestCase):
    def test_manual_zip_contains_current_extension(self):
        with zipfile.ZipFile(DIST / 'Kino-SSpoisk-manual.zip') as archive:
            self.assertIn('КАК_УСТАНОВИТЬ.txt', archive.namelist())
            self.assertEqual(sorted(n for n in archive.namelist() if n.startswith('Kino-SSpoisk/')), sorted('Kino-SSpoisk/' + n for n in ASSETS))
            for asset in ASSETS:
                self.assertEqual(archive.read('Kino-SSpoisk/' + asset), (ROOT / asset).read_bytes())

    @unittest.skipUnless(sys.platform == 'darwin', 'macOS bundle built on Mac only')
    def test_mac_universal_app_contains_current_extension(self):
        app = DIST / 'Установить Kino-SSpoisk.app'
        binary = app / 'Contents/MacOS/KinoSSpoiskInstaller'
        archs = subprocess.check_output(['/usr/bin/lipo', '-archs', str(binary)], text=True).split()
        self.assertEqual(set(archs), {'arm64', 'x86_64'})
        subprocess.run(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(app)], check=True)
        with (app / 'Contents/Info.plist').open('rb') as file:
            self.assertEqual(plistlib.load(file)['CFBundlePackageType'], 'APPL')
        for asset in ASSETS:
            self.assertEqual((app / 'Contents/Resources/extension' / asset).read_bytes(), (ROOT / asset).read_bytes())
        image = DIST / 'Kino-SSpoisk-macOS.dmg'
        subprocess.run(['/usr/bin/hdiutil', 'verify', str(image)], check=True, stdout=subprocess.DEVNULL)


if __name__ == '__main__':
    unittest.main()
