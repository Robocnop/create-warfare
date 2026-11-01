import json,sys,subprocess,os,re,zipfile,tempfile
mods,javap,out=sys.argv[1],sys.argv[2],sys.argv[3]
prefixes=sys.argv[4:]
res=[]
for f in sorted(os.listdir(mods)):
    if not any(f.lower().startswith(p.lower()) for p in prefixes): continue
    z=zipfile.ZipFile(os.path.join(mods,f)); names=set(z.namelist())
    tmp=tempfile.mkdtemp()
    for cfg in [n for n in names if n.endswith(".json") and "mixin" in n.lower() and "refmap" not in n.lower()]:
        try: c=json.loads(z.read(cfg))
        except Exception: continue
        pkg=c.get("package")
        if not pkg: continue
        for cl in c.get("mixins",[])+c.get("server",[]):
            n=(pkg+"."+cl).replace(".","/")+".class"
            if n not in names: continue
            p=os.path.join(tmp,os.path.basename(n)); open(p,"wb").write(z.read(n))
            txt=subprocess.run([javap,"-v",p],capture_output=True,text=True).stdout
            i=txt.find("org.spongepowered.asm.mixin.Mixin(")
            if i<0: continue
            seg=txt[i:i+1500].split(")\n")[0]
            for t in re.findall(r'class L?([\w/$.]+);?',seg)+re.findall(r'"L?([\w.$/]+);?"',seg):
                t=t.replace("/",".")
                if "." in t: res.append((f,t))
open(out,"w").write("\n".join(f"{a}\t{b}" for a,b in sorted(set(res))))
print(len(set(res)),"cibles")
