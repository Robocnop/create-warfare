# Patch Advanced Weather (MIT) pour Create Warfare. Rejouable sur un jar deja patche.
# 1. Deplace cote client deux mixins qui chargent des classes client et font planter le serveur dedie.
# 2. Remplace AWFluids (source : tools/patch_advanced_weather/) : le mercure n'a plus de seau ni de bloc fantome.
#    Sans ca, le plugin JEI de Create plante (« Unknown registration mercury_bucket ») et ses recettes
#    (assemblage sequence, bec verseur, ...) disparaissent de JEI/EMI.
# Usage : python -I tools/patch_advanced_weather.py <jar d'origine ou -cw> <nouveau jar -cw>
# Il faut le JDK 21 et les bibliotheques NeoForge 21.1.264 de CurseForge (variables JAVAC et CF_LIBS pour changer).
import json, os, subprocess, sys, tempfile, zipfile

src, dst = sys.argv[1], sys.argv[2]
MOVE = ["BlockFrictionMixin", "compat.SeasonHooksClientMixin"]
CLASS = "net/antopfr/advancedweather/content/fluid/AWFluids.class"
HERE = os.path.dirname(os.path.abspath(__file__))
JAVA_SRC = os.path.join(HERE, "patch_advanced_weather", CLASS.replace(".class", ".java"))
JAVAC = os.environ.get("JAVAC", r"C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot\bin\javac.exe")
LIBS = os.environ.get("CF_LIBS", os.path.expanduser(r"~\curseforge\minecraft\Install\libraries"))
CP_LIBS = [
    "net/neoforged/neoforge/21.1.264/neoforge-21.1.264-universal.jar",
    "net/neoforged/neoforge/21.1.264/neoforge-21.1.264-client.jar",
    "net/minecraft/client/1.21.1-20240808.144430/client-1.21.1-20240808.144430-srg.jar",
]


def compile_awfluids(tmp):
    with zipfile.ZipFile(src) as z:
        reg = next(n for n in z.namelist() if n.startswith("META-INF/jarjar/Registrate-"))
        z.extract(reg, tmp)
    cp = [os.path.join(tmp, reg), os.path.abspath(src)] + [os.path.join(LIBS, p) for p in CP_LIBS]
    for p in cp:
        if not os.path.isfile(p):
            sys.exit("Introuvable : " + p)
    out = os.path.join(tmp, "out")
    subprocess.run([JAVAC, "--release", "21", "-proc:none", "-nowarn", "-cp", os.pathsep.join(cp), "-d", out, JAVA_SRC],
                   check=True)
    with open(os.path.join(out, CLASS), "rb") as f:
        return f.read()


with tempfile.TemporaryDirectory() as tmp:
    awfluids = compile_awfluids(tmp)
    with zipfile.ZipFile(src) as zin, zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as zout:
        for item in zin.infolist():
            data = zin.read(item.filename)
            if item.filename == "advancedweather.mixins.json":
                cfg = json.loads(data)
                for m in MOVE:
                    if m in cfg["mixins"]:
                        cfg["mixins"].remove(m)
                    if m not in cfg["client"]:
                        cfg["client"].append(m)
                data = json.dumps(cfg, indent=2).encode()
            elif item.filename == CLASS:
                data = awfluids
            zout.writestr(item, data)
print("OK :", dst)
