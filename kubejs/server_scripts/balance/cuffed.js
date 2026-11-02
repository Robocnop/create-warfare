// Create Warfare : blocs renforcés de Cuffed plus chers (v1.8.2, choix de Robo).
// Ils ont une résistance de 2400 (obsidienne : 1200) et résistent aux Mk 82/84 de Project: Explosive (test du 02/11).
// D'origine : 8 pierres + 1 fer → 8 blocs. Ici : un lingot d'acier TFMG par bloc environ.
// Dalles, escaliers et pierre sculptée partent de la pierre renforcée : leurs recettes ne changent pas.

ServerEvents.recipes(event => {
  const S = '#c:ingots/steel'

  const swap = (id, pattern, keys, count) => {
    event.remove({ id: 'cuffed:' + id })
    event.shaped(Item.of('cuffed:' + id, count), pattern, keys).id('cuffed:' + id)
  }

  swap('reinforced_stone', ['BSB', 'SBS', 'BSB'], { B: 'minecraft:stone', S: S }, 4)
  swap('reinforced_smooth_stone', ['BSB', 'SBS', 'BSB'], { B: 'minecraft:smooth_stone', S: S }, 4)
  swap('reinforced_bars', ['SBS', 'SBS'], { B: 'minecraft:iron_bars', S: S }, 4)
  swap('reinforced_bars_gap', ['S S', ' B ', 'S S'], { B: 'minecraft:iron_bars', S: S }, 2)
  swap('reinforced_lamp', [' S ', 'SGS', ' S '], { G: 'minecraft:glowstone', S: S }, 2)
  swap('cell_door', ['RR', 'RS', 'RR'], { R: 'cuffed:reinforced_bars', S: S }, 2)
  swap('bunk', ['CSC', 'SBS'], { C: 'minecraft:chain', S: S, B: 'minecraft:white_bed' }, 1)
})
