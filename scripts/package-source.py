#!/usr/bin/env python3
"""Create a reproducible source archive without build caches or user settings."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parents[1]
excluded = {"work", "outputs", ".git", ".build", "bin", "obj", "__pycache__"}
out = root / "outputs" / "LGInputSwitch-source.zip"
out.parent.mkdir(exist_ok=True)
with ZipFile(out, "w", compression=ZIP_DEFLATED) as archive:
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root)
        if not path.is_file() or any(part in excluded for part in relative.parts) or path.name == ".DS_Store":
            continue
        archive.write(path, str(Path("LGInputSwitch") / relative))
print(out)
