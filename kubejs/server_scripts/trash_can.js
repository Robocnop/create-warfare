// Poubelle de Kaleidoscope Cookery : en sautant dessus, un joueur « entre » dedans
// (TrashCanBlock.fallOn crée un siège invisible kaleidoscope_cookery:sit et l'y fait monter).
// Le rendu de ce joueur caché fait planter le client de ceux qui le regardent
// (« Pose stack not empty », 13/11). On refuse donc la montée sur un siège posé dans une poubelle ;
// les chaises et tabourets du mod, qui utilisent le même siège, ne sont pas touchés.
NativeEvents.onEvent('net.neoforged.neoforge.event.entity.EntityMountEvent', event => {
  if (!event.isMounting()) return
  var seat = event.getEntityBeingMounted()
  if (seat == null || String(seat.getEncodeId()) !== 'kaleidoscope_cookery:sit') return
  var level = seat.level
  var pos = seat.blockPosition()
  var id = String(level.getBlockState(pos).getBlock().getId())
  if (id !== 'kaleidoscope_cookery:trash_can') {
    id = String(level.getBlockState(pos.below()).getBlock().getId())
    if (id !== 'kaleidoscope_cookery:trash_can') return
  }
  event.setCanceled(true)
  seat.discard()
})
