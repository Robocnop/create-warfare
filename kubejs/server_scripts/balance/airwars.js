// Create Warfare : objets de Create: The Air Wars sans recette dans le jar, v1.8.6.
// Ordinateur, manette et drone FPV, lanceur antiaérien, roquette C-75, bombes FAB et missile Kh-101.
// Recettes écrites avec les composants du mod (titane, puce, fils, explosifs), comme ses propres missiles.

ServerEvents.recipes(event => {
  const AW = 'create_the_air_wars:'
  const id = name => 'createwarfare:airwars/' + name
  const K = {
    S: 'create_the_air_wars:titaniumsheet',
    I: 'create_the_air_wars:titaniumingot',
    C: 'create_the_air_wars:chip',
    W: 'create_the_air_wars:wires',
    D: 'create_the_air_wars:display',
    A: 'create_the_air_wars:activator',
    N: 'create_the_air_wars:antenna',
    V: 'create_the_air_wars:camera',
    X: 'create_the_air_wars:explosive',
    Y: 'create_the_air_wars:bigexplzv',
    Z: 'create_the_air_wars:miniexplzv',
    R: 'create_the_air_wars:rocket_engine',
    P: 'create:propeller',
    M: 'create:mechanical_bearing'
  }
  const keys = pattern => {
    const used = {}
    pattern.join('').split('').forEach(c => {
      if (c != ' ') used[c] = K[c]
    })
    return used
  }
  const shaped = (out, pattern) => {
    event.shaped(AW + out, pattern, keys(pattern)).id(id(out))
  }
  const mechanical = (out, pattern) => {
    const k = keys(pattern)
    const json = {}
    Object.keys(k).forEach(c => {
      json[c] = { item: k[c] }
    })
    event.custom({
      type: 'create:mechanical_crafting',
      accept_mirrored: true,
      category: 'misc',
      pattern: pattern,
      key: json,
      result: { id: AW + out, count: 1 }
    }).id(id(out))
  }

  shaped('computer', ['SSS', 'CDC', 'SWS'])
  shaped('drone_controller', [' N ', 'SCS', ' W '])
  shaped('fpv_drone', ['PVP', ' C ', 'PZP'])
  shaped('anti_aircraft_launcher', ['S S', 'SCS', 'SMS'])
  mechanical('c75', [' A ', 'SXS', 'SRS'])
  mechanical('fab_1500', [' A ', 'SYS', 'SYS', ' I '])
  mechanical('fab_3000', [' A ', 'SYS', 'SYS', 'SYS', ' I '])
  mechanical('kh101', [' A ', 'SYS', 'SCS', 'SWS', 'IRI'])
})
