// Create Warfare : mur de progression des armes lourdes.
// Principe : l'ICBM et les missiles longue portée demandent toute la chaîne industrielle
// (acier TFMG, nucléaire civil, électronique) plus des « Codes de lancement » obtenus par quête.
// Les recettes d'origine sont relevées dans docs/balance/A-militaire.md.

// ICBM de Project: Explosive. Ils sont fabriqués dans l'établi de munitions, dont les recettes sont
// codées dans le mod (pas de JSON) : on ne peut pas y ajouter d'ingrédient. Les codes sont donc
// consommés à la POSE de l'ICBM, ce qui bloque aussi la pose automatique (déployeur).
const ICBMS = ['projectexplosive:icbm_small', 'projectexplosive:icbm_big', 'projectexplosive:icbm_nuclear']
const LAUNCH_CODES = 'kubejs:launch_codes'

// Pièces de Create Missiles : le fer devient de l'acier (moitié de la quantité), chaque propulseur
// demande un mécanisme de précision (tube électronique pour la gamme feu d'artifice) et chaque
// ogive destructrice deux obus HE de Create Big Cannons. Le reste de la recette d'origine est conservé.
const MISSILE_PARTS = {
    ancient_chassis: [{"items": ["minecraft:sculk_catalyst"], "count": 4}, {"items": ["minecraft:sculk_sensor"], "count": 3}, {"tags": ["c:ingots/steel"], "count": 10}],
    ancient_thruster: [{"items": ["minecraft:sculk_catalyst"], "count": 2}, {"items": ["minecraft:echo_shard"], "count": 2}, {"tags": ["c:ingots/steel"], "count": 10}, {"items": ["create:precision_mechanism"], "count": 1}],
    ancient_warhead: [{"items": ["minecraft:sculk"], "count": 32}, {"items": ["minecraft:sculk_shrieker"], "count": 1}, {"items": ["minecraft:echo_shard"], "count": 12}, {"items": ["minecraft:gunpowder"], "count": 25}, {"items": ["createbigcannons:he_shell"], "count": 2}],
    blazing_warhead: [{"items": ["minecraft:blaze_rod"], "count": 20}, {"tags": ["c:ingots/steel"], "count": 5}, {"items": ["minecraft:flint"], "count": 10}, {"items": ["minecraft:gunpowder"], "count": 15}, {"items": ["createbigcannons:he_shell"], "count": 2}],
    direct_hit_warhead: [{"tags": ["c:storage_blocks/steel"], "count": 3}],
    dragon_chassis: [{"items": ["minecraft:obsidian"], "count": 32}, {"tags": ["c:ingots/steel"], "count": 15}],
    dragon_thruster: [{"items": ["minecraft:obsidian"], "count": 16}, {"tags": ["c:ingots/steel"], "count": 8}, {"items": ["create:precision_mechanism"], "count": 1}],
    dragon_warhead: [{"items": ["createmissiles:reinforced_dragon_egg_shell"], "count": 3}, {"items": ["minecraft:gunpowder"], "count": 40}, {"items": ["createbigcannons:he_shell"], "count": 2}],
    excavator_chassis: [{"items": ["minecraft:piston"], "count": 5}, {"tags": ["c:ingots/steel"], "count": 8}],
    excavator_thruster: [{"items": ["minecraft:piston"], "count": 2}, {"tags": ["c:ingots/steel"], "count": 3}, {"items": ["create:precision_mechanism"], "count": 1}],
    excavator_warhead: [{"items": ["minecraft:gunpowder"], "count": 20}, {"tags": ["c:ingots/steel"], "count": 10}, {"items": ["createbigcannons:he_shell"], "count": 2}],
    firework_chassis: [{"items": ["minecraft:paper"], "count": 2}, {"items": ["minecraft:gunpowder"], "count": 3}, {"items": ["create:electron_tube"], "count": 1}],
    firework_thruster: [{"tags": ["minecraft:planks"], "count": 1}, {"items": ["minecraft:paper"], "count": 2}, {"items": ["create:electron_tube"], "count": 1}],
    flaming_chassis: [{"tags": ["c:ingots/steel"], "count": 13}, {"items": ["minecraft:blaze_rod"], "count": 16}],
    flaming_thruster: [{"tags": ["c:ingots/steel"], "count": 8}, {"items": ["minecraft:blaze_rod"], "count": 8}, {"items": ["create:precision_mechanism"], "count": 1}],
    flaming_warhead: [{"items": ["minecraft:blaze_rod"], "count": 6}, {"tags": ["c:ingots/steel"], "count": 3}, {"items": ["minecraft:flint"], "count": 5}, {"items": ["minecraft:gunpowder"], "count": 10}],
    frost_chassis: [{"items": ["minecraft:snow_block"], "count": 32}, {"tags": ["c:ingots/steel"], "count": 6}, {"items": ["minecraft:stick"], "count": 2}],
    frost_thruster: [{"items": ["minecraft:snow_block"], "count": 16}, {"tags": ["c:ingots/steel"], "count": 3}, {"items": ["create:precision_mechanism"], "count": 1}],
    frozen_chassis: [{"tags": ["c:ingots/steel"], "count": 13}, {"items": ["minecraft:blue_ice"], "count": 30}],
    frozen_thruster: [{"tags": ["c:ingots/steel"], "count": 8}, {"items": ["minecraft:blue_ice"], "count": 15}, {"items": ["create:precision_mechanism"], "count": 1}],
    frozen_warhead: [{"items": ["minecraft:carved_pumpkin"], "count": 10}, {"items": ["minecraft:blue_ice"], "count": 30}, {"items": ["minecraft:gunpowder"], "count": 25}, {"items": ["createbigcannons:he_shell"], "count": 2}],
    guardian_warhead: [{"items": ["minecraft:tadpole_bucket"], "count": 1}, {"items": ["minecraft:cod"], "count": 32}, {"items": ["minecraft:prismarine_crystals"], "count": 20}, {"items": ["minecraft:gunpowder"], "count": 25}, {"items": ["createbigcannons:he_shell"], "count": 2}],
    infernal_warhead: [{"items": ["minecraft:blaze_rod"], "count": 25}, {"items": ["minecraft:lava_bucket"], "count": 10}, {"items": ["minecraft:gunpowder"], "count": 25}, {"items": ["createbigcannons:he_shell"], "count": 2}],
    messy_warhead: [{"items": ["minecraft:ender_pearl"], "count": 64}, {"items": ["minecraft:gunpowder"], "count": 20}, {"items": ["createbigcannons:he_shell"], "count": 2}],
    tunneler_warhead: [{"items": ["minecraft:gunpowder"], "count": 30}, {"tags": ["c:storage_blocks/steel"], "count": 5}, {"items": ["createbigcannons:he_shell"], "count": 2}],
    withered_chassis: [{"items": ["minecraft:soul_sand", "minecraft:soul_soil"], "count": 12}, {"tags": ["c:ingots/steel"], "count": 13}],
    withered_thruster: [{"items": ["minecraft:soul_sand", "minecraft:soul_soil"], "count": 6}, {"tags": ["c:ingots/steel"], "count": 8}, {"items": ["create:precision_mechanism"], "count": 1}],
    withered_warhead: [{"items": ["minecraft:soul_sand", "minecraft:soul_soil"], "count": 4}, {"items": ["minecraft:nether_star"], "count": 1}, {"items": ["minecraft:wither_skeleton_skull"], "count": 3}, {"items": ["minecraft:gunpowder"], "count": 30}, {"items": ["createbigcannons:he_shell"], "count": 2}],
}

ServerEvents.recipes(event => {
  // Établi de munitions : passe d'un établi en fer à un poste de l'industrie lourde.
  event.remove({ id: 'projectexplosive:munitions_workbench' })
  event.shaped('projectexplosive:munitions_workbench', [
    'SSS',
    'PCP',
    'MBM'
  ], {
    S: '#c:ingots/steel',
    P: 'create:precision_mechanism',
    C: 'minecraft:crafting_table',
    M: 'tfmg:steel_mechanism',
    B: '#c:storage_blocks/steel'
  }).id('createwarfare:munitions_workbench')

  // Uranium de Project: Explosive : plus de cuisson de minerai, il vient du yellowcake enrichi
  // de Create Nuclear (chapitre 8), compacté sous brûleur surchauffé avec du plomb.
  event.remove({ id: 'projectexplosive:uranium_ingot_from_smelting' })
  event.remove({ id: 'projectexplosive:uranium_ingot_from_blasting' })
  event.custom({
    type: 'create:compacting',
    ingredients: [
      { item: 'createnuclear:enriched_yellowcake' },
      { item: 'createnuclear:enriched_yellowcake' },
      { item: 'createnuclear:enriched_yellowcake' },
      { item: 'createnuclear:enriched_yellowcake' },
      { tag: 'c:ingots/lead' }
    ],
    results: [{ id: 'projectexplosive:uranium_ingot' }],
    heat_requirement: 'superheated'
  }).id('createwarfare:uranium_ingot_compacting')

  // Create Missiles : suppression du clonage d'assemblage avec du papier.
  event.remove({ id: 'createmissiles:assembly_cloning' })

  // Create Missiles : coût des pièces (voir MISSILE_PARTS).
  Object.keys(MISSILE_PARTS).forEach(part => {
    event.remove({ id: `createmissiles:missile_part/${part}` })
    event.custom({
      type: 'createmissiles:missile_part',
      ingredients: MISSILE_PARTS[part],
      assembly: `createmissiles:${part}`
    }).id(`createwarfare:missile_part/${part}`)
  })

  // Sable LRW : les missiles longue portée demandent des codes de lancement,
  // de l'acier et de l'électronique (avant : TNT, feux d'artifice et un bloc de redstone).
  const longRange = {
    'createarms:tomahawk': { pattern: ['BLB', 'TTT', 'SFS'], key: { B: 'tfmg:circuit_board' } },
    'createarms:kinzhal': { pattern: ['PLP', 'TTT', 'SFS'], key: { P: 'create:precision_mechanism' } },
    'createarms:lrasm': { pattern: ['DLD', 'TTT', 'SFS'], key: { D: 'minecraft:diamond' } },
    'createarms:ship_missile': { pattern: ['RLR', 'TTT', 'SFS'], key: { R: 'createarms:apg_radar' } }
  }
  Object.keys(longRange).forEach(missile => {
    const r = longRange[missile]
    event.remove({ output: missile })
    event.shaped(missile, r.pattern, Object.assign({
      L: LAUNCH_CODES,
      T: 'minecraft:tnt_minecart',
      S: '#c:ingots/steel',
      F: 'minecraft:firework_rocket'
    }, r.key)).id(`createwarfare:${missile.split(':')[1]}`)
  })
})

// Pose d'un ICBM : il faut des codes de lancement dans la main secondaire, un exemplaire est consommé.
// Les joueurs en créatif ne sont pas concernés.
ICBMS.forEach(icbm => {
  BlockEvents.placed(icbm, event => {
    const player = event.player
    if (player && player.isCreative()) return
    const codes = player ? player.offHandItem : null
    if (!codes || codes.id != LAUNCH_CODES) {
      if (player) player.tell(Text.red('Il faut des Codes de lancement dans la main secondaire pour armer un ICBM (quête « Codes de lancement », chapitre 10).'))
      return event.cancel()
    }
    codes.shrink(1)
    if (icbm == 'projectexplosive:icbm_nuclear') {
      player.server.tell(Text.gold(`☢ ${player.username} vient d'armer un ICBM nucléaire.`))
    }
  })
})
