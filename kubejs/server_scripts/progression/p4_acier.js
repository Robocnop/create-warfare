// Create Warfare — porte du palier 4 : l'acier ne sort que du haut fourneau de TFMG.
// Avant la 2.0.0, quatre voies le contournaient (relevé : docs/AUDIT.md) :
//  - Create Nuclear : fer + poussière de charbon au mixeur ;
//  - Create: Gunsmithing : fer + poussière de charbon de bois au mixeur ;
//  - Superb Warfare : poudre fer-coke au haut fourneau vanilla ;
//  - Overgeared : acier brut de la forge, refroidi en lingot que Almost Unified changeait en acier TFMG.

// L'acier de la forge reste un objet à part (« acier forgé ») : il sert aux outils d'Overgeared,
// pas à l'industrie. Sorti du tag c:ingots/steel, il n'est plus unifié ni accepté par les recettes d'acier.
ServerEvents.tags('item', event => {
  event.remove('c:ingots/steel', 'overgeared:steel_ingot')
})

ServerEvents.recipes(event => {
  ;[
    'createnuclear:mixing/steel',
    'cgs:mixing/steel_ingot',
    'superbwarfare:steel_ingot_blasting',
    'superbwarfare:coal_iron_powder'
  ].forEach(id => event.remove({ id: id }))

  // Recyclage des outils en acier forgé : il rendait des pépites d'acier industriel.
  event.remove({ id: /^overgeared:steel_nugget_from_blasting_/ })
  // Conversions entre acier forgé et acier industriel (pépites, bloc) : autant de passerelles à fermer.
  ;[
    'overgeared:steel_nugget_from_ingot',
    'overgeared:steel_block',
    'overgeared:steel_ingot_from_block',
    'overgeared:steel_ingot_from_nuggets'
  ].forEach(id => event.remove({ id: id }))

  // Overgeared fait du briquet et du chaudron des objets en acier : ils restent accessibles au début.
  event.shapeless('minecraft:flint_and_steel', ['minecraft:flint', 'overgeared:steel_ingot']).id('createwarfare:forge/flint_and_steel')
  event.shaped('minecraft:cauldron', ['P P', 'P P', 'PPP'], { P: '#c:plates/iron' }).id('createwarfare:forge/cauldron')

  // Un lingot d'acier forgé refroidi se réchauffe pour retourner à l'enclume.
  event.blasting('overgeared:heated_steel_ingot', 'overgeared:steel_ingot').id('createwarfare:forge/heated_steel_from_forged')
})
