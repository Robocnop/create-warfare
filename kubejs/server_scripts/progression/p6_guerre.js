// Create Warfare — palier 6 : la guerre mécanisée passe par l'acier (4) et l'électronique (5).
// Superb Warfare avait sa propre industrie (laine, fer, lapis) sans lien avec Create ; les radars
// se montaient dès le laiton. Recettes d'origine relevées dans l'export du serveur de test (docs/AUDIT.md).
ServerEvents.recipes(event => {
  // --- Superb Warfare : composants communs à tous les véhicules ---
  event.remove({ id: 'superbwarfare:motor' })
  event.shaped('2x superbwarfare:motor', [' T ', 'BSR', 'BSR'], {
    T: 'create:electron_tube', B: 'createaddition:copper_spool', S: 'create:shaft', R: '#c:dusts/redstone'
  }).id('createwarfare:sbw/motor')

  event.remove({ id: 'superbwarfare:large_motor' })
  event.shaped('superbwarfare:large_motor', [' M ', 'ECR', 'ECR'], {
    M: 'tfmg:steel_mechanism', E: 'createaddition:electric_motor', C: '#c:storage_blocks/copper', R: '#c:storage_blocks/redstone'
  }).id('createwarfare:sbw/large_motor')

  // Roue : caoutchouc de TFMG (pétrole) sur jante en acier, au lieu de laine noire et de fer.
  event.remove({ id: 'superbwarfare:wheel' })
  event.shaped('2x superbwarfare:wheel', [' C ', 'CSC', ' C '], {
    C: 'tfmg:rubber_sheet', S: '#c:ingots/steel'
  }).id('createwarfare:sbw/wheel')

  // Noyau de canon : une vraie culasse de Create Big Cannons à la place du distributeur.
  event.replaceInput({ id: 'superbwarfare:cannon_core' }, 'minecraft:dispenser', 'createbigcannons:cast_iron_sliding_breech')
  // Module d'armement léger : mécanisme de précision à la place du distributeur.
  event.replaceInput({ id: 'superbwarfare:light_armament_module' }, 'minecraft:dispenser', 'create:precision_mechanism')
  // Autodirecteur et moteur de missile : électronique TFMG et acier.
  event.replaceInput({ id: 'superbwarfare:seeker' }, 'minecraft:comparator', 'tfmg:circuit_board')
  event.replaceInput({ id: 'superbwarfare:missile_engine' }, '#c:ingots/iron', '#c:ingots/steel')

  // Table d'assemblage de véhicules : la porte du palier.
  event.remove({ id: 'superbwarfare:vehicle_assembling_table' })
  event.shaped('superbwarfare:vehicle_assembling_table', ['PPP', 'MTC', 'SSS'], {
    P: '#c:plates/steel', M: 'tfmg:steel_mechanism', T: 'minecraft:smithing_table', C: 'tfmg:circuit_board', S: '#c:ingots/steel'
  }).id('createwarfare:sbw/vehicle_assembling_table')

  // --- Radars : circuit imprimé et acier (avant : tube électronique et tôle de fer, dès le laiton) ---
  event.replaceInput({ id: 'create_radar:crafting/radar_bearing' }, 'create:electron_tube', 'tfmg:circuit_board')
  event.replaceInput({ id: 'create_radar:crafting/radar_bearing' }, '#c:plates/iron', '#c:plates/steel')
  event.replaceInput({ id: 'create_radar:crafting/radar_receiver' }, 'create:electron_tube', 'tfmg:circuit_board')

  // --- Create Missiles : pas de tir en acier, électronique TFMG ---
  event.replaceInput({ id: 'createmissiles:mechanical_crafting/launch_pad' }, '#c:plates/iron', '#c:plates/steel')
  event.replaceInput({ id: 'createmissiles:mechanical_crafting/launch_pad' }, 'create:electron_tube', 'tfmg:circuit_board')

  // --- Appel de frappe aérienne : il faut une conduite de tir (chaîne radar) et de l'électronique ---
  event.remove({ id: 'projectexplosive:airstrike_caller' })
  event.shaped('projectexplosive:airstrike_caller', ['BCB', 'PFP', 'PDP'], {
    B: 'minecraft:blaze_rod', C: 'tfmg:circuit_board', P: '#c:plates/steel',
    F: 'create_radar:fire_controller', D: '#c:gems/diamond'
  }).id('createwarfare:airstrike_caller')
})
