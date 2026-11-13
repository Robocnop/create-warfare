// Create Warfare — porte du palier 5 : produire de l'électricité demande de l'acier.
// Le pack a quatre réseaux électriques (Crafts & Additions, New Age, Power Grid, TFMG). Seul celui de TFMG
// demandait de l'acier : les trois autres se montaient dès l'andésite. On verrouille la pièce d'entrée de chacun.
ServerEvents.recipes(event => {
  // Crafts & Additions : l'alternateur (les alternateurs de Better Motors en dérivent).
  event.replaceInput({ id: 'createaddition:mechanical_crafting/alternator' }, '#c:plates/iron', '#c:plates/steel')
  // New Age : la bobine de génératrice.
  event.replaceInput({ id: 'create_new_age:shaped/generator_coil' }, 'create:andesite_alloy_block', '#c:storage_blocks/steel')
  // Power Grid : le boîtier conducteur, base de toutes ses machines.
  event.replaceInput({ id: 'powergrid:item_application/conductive_casing' }, '#c:ingots/zinc', '#c:ingots/steel')
})
