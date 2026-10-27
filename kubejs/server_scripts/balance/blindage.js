// Create Warfare : blindage de Neo Warfare (chapitre 7).
// Le jar n'a aucune recette pour l'acier au carbone ni pour l'ERA : les quêtes du chapitre 7 étaient bloquées.
// L'acier au carbone vient de l'acier TFMG (chapitre 4), l'ERA des charges de poudre de CBC (chapitre 5).

ServerEvents.recipes(event => {
  const CW = 'cbcmodernwarfare'

  // Lingot : acier + coke, mélangés sous brûleur.
  event.custom({
    type: 'create:mixing',
    heat_requirement: 'heated',
    ingredients: [{ tag: 'c:ingots/steel' }, { item: 'tfmg:coal_coke' }],
    results: [{ id: `${CW}:carbon_steel_ingot` }]
  })
  event.shapeless(`9x ${CW}:carbon_steel_nugget`, [`${CW}:carbon_steel_ingot`])
  event.shaped(`${CW}:carbon_steel_ingot`, ['NNN', 'NNN', 'NNN'], { N: `${CW}:carbon_steel_nugget` })

  // Bloc de blindage : 4 lingots, sans recette inverse (sinon 4 lingots en donneraient 9).
  event.shaped(`${CW}:carbon_steel_block`, ['II', 'II'], { I: `${CW}:carbon_steel_ingot` })
  event.stonecutting(`2x ${CW}:carbon_steel_slab`, `${CW}:carbon_steel_block`)
  event.stonecutting(`${CW}:carbon_steel_stairs`, `${CW}:carbon_steel_block`)

  // ERA : une charge de poudre déployée sur un bloc d'acier au carbone. Variantes vert et sable à la teinture.
  event.custom({
    type: 'create:deploying',
    ingredients: [{ item: `${CW}:carbon_steel_block` }, { item: 'createbigcannons:powder_charge' }],
    results: [{ id: `${CW}:era_block` }]
  })
  event.shapeless(`${CW}:green_era_block`, [`${CW}:era_block`, 'minecraft:green_dye'])
  event.shapeless(`${CW}:tan_era_block`, [`${CW}:era_block`, 'minecraft:brown_dye'])
  ;['', 'green_', 'tan_'].forEach(color => {
    const block = `${CW}:${color}era_block`
    event.stonecutting(`2x ${CW}:${color}era_slab`, block)
    event.stonecutting(`${CW}:${color}era_stairs`, block)
  })
})
