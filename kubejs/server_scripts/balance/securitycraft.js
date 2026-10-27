// Create Warfare : SecurityCraft passe par Create et TFMG (v1.4.0).
// Recettes d'origine lues dans le jar (data/securitycraft/recipe/), réécrites avec le même id.
// Les blocs renforcés restent cassables par les explosions grâce à SC-SuperB Bridge et CBC Security Breach.

ServerEvents.recipes(event => {
  // Renforceurs : le manche en bois devient un mécanisme de précision (Create).
  event.remove({ id: 'securitycraft:universal_block_reinforcer_lvl1' })
  event.shaped('securitycraft:universal_block_reinforcer_lvl1', [' DG', 'RLD', 'PR '], {
    D: '#c:gems/diamond',
    G: '#c:glass_blocks/colorless',
    L: 'securitycraft:laser_block',
    R: '#c:dusts/redstone',
    P: 'create:precision_mechanism'
  }).id('securitycraft:universal_block_reinforcer_lvl1')

  event.remove({ id: 'securitycraft:universal_block_reinforcer_lvl2' })
  event.shaped('securitycraft:universal_block_reinforcer_lvl2', [' DG', 'RLD', 'PR '], {
    D: 'securitycraft:reinforced_diamond_block',
    G: 'securitycraft:reinforced_black_stained_glass',
    L: 'securitycraft:laser_block',
    R: '#c:dusts/redstone',
    P: 'create:precision_mechanism'
  }).id('securitycraft:universal_block_reinforcer_lvl2')

  event.remove({ id: 'securitycraft:universal_block_reinforcer_lvl3' })
  event.shaped('securitycraft:universal_block_reinforcer_lvl3', [' EG', 'RNE', 'PR '], {
    E: 'securitycraft:reinforced_emerald_block',
    G: 'securitycraft:reinforced_pink_stained_glass',
    N: '#c:nether_stars',
    R: 'securitycraft:reinforced_redstone_block',
    P: 'create:precision_mechanism'
  }).id('securitycraft:universal_block_reinforcer_lvl3')

  // Tourelle : acier TFMG et tubes électroniques au lieu du fer et de la redstone.
  event.remove({ id: 'securitycraft:sentry' })
  event.shaped('securitycraft:sentry', ['EDE', 'SPS', 'BBB'], {
    E: 'create:electron_tube',
    D: 'securitycraft:reinforced_dispenser',
    S: '#c:ingots/steel',
    P: 'securitycraft:portable_radar',
    B: 'securitycraft:reinforced_iron_block'
  }).id('securitycraft:sentry')

  // Système Trophy (abat les projectiles) : circuit imprimé et acier, pour qu'il reste rare.
  event.remove({ id: 'securitycraft:trophy_system' })
  event.shaped('securitycraft:trophy_system', [' T ', 'CBC', 'S S'], {
    T: 'securitycraft:sentry',
    C: 'tfmg:circuit_board',
    B: 'securitycraft:reinforced_iron_block',
    S: '#c:ingots/steel'
  }).id('securitycraft:trophy_system')
})
