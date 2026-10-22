// Create Warfare : liens entre mods, pour que l'armement passe par l'industrie lourde (chapitre 4).
// Seules des recettes lues dans les jars sont modifiées (voir docs/balance/B-coherence.md).

ServerEvents.recipes(event => {
  // Radars : la conduite de tir demande un circuit imprimé TFMG au lieu des comparateurs,
  // et l'écran radar un boîtier en acier au lieu du boîtier en laiton.
  event.replaceInput({ id: 'create_radar:crafting/fire_controller' }, 'minecraft:comparator', 'tfmg:circuit_board')
  event.replaceInput({ id: 'create_radar:crafting/monitor' }, 'create:brass_casing', 'tfmg:steel_casing')

  // Propulseur (Create Propulsion) : boîtier et pompes en acier, pour lier l'aviation au pétrole et à l'acier.
  event.replaceInput({ id: 'createpropulsion:crafting/thruster' }, 'create:brass_casing', 'tfmg:steel_casing')
  event.replaceInput({ id: 'createpropulsion:crafting/thruster' }, 'create:mechanical_pump', 'tfmg:steel_mechanical_pump')

  // Missiles guidés de Sable LRW : l'électronique de guidage passe par les tubes électroniques de Create.
  event.replaceInput({ mod: 'createarms' }, 'minecraft:redstone_torch', 'create:electron_tube')
})
