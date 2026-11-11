// Create Warfare : Sable LRW (createarms), v1.8.6.
// 1. Missiles et bombes guidés : d'origine, un AIM-120 ou un Hellfire coûtait 1 TNT, 1 fusée et un objet banal.
//    Ils demandent maintenant de l'acier et de l'électronique ; les missiles à moyenne portée, un obus HE et des circuits TFMG.
//    L'objet distinctif d'origine reste en tête de recette (deux recettes identiques ont été séparées).
// 2. Objets sans recette dans le jar : périscope (quête bloquée), roquettes Hydra et panier, torpille, ASROC, Iron Dome.
//    Les missiles longue portée (Tomahawk, Kinzhal, LRASM, antinavire) restent dans military.js.

ServerEvents.recipes(event => {
  const id = name => 'createwarfare:lrw/' + name
  const S = '#c:ingots/steel'
  const T = 'create:electron_tube'
  const B = 'tfmg:circuit_board'
  const F = 'minecraft:firework_rocket'
  const X = 'minecraft:tnt'
  const H = 'createbigcannons:he_shell'
  const N = 'create:iron_sheet'
  const replace = (out, count, pattern, key) => {
    event.remove({ id: 'createarms:' + out })
    event.shaped(Item.of('createarms:' + out, count), pattern, key).id(id(out))
  }

  // Courte portée et antichar : acier, TNT, tubes électroniques.
  const light = {
    aim_9_sidewinder: '#c:nuggets/gold',
    r73: '#c:nuggets/iron',
    stinger: '#c:ingots/copper',
    agm114l: 'minecraft:redstone',
    agm114k: 'minecraft:redstone_torch',
    agm_65e_maverick: '#c:glass_panes/colorless',
    tv_missile: 'minecraft:spyglass'
  }
  Object.keys(light).forEach(m => {
    replace(m, 1, [' D ', 'SXS', 'TFT'], { D: light[m], S: S, X: X, T: T, F: F })
  })

  // Moyenne portée et sol-air : obus HE et circuits imprimés.
  const heavy = {
    aim_120c_amraam: 'minecraft:comparator',
    r77m: '#c:gems/quartz',
    patriot: 'createarms:apg_radar'
  }
  Object.keys(heavy).forEach(m => {
    replace(m, 1, [' D ', 'SHS', 'BFB'], { D: heavy[m], S: S, H: H, B: B, F: F })
  })

  // Bombes : lisse en acier, guidées avec un tube électronique.
  replace('dumb_bomb', 1, ['S', 'X', 'S'], { S: S, X: X })
  replace('gbu_38_jdam', 1, [' T ', 'SXS'], { T: T, S: S, X: X })
  replace('gbu_39', 2, ['T', 'X', 'S'], { T: T, S: S, X: X })

  // Objets sans recette.
  const add = (out, count, pattern, key) => {
    event.shaped(Item.of('createarms:' + out, count), pattern, key).id(id(out))
  }
  add('periscope_mast', 1, [' G ', 'NYN', ' N '], { G: '#c:glass_panes/colorless', N: N, Y: 'minecraft:spyglass' })
  add('periscope_eyepiece', 1, ['Y', 'N'], { Y: 'minecraft:spyglass', N: N })
  add('hydra_rocket', 4, ['X', 'S', 'F'], { X: X, S: S, F: F })
  add('rocket_pod', 1, ['NNN', 'NMN', 'NNN'], { N: N, M: 'createarms:munition_rack' })
  add('torpedo', 1, [' T ', 'SHS', ' P '], { T: T, S: S, H: H, P: 'create:propeller' })
  add('asroc', 1, [' K ', 'SHS', 'BFB'], { K: 'createarms:torpedo', S: S, H: H, B: B, F: F })
  add('iron_dome_missile', 1, [' D ', 'SXS', 'TFT'], { D: 'minecraft:lightning_rod', S: S, X: X, T: T, F: F })
  add('aerial_target_controller', 1, ['TBT', 'NLN'], { T: T, B: B, N: N, L: 'create:linked_controller' })
})
