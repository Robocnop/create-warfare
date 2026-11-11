// Create Warfare : recettes d'Armored Constructs (arcon), v1.8.6.
// Le jar ne contient aucune recette : blindages, canon et modules n'existaient qu'en créatif,
// et la quête « Canon blindé » était impossible.
// Blindages : une recette par matériau (4 blocs de la première teinte), puis le tailleur de pierre change la teinte.

const ARCON_HULLS = {
  "aluminum": [
    "arcon:hull_aluminum_aviation_green",
    "arcon:hull_aluminum_dark_ghost_gray",
    "arcon:hull_aluminum_dark_tan",
    "arcon:hull_aluminum_desert_sand",
    "arcon:hull_aluminum_flanker_dark_blue",
    "arcon:hull_aluminum_flanker_light_blue",
    "arcon:hull_aluminum_flanker_medium_blue",
    "arcon:hull_aluminum_forest_green",
    "arcon:hull_aluminum_gunship_gray",
    "arcon:hull_aluminum_insignia_white",
    "arcon:hull_aluminum_light_ghost_gray",
    "arcon:hull_aluminum_matte_black",
    "arcon:hull_aluminum_neutral_gray",
    "arcon:hull_aluminum_olive_drab",
    "arcon:hull_aluminum_radome_green"
  ],
  "boron": [
    "arcon:hull_boron_brown",
    "arcon:hull_boron_cobalt_operational",
    "arcon:hull_boron_dark",
    "arcon:hull_boron_desert",
    "arcon:hull_boron_eastern_european",
    "arcon:hull_boron_factory",
    "arcon:hull_boron_faded_khaki",
    "arcon:hull_boron_forest",
    "arcon:hull_boron_green",
    "arcon:hull_boron_green_gray",
    "arcon:hull_boron_polar_blue",
    "arcon:hull_boron_sand_green",
    "arcon:hull_boron_shadow_violet_gray",
    "arcon:hull_boron_snow",
    "arcon:hull_boron_urban"
  ],
  "ceramic": [
    "arcon:hull_ceramic_brown",
    "arcon:hull_ceramic_cobalt_operational",
    "arcon:hull_ceramic_dark",
    "arcon:hull_ceramic_desert",
    "arcon:hull_ceramic_eastern_european",
    "arcon:hull_ceramic_factory",
    "arcon:hull_ceramic_faded_khaki",
    "arcon:hull_ceramic_forest",
    "arcon:hull_ceramic_green",
    "arcon:hull_ceramic_green_gray",
    "arcon:hull_ceramic_polar_blue",
    "arcon:hull_ceramic_sand_green",
    "arcon:hull_ceramic_shadow_violet_gray",
    "arcon:hull_ceramic_snow",
    "arcon:hull_ceramic_urban"
  ],
  "nickel": [
    "arcon:hull_nickel_brown",
    "arcon:hull_nickel_cobalt_operational",
    "arcon:hull_nickel_dark",
    "arcon:hull_nickel_desert",
    "arcon:hull_nickel_eastern_european",
    "arcon:hull_nickel_factory",
    "arcon:hull_nickel_faded_khaki",
    "arcon:hull_nickel_forest",
    "arcon:hull_nickel_green",
    "arcon:hull_nickel_green_gray",
    "arcon:hull_nickel_polar_blue",
    "arcon:hull_nickel_sand_green",
    "arcon:hull_nickel_shadow_violet_gray",
    "arcon:hull_nickel_snow",
    "arcon:hull_nickel_urban"
  ],
  "rolled": [
    "arcon:hull_rolled_brown",
    "arcon:hull_rolled_cobalt_operational",
    "arcon:hull_rolled_dark",
    "arcon:hull_rolled_desert",
    "arcon:hull_rolled_eastern_european",
    "arcon:hull_rolled_factory",
    "arcon:hull_rolled_faded_khaki",
    "arcon:hull_rolled_forest",
    "arcon:hull_rolled_green",
    "arcon:hull_rolled_green_gray",
    "arcon:hull_rolled_polar_blue",
    "arcon:hull_rolled_sand_green",
    "arcon:hull_rolled_shadow_violet_gray",
    "arcon:hull_rolled_snow",
    "arcon:hull_rolled_urban"
  ],
  "titanium": [
    "arcon:hull_titanium_aviation_green",
    "arcon:hull_titanium_dark_ghost_gray",
    "arcon:hull_titanium_dark_tan",
    "arcon:hull_titanium_desert_sand",
    "arcon:hull_titanium_flanker_dark_blue",
    "arcon:hull_titanium_flanker_light_blue",
    "arcon:hull_titanium_flanker_medium_blue",
    "arcon:hull_titanium_forest_green",
    "arcon:hull_titanium_gunship_gray",
    "arcon:hull_titanium_insignia_white",
    "arcon:hull_titanium_light_ghost_gray",
    "arcon:hull_titanium_matte_black",
    "arcon:hull_titanium_neutral_gray",
    "arcon:hull_titanium_olive_drab",
    "arcon:hull_titanium_radome_green"
  ]
}
const ARCON_KONTAKT = ["arcon:kontakt_1_black", "arcon:kontakt_1_desert", "arcon:kontakt_1_forest", "arcon:kontakt_1_green", "arcon:kontakt_1_snow"]

ServerEvents.tags('item', event => {
  Object.keys(ARCON_HULLS).forEach(mat => event.add('createwarfare:arcon_hull/' + mat, ARCON_HULLS[mat]))
  event.add('createwarfare:arcon_kontakt', ARCON_KONTAKT)
})

ServerEvents.recipes(event => {
  const id = name => 'createwarfare:arcon/' + name
  const S = '#c:ingots/steel'
  const P = '#c:plates/steel'

  // Matériau de surface de chaque blindage. Le titane vient de Northstar (espace) ou d'Air Wars (monde normal).
  const surface = {
    rolled: [P],
    aluminum: ['#c:plates/aluminum'],
    nickel: ['tfmg:nickel_sheet'],
    ceramic: ['tfmg:fireproof_brick'],
    boron: ['tfmg:silicon_ingot'],
    titanium: ['#c:plates/titanium', 'create_the_air_wars:titaniumsheet']
  }
  Object.keys(ARCON_HULLS).forEach(mat => {
    const blocks = ARCON_HULLS[mat]
    surface[mat].forEach((top, n) => {
      event.shaped(Item.of(blocks[0], 4), ['AA', 'SS'], { A: top, S: S }).id(id('hull_' + mat + (n ? '_' + n : '')))
    })
    blocks.forEach(block => {
      event.stonecutting(block, '#createwarfare:arcon_hull/' + mat).id(id('teinte/' + block.split(':')[1]))
    })
  })

  // Kontakt-1 (blindage réactif) : plaque d'acier et poudre tassée, teinte au tailleur de pierre.
  event.shaped(Item.of('arcon:kontakt_1_platform', 4), [' P ', 'PSP'], { P: P, S: S }).id(id('kontakt_1_platform'))
  event.shaped(Item.of(ARCON_KONTAKT[0], 2), ['PGP'], { P: P, G: 'createbigcannons:packed_gunpowder' }).id(id('kontakt_1'))
  ARCON_KONTAKT.forEach(block => {
    event.stonecutting(block, '#createwarfare:arcon_kontakt').id(id('teinte/' + block.split(':')[1]))
  })

  // Canon Arcon : tout en acier, avec ressort de recul et mécanisme à tir rapide de Create Big Cannons.
  event.shaped('arcon:arcon_cannon_barrel', ['SSS', 'R R', 'SSS'], { S: S, R: 'createbigcannons:recoil_spring' }).id(id('arcon_cannon_barrel'))
  event.shaped('arcon:arcon_barrel_section', ['SBS', 'SQS', 'SBS'], {
    S: S, B: '#c:storage_blocks/steel', Q: 'createbigcannons:quickfiring_mechanism'
  }).id(id('arcon_barrel_section'))
  event.shaped('arcon:arcon_cannon_muzzle', [' S ', 'SPS', ' S '], { S: S, P: P }).id(id('arcon_cannon_muzzle'))
  event.shaped('arcon:arcon_intermediate_muzzle', ['SPS'], { S: S, P: P }).id(id('arcon_intermediate_muzzle'))
  event.shaped(Item.of('arcon:bops', 2), ['S', 'G', 'C'], {
    S: S, G: 'createbigcannons:packed_gunpowder', C: 'createbigcannons:big_cartridge_sheet'
  }).id(id('bops'))

  // Modules de tourelle : optique, tubes électroniques et circuit imprimé TFMG.
  const turretModule = (out, sensor) => {
    event.shaped('arcon:' + out, ['PAP', 'TBT', 'PSP'], {
      P: P, A: sensor, T: 'create:electron_tube', B: 'tfmg:circuit_board', S: S
    }).id(id(out))
  }
  turretModule('aiming_module', '#c:glass_panes/colorless')
  turretModule('optical_observation_unit', 'minecraft:spyglass')
  turretModule('thermal_vision_module', 'create:polished_rose_quartz')
  event.shaped('arcon:power_generator', ['PSP', 'CAC', 'PSP'], {
    P: P, S: S, C: 'tfmg:electromagnetic_coil', A: 'create:shaft'
  }).id(id('power_generator'))
  event.shaped('arcon:steel_seat', ['W', 'P'], { W: '#minecraft:wool', P: P }).id(id('steel_seat'))
})
