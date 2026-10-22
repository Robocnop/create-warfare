// Create Warfare : objet « Codes de lancement ».
// Non craftable : il ne s'obtient que par la quête répétable du chapitre 10 (Armes de destruction massive).
// Il est consommé à la pose d'un ICBM (voir server_scripts/balance/military.js)
// et entre dans la recette des missiles longue portée de Sable LRW.

StartupEvents.registry('item', event => {
  event.create('launch_codes')
    .texture('minecraft:item/paper')
    .maxStackSize(16)
    .rarity('epic')
    .glow(true)
})
