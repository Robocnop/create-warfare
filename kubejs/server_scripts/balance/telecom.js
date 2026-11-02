// Create Warfare : Mobile Networks passe par Create (v1.8.2).
// Recettes d'origine (data/mobilenetworks/recipe/, toutes vanilla : fer, cuivre, redstone) réécrites avec le même id.
// Le tournevis garde sa recette. La puce Wi-Fi n'a pas de recette dans le mod.

ServerEvents.recipes(event => {
  // Téléphone : tube électronique et fil de cuivre au lieu de la redstone.
  event.remove({ id: 'mobilenetworks:mobile_phone' })
  event.shaped('mobilenetworks:mobile_phone', ['SWS', 'SGS', 'SES'], {
    S: 'create:iron_sheet',
    W: '#c:wires/copper',
    G: '#c:glass_panes/colorless',
    E: 'create:electron_tube'
  }).id('mobilenetworks:mobile_phone')

  // Antennes : plaques de cuivre, fil de cuivre et tube électronique ; le métal du sommet donne la portée.
  const antennas = [
    ['copper_antenna', '#c:ingots/copper'],
    ['iron_antenna', '#c:ingots/iron'],
    ['diamond_antenna', '#c:gems/diamond']
  ]
  antennas.forEach(a => {
    event.remove({ id: 'mobilenetworks:' + a[0] })
    event.shaped('mobilenetworks:' + a[0], [' T ', 'WEW', ' S '], {
      T: a[1],
      W: '#c:wires/copper',
      E: 'create:electron_tube',
      S: 'create:copper_sheet'
    }).id('mobilenetworks:' + a[0])
  })

  // Serveur du réseau : mécanisme de précision au centre.
  event.remove({ id: 'mobilenetworks:server' })
  event.shaped('mobilenetworks:server', ['SES', 'WPW', 'SES'], {
    S: 'create:iron_sheet',
    E: 'create:electron_tube',
    W: '#c:wires/copper',
    P: 'create:precision_mechanism'
  }).id('mobilenetworks:server')

  // Routeur Wi-Fi.
  event.remove({ id: 'mobilenetworks:wifi_router' })
  event.shaped('mobilenetworks:wifi_router', [' W ', 'SES', 'CCC'], {
    W: '#c:wires/copper',
    S: 'create:iron_sheet',
    E: 'create:electron_tube',
    C: 'create:copper_sheet'
  }).id('mobilenetworks:wifi_router')
})
