// Outil d'audit, PAS un script du pack : à copier dans server-test/kubejs/server_scripts/ le temps d'un test
// (tools/check_progression.sh le fait et le retire). Il exporte dans server-test/kubejs/export/ :
//   cw_recipes.json : toutes les recettes réellement chargées (après KubeJS et Almost Unified), en JSON ;
//   cw_loot.json    : toutes les tables de butin ;
//   cw_items.json   : tous les objets enregistrés ;
//   cw_tags.json    : tous les tags d'objets et leur contenu.
// tools/check_progression.py lit ces fichiers.
ServerEvents.loaded(event => {
  var JsonOps = Java.loadClass('com.mojang.serialization.JsonOps')
  var Recipe = Java.loadClass('net.minecraft.world.item.crafting.Recipe')
  var LootTable = Java.loadClass('net.minecraft.world.level.storage.loot.LootTable')
  var Registries = Java.loadClass('net.minecraft.core.registries.Registries')
  var BuiltIn = Java.loadClass('net.minecraft.core.registries.BuiltInRegistries')
  var JsonObject = Java.loadClass('com.google.gson.JsonObject')
  var JsonArray = Java.loadClass('com.google.gson.JsonArray')
  var ops = event.server.registryAccess().createSerializationContext(JsonOps.INSTANCE)

  var out = new JsonObject()
  event.server.getRecipeManager().getRecipes().forEach(h => {
    try { out.add(String(h.id()), Recipe.CODEC.encodeStart(ops, h.value()).getOrThrow()) }
    catch (e) { out.addProperty(String(h.id()), 'ERR ' + e) }
  })
  JsonIO.write('kubejs/export/cw_recipes.json', out)

  var loot = new JsonObject()
  try {
    var reg = event.server.reloadableRegistries().get().registryOrThrow(Registries.LOOT_TABLE)
    reg.entrySet().forEach(en => {
      try { loot.add(String(en.getKey().location()), LootTable.DIRECT_CODEC.encodeStart(ops, en.getValue()).getOrThrow()) }
      catch (e) { loot.addProperty(String(en.getKey().location()), 'ERR') }
    })
  } catch (e) { console.error('CW-DUMP butin : ' + e) }
  JsonIO.write('kubejs/export/cw_loot.json', loot)

  var items = new JsonArray()
  BuiltIn.ITEM.keySet().forEach(k => items.add(String(k)))
  var o2 = new JsonObject()
  o2.add('items', items)
  JsonIO.write('kubejs/export/cw_items.json', o2)

  var tags = new JsonObject()
  try {
    BuiltIn.ITEM.getTags().forEach(pair => {
      var arr = new JsonArray()
      pair.getSecond().forEach(holder => arr.add(String(BuiltIn.ITEM.getKey(holder.value()))))
      tags.add(String(pair.getFirst().location()), arr)
    })
  } catch (e) { console.error('CW-DUMP tags : ' + e) }
  JsonIO.write('kubejs/export/cw_tags.json', tags)
  console.info('CW-DUMP terminé')
})
