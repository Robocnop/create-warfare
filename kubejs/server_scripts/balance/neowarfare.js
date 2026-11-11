// Create Warfare : recettes de Neo Warfare (cbcmodernwarfare), v1.8.6.
// Le jar (portage 1.20 incomplet) ne livre presque aucune recette : canons moyens, canons rotatifs, lance-missiles,
// munitions et accessoires n'existaient qu'en créatif, et la quête « Canon moyen » était impossible.
// Coulée et forage des canons : kubejs/data/cbcmodernwarfare/createbigcannons/block_recipes/ (le jar les range dans
// « block_recipies », que Create Big Cannons ne lit pas). Ici : moules, munitions et pièces assemblées.
// Les recettes suivent celles de Create Big Cannons (obus, obus d'autocanon), un cran au-dessus en coût.

ServerEvents.recipes(event => {
  const id = name => 'createwarfare:neowarfare/' + name
  const K = {
    C: '#c:ingots/cast_iron',
    I: '#c:ingots/iron',
    S: '#c:ingots/steel',
    W: '#c:ingots/tungsten',
    P: '#c:plates/steel',
    U: '#c:plates/copper',
    X: '#createbigcannons:high_explosive_materials',
    G: '#createbigcannons:gunpowder',
    B: 'createbigcannons:shot_balls',
    L: '#minecraft:wooden_slabs',
    K: '#minecraft:coals',
    T: 'create:electron_tube',
    E: 'tfmg:circuit_board',
    Q: 'createbigcannons:quickfiring_mechanism',
    Y: 'minecraft:spyglass',
    V: '#c:glass_panes/colorless',
    N: 'create:iron_sheet'
  }
  // Clés du motif : les majuscules viennent de K, les minuscules de « extra ».
  const keys = (pattern, extra) => {
    const used = {}
    pattern.join('').split('').forEach(c => {
      if (c == ' ') return
      used[c] = K[c] ? K[c] : extra[c]
    })
    return used
  }
  const shaped = (out, count, pattern, extra) => {
    event.shaped(Item.of('cbcmodernwarfare:' + out, count), pattern, keys(pattern, extra || {})).id(id(out))
  }
  const shapeless = (out, count, inputs) => {
    event.shapeless(Item.of('cbcmodernwarfare:' + out, count), inputs).id(id(out))
  }
  const mechanical = (out, pattern) => {
    const k = keys(pattern, {})
    const json = {}
    Object.keys(k).forEach(c => {
      json[c] = k[c].charAt(0) == '#' ? { tag: k[c].substring(1) } : { item: k[c] }
    })
    event.custom({
      type: 'create:mechanical_crafting',
      accept_mirrored: true,
      category: 'misc',
      pattern: pattern,
      key: json,
      result: { id: 'cbcmodernwarfare:' + out, count: 1 }
    }).id(id(out))
  }

  // Moules de coulée : à la scie, comme ceux de Create Big Cannons.
  const moulds = ['mediumcannon_barrel', 'mediumcannon_breech', 'mediumcannon_recoil_barrel',
    'rotarycannon_barrel', 'rotarycannon_bearing', 'rotarycannon_breech']
  moulds.forEach(m => {
    event.custom({
      type: 'create:cutting',
      ingredients: [{ tag: 'minecraft:logs' }],
      results: [{ id: 'cbcmodernwarfare:' + m + '_cast_mould' }]
    }).id(id('cutting/' + m + '_cast_mould'))
  })

  // Culasses : le mécanisme à tir rapide transforme la culasse coulée.
  const metals = ['cast_iron', 'bronze', 'steel', 'nethersteel']
  metals.forEach(mat => {
    shapeless(mat + '_mediumcannon_falling_breech', 1, ['cbcmodernwarfare:' + mat + '_mediumcannon_breech', K.Q])
  })
  shapeless('bronze_rotarycannon_breech_extractor', 1, ['cbcmodernwarfare:bronze_rotarycannon_breech', K.Q])
  shapeless('nethersteel_rotarycannon_breech_extractor', 1, ['cbcmodernwarfare:nethersteel_rotarycannon_breech', K.Q])
  // « rotaryannon » : faute d'orthographe du mod.
  shapeless('steel_rotaryannon_breech_extractor', 1, ['cbcmodernwarfare:steel_rotarycannon_breech', K.Q])

  // Obus de canon moyen (2 par recette) : ogive en haut, charge au milieu, culot en fer.
  shaped('ap_mediumcannon_round', 2, ['C', 'C', 'I'])
  shaped('aphe_mediumcannon_round', 2, ['C', 'X', 'I'])
  shaped('he_mediumcannon_round', 2, ['I', 'X', 'I'])
  shaped('hef_mediumcannon_round', 2, ['B', 'X', 'I'])
  shaped('heap_mediumcannon_round', 2, ['U', 'X', 'I'])
  shaped('apds_mediumcannon_round', 2, ['S', 'C', 'I'])
  shaped('apfsds_mediumcannon_round', 2, ['W', 'S', 'I'])
  shaped('canister_mediumcannon_round', 2, ['B', 'B', 'I'])
  shaped('smoke_mediumcannon_round', 2, ['I', 'K', 'G'])
  // Douilles : feuille de laiton à la scie, douille pliée, puis remplie de poudre tassée.
  event.custom({
    type: 'create:cutting',
    ingredients: [{ tag: 'c:plates/brass' }],
    results: [{ id: 'cbcmodernwarfare:mediumcannon_cartridge_sheet', count: 2 }]
  }).id(id('cutting/mediumcannon_cartridge_sheet'))
  shaped('empty_mediumcannon_cartridge', 1, ['d', 'd'], { d: 'cbcmodernwarfare:mediumcannon_cartridge_sheet' })
  shapeless('filled_mediumcannon_cartridge', 1, ['cbcmodernwarfare:empty_mediumcannon_cartridge', 'createbigcannons:packed_gunpowder'])

  // Obus d'autocanon supplémentaires (4 par recette, comme l'obus AP de Create Big Cannons).
  shaped('apds_autocannon_round', 4, ['S', 'C'])
  shaped('hvap_autocannon_round', 4, ['W', 'C'])
  shaped('he_autocannon_round', 4, ['X', 'I'])
  shaped('canister_autocannon_round', 4, ['B', 'I'])

  // Obus de gros canon.
  mechanical('heap_shell', [' U ', 'IXI', 'IXI', ' L '])
  mechanical('hefrag_shell', [' B ', 'IXI', 'IXI', ' L '])
  shaped('apds_shot', 1, [' S ', 'ICI', ' L '])

  // Accessoires de munitions.
  shapeless('incendiary_tip', 4, ['minecraft:blaze_powder', '#c:nuggets/iron'])
  shapeless('inert_explosives', 2, ['minecraft:sand', 'minecraft:clay_ball', K.G])
  shaped('smoke_discharger', 1, ['NNN', 'NaN', ' S '], { a: 'minecraft:dispenser' })
  shaped('smoke_discharger_round', 4, ['K', 'G', 'N'])
  shaped('rotarycannon_ammo_drum', 1, [' P ', 'PQP', ' P '])

  // Affûts, ressorts, blindage composite.
  shapeless('heavy_recoil_spring', 1, ['createbigcannons:recoil_spring', 'createbigcannons:recoil_spring', K.S])
  shaped('compact_mount', 1, [' N ', 'SaS'], { a: 'createbigcannons:cannon_mount' })
  shaped('composite_block', 4, ['aba', 'ccc', 'aba'], {
    a: 'cbcmodernwarfare:carbon_steel_ingot',
    b: 'tfmg:plastic_sheet',
    c: 'tfmg:fireproof_brick'
  })

  // Guidage des missiles : tubes électroniques et circuit imprimé TFMG, le capteur fait la différence.
  shaped('manual_command_guidance', 1, [' a ', 'TET', ' S '], { a: 'minecraft:lever' })
  shaped('optical_command_guidance', 1, [' Y ', 'TET', ' S '])
  shaped('infrared_seeker_guidance', 1, [' a ', 'TET', ' S '], { a: 'create:polished_rose_quartz' })

  // Viseurs.
  shaped('periscope_ring_sight', 1, [' V ', 'NYN'])
  shaped('periscope_arc_sight', 1, ['NYN', ' V '])
  shaped('remote_gun_sight', 1, [' Y ', 'TET', ' N '])

  // Propulsion à poudre et rails de roquettes.
  shaped('solid_fuel_tank', 2, ['PaP', 'PaP'], { a: 'createbigcannons:packed_gunpowder' })
  shaped('solid_fuel_thruster', 1, [' a ', 'PbP', 'P P'], { a: 'cbcmodernwarfare:solid_fuel_tank', b: 'minecraft:blaze_powder' })
  shapeless('solid_fuel_split_thruster', 1, ['cbcmodernwarfare:solid_fuel_thruster', 'cbcmodernwarfare:solid_fuel_thruster'])
  shaped('rocket_rails', 4, ['NNN', 'S S'])
  shaped('rocket_rails_end', 1, ['N', 'a'], { a: 'cbcmodernwarfare:rocket_rails' })
  shaped('torpedo_tube', 2, ['PPP', 'P P', 'PPP'])

  // Lance-missiles (gun launcher) : assemblé en acier, sans coulée (le mod n'a pas de moule pour lui).
  shaped('gun_launcher_barrel', 1, ['SaS', 'S S', 'SaS'], { a: 'cbcmodernwarfare:rocket_rails' })
  shaped('gun_launcher_chamber', 1, ['SaS', 'S S', 'SaS'], { a: '#c:storage_blocks/steel' })
  shaped('gun_launcher_sliding_breech', 1, ['SaS', 'SbS', 'SaS'], { a: '#c:storage_blocks/steel', b: 'create:precision_mechanism' })
  shapeless('gun_launcher_quickfiring_breech', 1, ['cbcmodernwarfare:gun_launcher_sliding_breech', K.Q])
  shaped('built_up_gun_launcher_barrel', 1, ['a', 'b', 'a'], { a: '#c:storage_blocks/steel', b: 'cbcmodernwarfare:gun_launcher_barrel' })
})
