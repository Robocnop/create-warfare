"""Vérifie les portes de progression de Create Warfare sur les recettes réellement chargées.

Usage : bash tools/check_progression.sh   (lance le serveur de test avec l'export, puis ce script)
        python -I tools/check_progression.py [dossier des exports]   (exports déjà faits)

Les exports viennent de tools/progression/zz_cw_dump.js (server-test/kubejs/export/cw_*.json) :
recettes finales (après KubeJS et Almost Unified), tables de butin, objets, tags.

Contrôles :
  1. chaque objet « porte » n'est produit que par les recettes autorisées (pas de voie de contournement) ;
  2. les recettes de porte contiennent bien l'ingrédient du palier précédent ;
  3. aucun butin ne donne un objet porte (hors liste tolérée) ;
  4. chaque quête demande un objet qui existe et qui s'obtient (recette, butin, ou liste d'exceptions) ;
  5. les objets non unifiés d'un même métal (plusieurs lingots dans un tag c:ingots/*).
Code de sortie 1 s'il reste une erreur.
"""
import collections, json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
D = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'server-test', 'kubejs', 'export')


def load(name):
    return json.load(open(os.path.join(D, name), encoding='utf-8'))


RAW = load('cw_recipes.json')
R = {k: v for k, v in RAW.items() if isinstance(v, dict)}
LOOT = load('cw_loot.json')
ITEMS = set(load('cw_items.json')['items'])
TAGS = load('cw_tags.json')
OUT_KEYS = ('result', 'results', 'output', 'outputs', 'assembly', 'transitional_item')


def ids(node, acc):
    """Tous les ids d'objet (item/id), de tag (#) et de fluide (~) d'un sous-arbre JSON."""
    if isinstance(node, dict):
        for k, v in node.items():
            if k in ('item', 'id') and isinstance(v, str) and ':' in v:
                acc.append(v)
            elif k == 'tag' and isinstance(v, str):
                acc.append('#' + v)
            elif k == 'fluid' and isinstance(v, str):
                acc.append('~' + v)
            elif k in ('items', 'tags') and isinstance(v, list) and all(isinstance(x, str) for x in v):
                acc.extend(('#' if k == 'tags' else '') + x for x in v)
            elif k == 'inputs' and isinstance(v, list) and all(isinstance(x, str) for x in v):
                acc.extend(x.split(' ')[-1] for x in v)  # Superb Warfare : "4 4 #c:plates/copper"
            else:
                ids(v, acc)
    elif isinstance(node, list):
        for x in node:
            ids(x, acc)
    return acc


def outs(r):
    """Sorties d'une recette. Assemblage séquencé : seulement le premier résultat (le reste est du rebut)."""
    acc = []
    sequenced = r.get('type') == 'create:sequenced_assembly'
    for k in OUT_KEYS:
        if k not in r or (sequenced and k == 'transitional_item'):
            continue
        v = r[k]
        if sequenced and k == 'results':
            v = v[:1]
        if isinstance(v, str):
            acc.append(v)
        else:
            ids(v, acc)
    return [x for x in acc if not x.startswith('#')]


def ins(r):
    acc = []
    for k, v in r.items():
        if k in OUT_KEYS or k == 'type':
            continue
        if k == 'sequence':
            for step in v:
                ids({kk: vv for kk, vv in step.items() if kk not in OUT_KEYS}, acc)
        else:
            ids(v, acc)
    return acc


BY_OUT = collections.defaultdict(list)
for rid, r in R.items():
    for o in set(outs(r)):
        BY_OUT[o].append(rid)


def loot_names(node, acc):
    if isinstance(node, dict):
        if node.get('type', '').endswith(':item') and 'name' in node:
            acc.append(node['name'])
        for v in node.values():
            loot_names(v, acc)
    elif isinstance(node, list):
        for x in node:
            loot_names(x, acc)
    return acc


BY_LOOT = collections.defaultdict(set)
for table, v in LOOT.items():
    if not isinstance(v, dict):
        continue
    for n in loot_names(v, []):
        if ':blocks/' in table and table.split('/')[-1] == n.split(':')[-1]:
            continue  # un bloc qui se lâche lui-même
        BY_LOOT[n].add(table)

# Recettes de stockage (lingot <-> bloc / pépites) : jamais une source nette.
STORAGE = [r'_from_(de)?compacting$', r'from_block$', r'from_nuggets$']
# Portes : objet -> (recettes autorisées, ingrédients exigés dans une recette principale, butin toléré). Regex.
GATES = {
    # Palier 3 : laiton
    'create:brass_ingot': ([r'^create:mixing/brass_ingot$'], ['#c:ingots/zinc'],
                           [r'^create_easy_structures:', r'^createmissiles:chests/bunker']),
    'create:precision_mechanism': ([r'^create:sequenced_assembly/precision_mechanism$'], ['#c:plates/gold'],
                                   [r'^create_easy_structures:']),
    # Palier 4 : acier
    'tfmg:steel_ingot': ([r'^tfmg:casting/steel$'], ['#c:molten_steel'], []),
    'tfmg:steel_mechanism': ([r'^tfmg:sequenced_assembly/steel_mechanism$'], ['#c:plates/steel'], []),
    # Palier 5 : électricité et électronique
    'createaddition:alternator': ([r'^createaddition:mechanical_crafting/alternator$'], ['#c:plates/steel'], []),
    'create_new_age:generator_coil': ([r'^create_new_age:shaped/generator_coil$'], ['#c:storage_blocks/steel'], []),
    'powergrid:conductive_casing': ([r'^powergrid:item_application/conductive_casing$'], ['#c:ingots/steel'], []),
    'tfmg:generator': ([r'^tfmg:sequenced_assembly/generator$'], ['#c:plates/steel'], []),
    'tfmg:circuit_board': ([r'^tfmg:sequenced_assembly/unfinished_circuit_board$'], ['tfmg:etched_circuit_board'], []),
    # Palier 6 : guerre mécanisée
    'superbwarfare:vehicle_assembling_table': ([r'^createwarfare:sbw/vehicle_assembling_table$'],
                                               ['tfmg:circuit_board', 'tfmg:steel_mechanism'], []),
    'superbwarfare:large_motor': ([r'^createwarfare:sbw/large_motor$'], ['createaddition:electric_motor'], []),
    'superbwarfare:wheel': ([r'^createwarfare:sbw/wheel$'], ['tfmg:rubber_sheet'], []),
    'create_radar:radar_bearing': ([r'^create_radar:crafting/radar_bearing$'], ['tfmg:circuit_board'], []),
    'create_radar:fire_controller': ([r'^create_radar:crafting/fire_controller$'], ['tfmg:circuit_board'], []),
    'createmissiles:launch_pad': ([r'^createmissiles:mechanical_crafting/launch_pad$'], ['tfmg:circuit_board'], []),
    'projectexplosive:munitions_workbench': ([r'^createwarfare:munitions_workbench$'],
                                             ['tfmg:circuit_board', 'tfmg:steel_mechanism'], []),
    'projectexplosive:airstrike_caller': ([r'^createwarfare:airstrike_caller$'], ['create_radar:fire_controller'], []),
    # Palier 7 : dissuasion
    'createnuclear:enriched_yellowcake': ([r'^createnuclear:enriched/enriched_yellowcake$'], ['createnuclear:yellowcake'], []),
    'projectexplosive:uranium_ingot': ([r'^createwarfare:uranium_ingot_compacting$'], ['createnuclear:enriched_yellowcake'], []),
    'projectexplosive:icbm': ([r'^createwarfare:icbm_designator$'], ['northstar:targeting_computer'], []),
}
# Objets que rien ne doit produire ni donner : recettes codées dans Project: Explosive (hors JSON), ou quête seule.
NO_RECIPE = ['projectexplosive:icbm_small', 'projectexplosive:icbm_big', 'projectexplosive:icbm_nuclear',
             'projectexplosive:nuclear_bomb', 'kubejs:launch_codes']
# Recettes qui doivent avoir disparu.
REMOVED = ['createnuclear:mixing/steel', 'cgs:mixing/steel_ingot', 'superbwarfare:steel_ingot_blasting',
           'superbwarfare:coal_iron_powder', 'createmissiles:assembly_cloning', 'projectexplosive:airstrike_caller',
           'projectexplosive:icbm', 'projectexplosive:munitions_workbench',
           'projectexplosive:uranium_ingot_from_smelting', 'projectexplosive:uranium_ingot_from_blasting']
# Quêtes dont l'objet ne vient ni d'une recette JSON ni d'un butin (vérifié à la main, raison indiquée).
QUEST_OK = {
    'overgeared:knappable_rock': 'clic droit au silex sur la pierre',
    'overgeared:blueprint': "table d'Overgeared",
    'overgeared:iron_plate': "forge à l'enclume (recette unifiée vers la tôle)",
    'powergrid:wire': 'bobinage (tag c:copper_coils)',
    'createbigcannons:cast_iron_cannon_barrel': 'coulée de Create Big Cannons (block_recipes)',
    'createbigcannons:cast_iron_cannon_chamber': 'coulée de Create Big Cannons (block_recipes)',
    'createbigcannons:cast_iron_sliding_breech': 'coulée de Create Big Cannons (block_recipes)',
    'createbigcannons:cast_iron_autocannon_breech': 'coulée de Create Big Cannons (block_recipes)',
    'cbcmodernwarfare:cast_iron_mediumcannon_breech': 'coulée (kubejs/data/cbcmodernwarfare/createbigcannons/block_recipes)',
    'aeronautics:levitite': 'mélange posé dans le monde (Aeronautics)',
    'projectexplosive:icbm_small': 'établi de munitions (codé dans le mod)',
    'projectexplosive:icbm_big': 'établi de munitions (codé dans le mod)',
    'projectexplosive:nuclear_bomb': 'établi de munitions (codé dans le mod)',
    'projectexplosive:icbm_nuclear': 'établi de munitions (codé dans le mod)',
    'northstar:titanium_tetrachloride_bucket': 'seau du fluide (mélange Northstar)',
    'cuffed:key_ring': 'recette spéciale de Cuffed',
    'kubejs:launch_codes': 'récompense de quête',
}

errors, notes = [], []


def match(text, patterns):
    return any(re.search(p, text) for p in patterns)


def expand(ingredient):
    """Un tag vaut aussi pour ses membres."""
    if ingredient.startswith('#'):
        return {ingredient} | set(TAGS.get(ingredient[1:], []))
    return {ingredient}


print(f'{len(R)} recettes, {len(LOOT)} tables de butin, {len(ITEMS)} objets, {len(TAGS)} tags\n')
print('== 1-3. Portes')
for item, (allowed, required, loot_ok) in GATES.items():
    if item not in ITEMS:
        errors.append(f'{item} : objet inconnu')
        continue
    recipes = BY_OUT.get(item, [])
    main = [r for r in recipes if match(r, allowed)]
    bad = [r for r in recipes if not match(r, allowed) and not match(r, STORAGE)]
    for r in bad:
        errors.append(f'{item} : recette de contournement {r} <- {sorted(set(ins(R[r])))[:8]}')
    if not main:
        errors.append(f'{item} : recette principale introuvable (attendu : {allowed[0]})')
    for need in required:
        if not any(expand(need) & set(ins(R[r])) for r in main):
            errors.append(f'{item} : ingrédient du palier absent ({need})')
    for table in sorted(BY_LOOT.get(item, [])):
        if match(table, loot_ok):
            notes.append(f'{item} : butin toléré {table}')
        else:
            errors.append(f'{item} : donné par le butin {table}')
    print(f'  {"OK " if main and not bad else "ERR"} {item:42} {len(main)} recette(s) principale(s), {len(bad)} contournement(s)')

for item in NO_RECIPE:
    for r in BY_OUT.get(item, []):
        errors.append(f'{item} : ne devrait avoir aucune recette, trouvé {r}')
    for table in BY_LOOT.get(item, []):
        errors.append(f'{item} : donné par le butin {table}')
for rid in REMOVED:
    if rid in RAW:
        errors.append(f'recette censée être retirée encore présente : {rid}')

# L'acier forgé d'Overgeared ne doit plus compter comme acier industriel.
if 'overgeared:steel_ingot' in TAGS.get('c:ingots/steel', []):
    errors.append('overgeared:steel_ingot est encore dans c:ingots/steel')
FORGED_OK = ('overgeared:', 'minecraft:flint_and_steel')  # ce que l'acier forgé a le droit de produire
for rid, r in R.items():
    if 'overgeared:steel_ingot' in ins(r):
        for o in outs(r):
            if not o.startswith(FORGED_OK):
                errors.append(f"l'acier forgé devient {o} par {rid} (passerelle vers l'acier industriel)")
STEEL = set()
for t in ('c:ingots/steel', 'c:nuggets/steel', 'c:plates/steel', 'c:storage_blocks/steel'):
    STEEL |= set(TAGS.get(t, []))
for rid, r in R.items():
    if rid.startswith('overgeared:') and r.get('type') != 'overgeared:forging':
        got = STEEL & set(outs(r))
        if got and not (STEEL & {x for i in ins(r) for x in expand(i)}):
            errors.append(f"Overgeared produit {sorted(got)} sans acier industriel en entrée : {rid}")
cool = R.get('overgeared:steel_ingot_from_cooling')
if cool and outs(cool) != ['overgeared:steel_ingot']:
    errors.append(f'overgeared:steel_ingot_from_cooling donne {outs(cool)} (attendu : overgeared:steel_ingot)')

print('\n== 4. Quêtes')
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import gen_quests  # noqa: E402

nq = 0
for chapter in gen_quests.CHAPTERS:
    for q in chapter[4]:
        nq += 1
        extra = q[7] if len(q) > 7 else {}
        wanted = [t for t, _ in extra.get('tasks', [])] or [q[1]]
        wanted += [it for it, _ in extra.get('rewards', [])]
        if 'reward_item' in extra:
            wanted.append(extra['reward_item'][0])
        for it in wanted:
            if it not in ITEMS:
                errors.append(f'quête {chapter[0]}:{q[0]} : objet inconnu {it}')
            elif (it not in BY_OUT and it not in BY_LOOT and it not in QUEST_OK
                  and not extra.get('checkmark') and not it.startswith('minecraft:')):
                errors.append(f'quête {chapter[0]}:{q[0]} : {it} sans recette ni butin')
print(f'  {nq} quêtes contrôlées')

print('\n== 5. Métaux en double (plusieurs objets dans un même tag c:ingots/*)')
for tag in sorted(TAGS):
    if tag.startswith('c:ingots/') and len(TAGS[tag]) > 1:
        print(f'  {tag} : {TAGS[tag]}')

print('\n== Notes')
for n in notes:
    print('  ', n)
print(f'\n== {len(errors)} erreur(s)')
for e in errors:
    print('  ERREUR', e)
if __name__ == '__main__':
    sys.exit(1 if errors else 0)
