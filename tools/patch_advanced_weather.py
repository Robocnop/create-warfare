# Patch Advanced Weather (MIT) : deplace cote client deux mixins qui chargent des classes client et font planter le serveur dedie.
# Usage : python -I tools/patch_advanced_weather.py <jar CurseForge> <jar -cw>
import json, sys, zipfile
src, dst = sys.argv[1], sys.argv[2]
MOVE = ["BlockFrictionMixin", "compat.SeasonHooksClientMixin"]
with zipfile.ZipFile(src) as zin, zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as zout:
    for item in zin.infolist():
        data = zin.read(item.filename)
        if item.filename == "advancedweather.mixins.json":
            cfg = json.loads(data)
            for m in MOVE:
                cfg["mixins"].remove(m)
                cfg["client"].append(m)
            data = json.dumps(cfg, indent=2).encode()
        zout.writestr(item, data)
