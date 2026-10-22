#!/bin/bash
# Build dist/: the CurseForge import zip and the server pack zip.
set -e
cd "$(dirname "$0")/.."
export PATH="$HOME/go/bin:$PATH"
VERSION=$(grep -m1 '^version' pack.toml | cut -d'"' -f2)
mkdir -p dist
packwiz refresh
packwiz cf export -o "dist/CreateWarfare-$VERSION.zip"
python -I - "$VERSION" <<'PY'
import json, os, sys, zipfile
version = sys.argv[1]

# CurseForge reads minecraft.recommendedRam (MB) from the manifest; packwiz does not write it.
client = f"dist/CreateWarfare-{version}.zip"
with zipfile.ZipFile(client) as z:
    entries = {n: z.read(n) for n in z.namelist()}
manifest = json.loads(entries["manifest.json"])
manifest["minecraft"]["recommendedRam"] = 8192
entries["manifest.json"] = json.dumps(manifest, indent=2).encode()
with zipfile.ZipFile(client, "w", zipfile.ZIP_DEFLATED) as z:
    for n, data in entries.items():
        z.writestr(n, data)

out = f"dist/CreateWarfare-Server-{version}.zip"
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for f in os.listdir("server-files"):
        z.write(os.path.join("server-files", f), f)
    for f in ("pack.toml", "index.toml", "tools/fetch_mods.py"):
        z.write(f, "pack/" + f)
    for top in ("mods", "config", "kubejs"):
        for dirpath, _, files in os.walk(top):
            for f in files:
                p = os.path.join(dirpath, f)
                z.write(p, "pack/" + p.replace(os.sep, "/"))
print("written", out)
PY
