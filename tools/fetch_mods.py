"""Download the pack's mod jars for a given side (server/client) into a folder.

Usage: python tools/fetch_mods.py <side> <dest_mods_dir>
CurseForge files whose authors block third-party API downloads are fetched
from Modrinth when the same file (matched by hash) exists there, else from the CurseForge CDN.
"""
import hashlib, json, os, sys, tomllib, urllib.request, urllib.error

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UA = {"User-Agent": "robocnop/create-warfare-pack (robocnop@gmail.com)"}

def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
        return r.read()

def modrinth_url(sha1):
    try:
        v = json.loads(get(f"https://api.modrinth.com/v2/version_file/{sha1}?algorithm=sha1"))
        for f in v["files"]:
            if f["hashes"].get("sha1") == sha1:
                return f["url"]
    except urllib.error.HTTPError:
        return None
    return None

def main(side, dest):
    os.makedirs(dest, exist_ok=True)
    wanted = set()
    failures = []
    for name in sorted(os.listdir(os.path.join(ROOT, "mods"))):
        meta = tomllib.load(open(os.path.join(ROOT, "mods", name), "rb"))
        if meta.get("side", "both") not in ("both", side):
            continue
        fn, dl = meta["filename"], meta["download"]
        wanted.add(fn)
        out = os.path.join(dest, fn)
        algo, expected = dl["hash-format"], dl["hash"]
        if os.path.exists(out) and hashlib.new(algo, open(out, "rb").read()).hexdigest() == expected:
            continue
        urls = []
        if dl.get("url"):
            urls.append(dl["url"])
        cf = meta.get("update", {}).get("curseforge")
        if cf:
            if algo == "sha1":
                mr = modrinth_url(expected)
                if mr:
                    urls.append(mr)
            fid = cf["file-id"]
            urls.append(f"https://mediafilez.forgecdn.net/files/{fid // 1000}/{fid % 1000}/{urllib.request.quote(fn)}")
        for u in urls:
            try:
                data = get(u)
            except Exception:
                continue
            if hashlib.new(algo, data).hexdigest() == expected:
                open(out, "wb").write(data)
                break
        else:
            failures.append(fn)
    # drop jars that are no longer part of the pack
    for fn in os.listdir(dest):
        if fn.endswith(".jar") and fn not in wanted:
            os.remove(os.path.join(dest, fn))
    print(f"{len(wanted) - len(failures)}/{len(wanted)} mods ready in {dest}")
    for f in failures:
        print("FAILED", f)
    return 1 if failures else 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
