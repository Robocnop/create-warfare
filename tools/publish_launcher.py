"""Generate dist/launcher/version.json for the Create Warfare launcher, and optionally publish it.

Usage:
  python tools/publish_launcher.py            # writes dist/launcher/version.json (URLs of the public repo)
  python tools/publish_launcher.py --local    # same, but zip/exe URLs are local paths (test with CW_LAUNCHER_MANIFEST)
  python tools/publish_launcher.py --upload   # also creates the GitHub releases and pushes version.json

Run tools/build.sh first (dist/CreateWarfare-<v>.zip). The launcher exe is picked up from
dist/launcher/CreateWarfareLauncher.exe if present (dotnet publish, see launcher/README.md).

Jars are not redistributed: each mod gets its Modrinth URL (matched by sha1) and its CurseForge CDN URL,
the launcher downloads them itself and checks the sha1, exactly like tools/fetch_mods.py.
"""
import argparse, hashlib, json, os, re, shutil, subprocess, sys, tomllib, urllib.parse, urllib.request, zipfile
from concurrent.futures import ThreadPoolExecutor

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_servers_dat import SERVERS  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UA = {"User-Agent": "robocnop/create-warfare-pack (robocnop@gmail.com)"}
DIST_REPO = "Robocnop/create-warfare"
DIST_BRANCH = "master"
OUT = os.path.join(ROOT, "dist", "launcher")

# Never overwritten nor deleted by the launcher, only created when missing.
PROTECTED = [
    "options.txt", "optionsof.txt", "optionsshaders.txt", "servers.dat", "servers.dat_old",
    "saves/", "screenshots/", "shaderpacks/", "resourcepacks/", "schematics/", "local/",
    "xaero/", "XaeroWaypoints/", "journeymap/", "Distant_Horizons_server_data/",
    # Inventory Profiles Next stores the player's hotkeys in its main config file.
    "config/inventoryprofilesnext/",
]


def sha(path, algo):
    h = hashlib.new(algo)
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def request(url, data=None, method=None):
    headers = dict(UA)
    if data is not None:
        headers["Content-Type"] = "application/json"
    return urllib.request.Request(url, data=data, headers=headers, method=method)


def cdn_url(file_id, filename):
    return f"https://mediafilez.forgecdn.net/files/{file_id // 1000}/{file_id % 1000}/{urllib.parse.quote(filename)}"


def head_size(url):
    try:
        with urllib.request.urlopen(request(url, method="HEAD"), timeout=60) as r:
            return int(r.headers.get("Content-Length") or 0)
    except Exception:
        return 0


def modrinth_files(sha1s):
    """sha1 -> (url, size) for every hash Modrinth knows, in one bulk request."""
    body = json.dumps({"hashes": sorted(sha1s), "algorithm": "sha1"}).encode()
    with urllib.request.urlopen(request("https://api.modrinth.com/v2/version_files", body), timeout=120) as r:
        versions = json.load(r)
    found = {}
    for h, v in versions.items():
        for f in v["files"]:
            if f["hashes"].get("sha1") == h:
                found[h] = (f["url"], f["size"])
    return found


def git(*args):
    return subprocess.run(["git", "-C", ROOT, *args], check=True, capture_output=True, text=True, encoding="utf-8").stdout


def retired_mods(current):
    """(filename, sha1) of client jars shipped by any earlier tag or dist zip but no longer in the pack."""
    seen = set()
    for tag in git("tag", "--list", "v*").split():
        for path in git("ls-tree", "--name-only", tag, "mods/").split("\n"):
            if not path.endswith(".pw.toml"):
                continue
            meta = tomllib.loads(git("show", f"{tag}:{path}"))
            dl = meta.get("download", {})
            if meta.get("side", "both") != "server" and dl.get("hash-format") == "sha1":
                seen.add((meta["filename"], dl["hash"]))
    dist = os.path.join(ROOT, "dist")
    for name in os.listdir(dist):
        if re.fullmatch(r"CreateWarfare-[\d.]+\.zip", name):
            with zipfile.ZipFile(os.path.join(dist, name)) as z:
                for n in z.namelist():
                    if n.startswith("overrides/mods/") and n.endswith(".jar"):
                        seen.add((n.rsplit("/", 1)[1], hashlib.sha1(z.read(n)).hexdigest()))
    return [{"file": f, "sha1": h} for f, h in sorted(seen) if (f, h) not in current]


def changelog(versions=3):
    """The latest sections of CHANGELOG.md, as plain text."""
    text = open(os.path.join(ROOT, "CHANGELOG.md"), encoding="utf-8").read()
    parts = re.split(r"(?m)^## ", text)[1:1 + versions]
    out = []
    for p in parts:
        p = re.sub(r"(?m)^### ", "", p)
        p = re.sub(r"(?m)^> ?", "", p)
        p = re.sub(r"\n {2,}(?=\S)", " ", p)  # Markdown continuation lines
        p = re.sub(r"(?<=[^\n])\n(?=[^\n\-])", " ", p)  # hard-wrapped paragraphs
        p = re.sub(r"\*\*(.+?)\*\*", r"\1", p)
        p = re.sub(r"`([^`]+)`", r"\1", p)
        out.append(p.strip())
    return "\n\n".join(out)


def launcher_version():
    csproj = os.path.join(ROOT, "launcher", "src", "CreateWarfare.Launcher", "CreateWarfare.Launcher.csproj")
    return re.search(r"<Version>([^<]+)</Version>", open(csproj, encoding="utf-8").read()).group(1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--local", action="store_true", help="zip/exe URLs are local paths (testing)")
    ap.add_argument("--upload", action="store_true", help=f"create the releases on {DIST_REPO} and push version.json")
    ap.add_argument("--notice", default=None, help="announcement shown on the launcher home screen")
    ap.add_argument("--min-launcher", default="1.0.0")
    args = ap.parse_args()

    pack = tomllib.load(open(os.path.join(ROOT, "pack.toml"), "rb"))
    version = pack["version"]
    zip_path = os.path.join(ROOT, "dist", f"CreateWarfare-{version}.zip")
    if not os.path.exists(zip_path):
        sys.exit(f"{zip_path} missing: run tools/build.sh first")
    with zipfile.ZipFile(zip_path) as z:
        bundled = {n.rsplit("/", 1)[1] for n in z.namelist() if n.startswith("overrides/mods/") and n.endswith(".jar")}

    mods, missing = [], []
    for name in sorted(os.listdir(os.path.join(ROOT, "mods"))):
        meta = tomllib.load(open(os.path.join(ROOT, "mods", name), "rb"))
        if meta.get("side", "both") == "server" or meta["filename"] in bundled:
            continue
        dl = meta["download"]
        if dl.get("hash-format") != "sha1":
            missing.append(f"{meta['filename']} ({dl.get('hash-format')}, not in overrides/mods)")
            continue
        cf = meta.get("update", {}).get("curseforge")
        mods.append({"file": meta["filename"], "sha1": dl["hash"], "size": 0,
                     "urls": [u for u in [dl.get("url"), cdn_url(cf["file-id"], meta["filename"]) if cf else None] if u]})
    if missing:
        sys.exit("Cannot verify these jars with sha1:\n  " + "\n  ".join(missing))

    mr = modrinth_files({m["sha1"] for m in mods})
    for m in mods:
        if m["sha1"] in mr:
            url, size = mr[m["sha1"]]
            m["urls"].insert(0, url)
            m["size"] = size
    with ThreadPoolExecutor(16) as pool:
        for m, size in zip(mods, pool.map(lambda m: 0 if m["size"] else head_size(m["urls"][-1]), mods)):
            m["size"] = m["size"] or size
    print(f"{len(mods)} jars, {len(mr)} found on Modrinth, {sum(1 for m in mods if not m['size'])} without size")

    base = f"https://github.com/{DIST_REPO}/releases/download"
    info = {
        "pack": pack["name"],
        "version": version,
        "minecraft": pack["versions"]["minecraft"],
        "neoforge": pack["versions"]["neoforge"],
        "minLauncherVersion": args.min_launcher,
        "launcher": None,
        "zip": {"url": zip_path if args.local else f"{base}/v{version}/CreateWarfare-{version}.zip",
                "sha256": sha(zip_path, "sha256"), "size": os.path.getsize(zip_path)},
        "mods": mods,
        "retiredMods": retired_mods({(m["file"], m["sha1"]) for m in mods}),
        "protected": PROTECTED,
        "servers": SERVERS,
        "notice": args.notice,
        "changelog": changelog(),
    }
    exe = os.path.join(OUT, "CreateWarfareLauncher.exe")
    if os.path.exists(exe):
        lv = launcher_version()
        info["launcher"] = {"version": lv, "url": exe if args.local else f"{base}/launcher-v{lv}/CreateWarfareLauncher.exe",
                            "sha256": sha(exe, "sha256"), "size": os.path.getsize(exe)}

    os.makedirs(OUT, exist_ok=True)
    out = os.path.join(OUT, "version.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(info, f, ensure_ascii=False, indent=2)
    print(f"written {out} (pack {version}, {len(info['retiredMods'])} retired jars, launcher {info['launcher'] and info['launcher']['version']})")

    if args.upload:
        upload(info, zip_path, exe, out)


def upload(info, zip_path, exe, version_json):
    def gh(*a, check=True):
        return subprocess.run(["gh", *a], check=check, capture_output=True, text=True)

    v = info["version"]
    if gh("release", "view", f"v{v}", "--repo", DIST_REPO, check=False).returncode != 0:
        gh("release", "create", f"v{v}", zip_path, "--repo", DIST_REPO, "--title", f"Create Warfare {v}", "--notes", info["changelog"][:20000])
    assets = gh("release", "view", f"v{v}", "--repo", DIST_REPO, "--json", "assets", "--jq", ".assets[].name").stdout.split()
    if os.path.basename(zip_path) not in assets:
        gh("release", "upload", f"v{v}", zip_path, "--repo", DIST_REPO)
    if info["launcher"]:
        lv = info["launcher"]["version"]
        if gh("release", "view", f"launcher-v{lv}", "--repo", DIST_REPO, check=False).returncode != 0:
            gh("release", "create", f"launcher-v{lv}", exe, "--repo", DIST_REPO, "--title", f"Launcher {lv}",
               "--notes", "Télécharge CreateWarfareLauncher.exe et lance-le.")
    # version.json is committed at the repo root (excluded from the pack by .packwizignore):
    # the launchers read https://raw.githubusercontent.com/<repo>/<branch>/version.json
    shutil.copyfile(version_json, os.path.join(ROOT, "version.json"))
    git("add", "version.json")
    if subprocess.run(["git", "-C", ROOT, "diff", "--cached", "--quiet", "--", "version.json"]).returncode != 0:
        git("commit", "-m", f"version.json {v}", "--", "version.json")
    git("push", "origin", DIST_BRANCH)
    print(f"published v{v} on {DIST_REPO}")


if __name__ == "__main__":
    main()
