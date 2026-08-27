# -*- mode: python ; coding: utf-8 -*-
"""PyInstaller spec — TimeTracker Pro (Windows onedir)."""

from PyInstaller.utils.hooks import collect_all, copy_metadata

block_cipher = None

datas = [
    ("dashboard.py", "."),
    ("dashboard_charts.py", "."),
    ("dashboard_data.py", "."),
    ("dashboard_details.py", "."),
    ("dashboard_filters.py", "."),
    ("dashboard_overview.py", "."),
    ("dashboard_settings.py", "."),
    ("dashboard_utils.py", "."),
    ("tracker.py", "."),
    ("app_paths.py", "."),
    ("app_settings.example.json", "."),
    (".streamlit", ".streamlit"),
]

binaries = []
hiddenimports = [
    "win32timezone",
    "win32com.client",
    "pythoncom",
    "pkg_resources",
]

for package in ("streamlit", "plotly", "pystray", "PIL"):
    pkg_datas, pkg_binaries, pkg_hidden = collect_all(package)
    datas += pkg_datas
    binaries += pkg_binaries
    hiddenimports += pkg_hidden

for package in ("streamlit", "plotly", "pandas", "numpy", "PIL", "pystray"):
    try:
        datas += copy_metadata(package)
    except Exception:
        pass

a = Analysis(
    ["main.py"],
    pathex=[],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=["hooks"],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="TimeTrackerPro",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name="TimeTrackerPro",
)
