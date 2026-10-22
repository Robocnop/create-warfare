"""Merge translated batches into kubejs/assets/<namespace>/lang/fr_fr.json.

Usage: python -I tools/merge_translations.py <done_dir>
<done_dir>/*.json = {"<namespace>|<key>": "<French text>"}, produced from the mods' lang files
(see docs/balance/TRADUCTIONS.md). Minecraft merges lang files key by key across resource packs,
so these files only hold the texts the mods do not translate themselves.
"""
import glob, json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "kubejs", "assets")


def main(done_dir):
    per_ns = {}
    for path in sorted(glob.glob(os.path.join(done_dir, "*.json"))):
        for full_key, text in json.load(open(path, encoding="utf-8")).items():
            ns, key = full_key.split("|", 1)
            per_ns.setdefault(ns, {})[key] = text
    for ns, texts in per_ns.items():
        folder = os.path.join(OUT, ns, "lang")
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, "fr_fr.json")
        if ns == "kubejs" and os.path.exists(path):
            texts = {**texts, **json.load(open(path, encoding="utf-8"))}  # keep the pack's own texts
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(dict(sorted(texts.items())), f, ensure_ascii=False, indent=1)
            f.write("\n")
    print(f"{sum(len(t) for t in per_ns.values())} texts written for {len(per_ns)} mods in {OUT}")


if __name__ == "__main__":
    main(sys.argv[1])
