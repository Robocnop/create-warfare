import sys
rows=[l.split("\t") for l in open(sys.argv[1]).read().splitlines() if l]
js=["// TEMPORAIRE : force le chargement des classes visees par des mixins (audit 1.7.7)",
"ServerEvents.loaded(event => {","  const targets = ["]
js+=[f'    ["{a}", "{b}"],' for a,b in rows]
js+=["  ];","  let ok = 0;","  targets.forEach(t => {",
"    try { Java.loadClass(t[1]); ok++; }",
"    catch (e) { console.error('CW-AUDIT ECHEC ' + t[0] + ' -> ' + t[1] + ' : ' + e); }","  });",
"  console.info('CW-AUDIT ' + ok + '/' + targets.length + ' classes chargees');","});"]
open(sys.argv[2],"w",encoding="utf8").write("\n".join(js)+"\n")
