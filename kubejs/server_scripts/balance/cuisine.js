// Create Warfare : farine et pâte communes à Create, Farmer's Delight et MrCrayfish's Furniture (1.8.1).
// Sans ces liens, la farine du moulin de Create ne servait pas aux pizzas de Refurbished, et inversement.

ServerEvents.tags('item', event => {
  // Farine : celle de Refurbished compte comme celle de Create (moulin, mélangeur)
  event.add('c:flours/wheat', 'refurbished_furniture:wheat_flour')
  // Pâte : les trois pâtes de blé s'échangent (Create, Farmer's Delight, Refurbished)
  event.add('c:foods/dough/wheat', ['create:dough', 'refurbished_furniture:dough'])
  event.add('c:foods/dough', ['create:dough', 'refurbished_furniture:dough'])
})

ServerEvents.recipes(event => {
  // Pâte de Refurbished : n'importe quelle farine de blé (la farine du moulin de Create en particulier)
  event.replaceInput({ id: 'refurbished_furniture:dough' }, 'refurbished_furniture:wheat_flour', '#c:flours/wheat')
  // Pizzas (type de recette propre à Refurbished, que replaceInput ne modifie pas) :
  // recettes remplacées par kubejs/data/refurbished_furniture/recipe/combining/raw_*_pizza.json
})
