// Create Warfare : petites réparations de recettes, v1.8.6.

// Garnished : le tailleur de pierre acceptait les dalles (tag « stone_types ») et rendait un bloc entier par dalle.
// Un bloc donnant deux dalles, les pierres décoratives se dupliquaient à l'infini. Les dalles sortent du tag.
const GARNISHED_SLABS = {
  "garnished:stone_types/abyssal_stone": [
    "garnished:abyssal_stone_brick_slab",
    "garnished:abyssal_stone_slab",
    "garnished:cut_abyssal_stone_slab",
    "garnished:polished_abyssal_stone_slab",
    "garnished:small_abyssal_stone_brick_slab",
    "garnished:smooth_abyssal_stone_slab"
  ],
  "garnished:stone_types/carnotite": [
    "garnished:carnotite_brick_slab",
    "garnished:carnotite_slab",
    "garnished:cut_carnotite_slab",
    "garnished:polished_carnotite_slab",
    "garnished:small_carnotite_brick_slab",
    "garnished:smooth_carnotite_slab"
  ],
  "garnished:stone_types/dragon_stone": [
    "garnished:cut_dragon_stone_slab",
    "garnished:dragon_stone_brick_slab",
    "garnished:dragon_stone_slab",
    "garnished:polished_dragon_stone_slab",
    "garnished:small_dragon_stone_brick_slab",
    "garnished:smooth_dragon_stone_slab"
  ],
  "garnished:stone_types/ritualistic_stone": [
    "garnished:cut_ritualistic_stone_slab",
    "garnished:polished_ritualistic_stone_slab",
    "garnished:ritualistic_stone_brick_slab",
    "garnished:ritualistic_stone_slab",
    "garnished:small_ritualistic_stone_brick_slab",
    "garnished:smooth_ritualistic_stone_slab"
  ],
  "garnished:stone_types/unstable_stone": [
    "garnished:cut_unstable_stone_slab",
    "garnished:polished_unstable_stone_slab",
    "garnished:small_unstable_stone_brick_slab",
    "garnished:smooth_unstable_stone_slab",
    "garnished:unstable_stone_brick_slab",
    "garnished:unstable_stone_slab"
  ],
  "garnished:stone_types/zultanite": [
    "garnished:black_zultanite_brick_slab",
    "garnished:black_zultanite_slab",
    "garnished:blue_zultanite_brick_slab",
    "garnished:blue_zultanite_slab",
    "garnished:brown_zultanite_brick_slab",
    "garnished:brown_zultanite_slab",
    "garnished:cut_black_zultanite_slab",
    "garnished:cut_blue_zultanite_slab",
    "garnished:cut_brown_zultanite_slab",
    "garnished:cut_cyan_zultanite_slab",
    "garnished:cut_gray_zultanite_slab",
    "garnished:cut_green_zultanite_slab",
    "garnished:cut_light_blue_zultanite_slab",
    "garnished:cut_light_gray_zultanite_slab",
    "garnished:cut_lime_zultanite_slab",
    "garnished:cut_magenta_zultanite_slab",
    "garnished:cut_orange_zultanite_slab",
    "garnished:cut_pink_zultanite_slab",
    "garnished:cut_purple_zultanite_slab",
    "garnished:cut_red_zultanite_slab",
    "garnished:cut_white_zultanite_slab",
    "garnished:cut_yellow_zultanite_slab",
    "garnished:cut_zultanite_slab",
    "garnished:cyan_zultanite_brick_slab",
    "garnished:cyan_zultanite_slab",
    "garnished:gray_zultanite_brick_slab",
    "garnished:gray_zultanite_slab",
    "garnished:green_zultanite_brick_slab",
    "garnished:green_zultanite_slab",
    "garnished:light_blue_zultanite_brick_slab",
    "garnished:light_blue_zultanite_slab",
    "garnished:light_gray_zultanite_brick_slab",
    "garnished:light_gray_zultanite_slab",
    "garnished:lime_zultanite_brick_slab",
    "garnished:lime_zultanite_slab",
    "garnished:magenta_zultanite_brick_slab",
    "garnished:magenta_zultanite_slab",
    "garnished:orange_zultanite_brick_slab",
    "garnished:orange_zultanite_slab",
    "garnished:pink_zultanite_brick_slab",
    "garnished:pink_zultanite_slab",
    "garnished:polished_black_zultanite_slab",
    "garnished:polished_blue_zultanite_slab",
    "garnished:polished_brown_zultanite_slab",
    "garnished:polished_cyan_zultanite_slab",
    "garnished:polished_gray_zultanite_slab",
    "garnished:polished_green_zultanite_slab",
    "garnished:polished_light_blue_zultanite_slab",
    "garnished:polished_light_gray_zultanite_slab",
    "garnished:polished_lime_zultanite_slab",
    "garnished:polished_magenta_zultanite_slab",
    "garnished:polished_orange_zultanite_slab",
    "garnished:polished_pink_zultanite_slab",
    "garnished:polished_purple_zultanite_slab",
    "garnished:polished_red_zultanite_slab",
    "garnished:polished_white_zultanite_slab",
    "garnished:polished_yellow_zultanite_slab",
    "garnished:polished_zultanite_slab",
    "garnished:purple_zultanite_brick_slab",
    "garnished:purple_zultanite_slab",
    "garnished:red_zultanite_brick_slab",
    "garnished:red_zultanite_slab",
    "garnished:small_black_zultanite_brick_slab",
    "garnished:small_blue_zultanite_brick_slab",
    "garnished:small_brown_zultanite_brick_slab",
    "garnished:small_cyan_zultanite_brick_slab",
    "garnished:small_gray_zultanite_brick_slab",
    "garnished:small_green_zultanite_brick_slab",
    "garnished:small_light_blue_zultanite_brick_slab",
    "garnished:small_light_gray_zultanite_brick_slab",
    "garnished:small_lime_zultanite_brick_slab",
    "garnished:small_magenta_zultanite_brick_slab",
    "garnished:small_orange_zultanite_brick_slab",
    "garnished:small_pink_zultanite_brick_slab",
    "garnished:small_purple_zultanite_brick_slab",
    "garnished:small_red_zultanite_brick_slab",
    "garnished:small_white_zultanite_brick_slab",
    "garnished:small_yellow_zultanite_brick_slab",
    "garnished:small_zultanite_brick_slab",
    "garnished:smooth_black_zultanite_slab",
    "garnished:smooth_blue_zultanite_slab",
    "garnished:smooth_brown_zultanite_slab",
    "garnished:smooth_cyan_zultanite_slab",
    "garnished:smooth_gray_zultanite_slab",
    "garnished:smooth_green_zultanite_slab",
    "garnished:smooth_light_blue_zultanite_slab",
    "garnished:smooth_light_gray_zultanite_slab",
    "garnished:smooth_lime_zultanite_slab",
    "garnished:smooth_magenta_zultanite_slab",
    "garnished:smooth_orange_zultanite_slab",
    "garnished:smooth_pink_zultanite_slab",
    "garnished:smooth_purple_zultanite_slab",
    "garnished:smooth_red_zultanite_slab",
    "garnished:smooth_white_zultanite_slab",
    "garnished:smooth_yellow_zultanite_slab",
    "garnished:smooth_zultanite_slab",
    "garnished:white_zultanite_brick_slab",
    "garnished:white_zultanite_slab",
    "garnished:yellow_zultanite_brick_slab",
    "garnished:yellow_zultanite_slab",
    "garnished:zultanite_brick_slab",
    "garnished:zultanite_slab"
  ],
  "garnished:stone_types/zultanite/basic": [
    "garnished:cut_zultanite_slab",
    "garnished:polished_zultanite_slab",
    "garnished:small_zultanite_brick_slab",
    "garnished:smooth_zultanite_slab",
    "garnished:zultanite_brick_slab",
    "garnished:zultanite_slab"
  ],
  "garnished:stone_types/zultanite/black": [
    "garnished:black_zultanite_brick_slab",
    "garnished:black_zultanite_slab",
    "garnished:cut_black_zultanite_slab",
    "garnished:polished_black_zultanite_slab",
    "garnished:small_black_zultanite_brick_slab",
    "garnished:smooth_black_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/blue": [
    "garnished:blue_zultanite_brick_slab",
    "garnished:blue_zultanite_slab",
    "garnished:cut_blue_zultanite_slab",
    "garnished:polished_blue_zultanite_slab",
    "garnished:small_blue_zultanite_brick_slab",
    "garnished:smooth_blue_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/brown": [
    "garnished:brown_zultanite_brick_slab",
    "garnished:brown_zultanite_slab",
    "garnished:cut_brown_zultanite_slab",
    "garnished:polished_brown_zultanite_slab",
    "garnished:small_brown_zultanite_brick_slab",
    "garnished:smooth_brown_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/cyan": [
    "garnished:cut_cyan_zultanite_slab",
    "garnished:cyan_zultanite_brick_slab",
    "garnished:cyan_zultanite_slab",
    "garnished:polished_cyan_zultanite_slab",
    "garnished:small_cyan_zultanite_brick_slab",
    "garnished:smooth_cyan_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/gray": [
    "garnished:cut_gray_zultanite_slab",
    "garnished:gray_zultanite_brick_slab",
    "garnished:gray_zultanite_slab",
    "garnished:polished_gray_zultanite_slab",
    "garnished:small_gray_zultanite_brick_slab",
    "garnished:smooth_gray_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/green": [
    "garnished:cut_green_zultanite_slab",
    "garnished:green_zultanite_brick_slab",
    "garnished:green_zultanite_slab",
    "garnished:polished_green_zultanite_slab",
    "garnished:small_green_zultanite_brick_slab",
    "garnished:smooth_green_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/light_blue": [
    "garnished:cut_light_blue_zultanite_slab",
    "garnished:light_blue_zultanite_brick_slab",
    "garnished:light_blue_zultanite_slab",
    "garnished:polished_light_blue_zultanite_slab",
    "garnished:small_light_blue_zultanite_brick_slab",
    "garnished:smooth_light_blue_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/light_gray": [
    "garnished:cut_light_gray_zultanite_slab",
    "garnished:light_gray_zultanite_brick_slab",
    "garnished:light_gray_zultanite_slab",
    "garnished:polished_light_gray_zultanite_slab",
    "garnished:small_light_gray_zultanite_brick_slab",
    "garnished:smooth_light_gray_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/lime": [
    "garnished:cut_lime_zultanite_slab",
    "garnished:lime_zultanite_brick_slab",
    "garnished:lime_zultanite_slab",
    "garnished:polished_lime_zultanite_slab",
    "garnished:small_lime_zultanite_brick_slab",
    "garnished:smooth_lime_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/magenta": [
    "garnished:cut_magenta_zultanite_slab",
    "garnished:magenta_zultanite_brick_slab",
    "garnished:magenta_zultanite_slab",
    "garnished:polished_magenta_zultanite_slab",
    "garnished:small_magenta_zultanite_brick_slab",
    "garnished:smooth_magenta_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/orange": [
    "garnished:cut_orange_zultanite_slab",
    "garnished:orange_zultanite_brick_slab",
    "garnished:orange_zultanite_slab",
    "garnished:polished_orange_zultanite_slab",
    "garnished:small_orange_zultanite_brick_slab",
    "garnished:smooth_orange_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/pink": [
    "garnished:cut_pink_zultanite_slab",
    "garnished:pink_zultanite_brick_slab",
    "garnished:pink_zultanite_slab",
    "garnished:polished_pink_zultanite_slab",
    "garnished:small_pink_zultanite_brick_slab",
    "garnished:smooth_pink_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/purple": [
    "garnished:cut_purple_zultanite_slab",
    "garnished:polished_purple_zultanite_slab",
    "garnished:purple_zultanite_brick_slab",
    "garnished:purple_zultanite_slab",
    "garnished:small_purple_zultanite_brick_slab",
    "garnished:smooth_purple_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/red": [
    "garnished:cut_red_zultanite_slab",
    "garnished:polished_red_zultanite_slab",
    "garnished:red_zultanite_brick_slab",
    "garnished:red_zultanite_slab",
    "garnished:small_red_zultanite_brick_slab",
    "garnished:smooth_red_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/white": [
    "garnished:cut_white_zultanite_slab",
    "garnished:polished_white_zultanite_slab",
    "garnished:small_white_zultanite_brick_slab",
    "garnished:smooth_white_zultanite_slab",
    "garnished:white_zultanite_brick_slab",
    "garnished:white_zultanite_slab"
  ],
  "garnished:stone_types/zultanite/yellow": [
    "garnished:cut_yellow_zultanite_slab",
    "garnished:polished_yellow_zultanite_slab",
    "garnished:small_yellow_zultanite_brick_slab",
    "garnished:smooth_yellow_zultanite_slab",
    "garnished:yellow_zultanite_brick_slab",
    "garnished:yellow_zultanite_slab"
  ]
}

ServerEvents.tags('item', event => {
  Object.keys(GARNISHED_SLABS).forEach(tag => event.remove(tag, GARNISHED_SLABS[tag]))
})

ServerEvents.recipes(event => {
  // Cuffed : la clé des fers demandait le tag « forge:cobblestone », vide en 1.21 (clé infabricable).
  event.remove({ id: 'cuffed:shackles_key' })
  event.shaped('cuffed:shackles_key', ['N ', 'NN', 'N '], { N: '#c:cobblestones' }).id('cuffed:shackles_key')
  // Cuffed : menottes en fourrure, sans recette dans le mod.
  event.shapeless('cuffed:fuzzy_handcuffs', ['cuffed:handcuffs', 'minecraft:pink_wool']).id('createwarfare:fixes/fuzzy_handcuffs')
  // Create Radars : fusée de proximité avancée, sans recette dans le mod.
  event.shapeless('create_radar:advanced_proxy_fuze', [
    'createbigcannons:proximity_fuze', 'create:electron_tube', 'tfmg:circuit_board'
  ]).id('createwarfare:fixes/advanced_proxy_fuze')
})
