# Create Warfare — liste des mods

Minecraft **1.21.1** · NeoForge **21.1.264** · Create **6.0.10** · 189 mods et packs.
Tableau généré depuis `mods/*.pw.toml` (versions figées : 2026-10-19).

## Notes de compatibilité (testé le 2026-10-19 sur serveur dédié : démarrage OK, 122 quêtes chargées)
- **Écartés** : Mi Bombo, Create: Steel Armor Blocks, SSRD, StickyWheels (licence « All Rights Reserved » et absents
  de CurseForge, donc impossibles à mettre dans le zip) · CBC Ballistics NeoForge Port (absent de CurseForge) et son
  correctif Sable · Embeddium (doublon de Sodium).
- **Absents en 1.21.1 NeoForge** : CBC Nukes, Create: Propulsion (remplacé par Create Propulsion: Simulated),
  Create Interactive (remplacé par Aeronautics/Sable), Create Misc & Things, Create Unlimited.
- **Erreurs connues, sans gravité** (le serveur démarre) : 4 recettes de CBC Advanced Technologies (roquettes),
  3 recettes de CBC Terminal Ballistics, 2 de Create Factory Logistics, 1 de Create Deco, 1 de Create Additional Logistics
  ne se chargent pas (fichiers de recette des addons pas à jour) ; environ 550 tables de loot de Steam 'n' Rails pour des bois de mods absents.
- **Project: Explosive** est en bêta (0.7.2) : les grosses explosions nucléaires peuvent faire ramer le serveur.
- **Almost Unified** fusionne les doublons (acier, plomb, fonte, fil de cuivre...) : priorité Create > TFMG > CBC > Create Nuclear > C&A > New Age (`config/almostunified/unification/materials.json`).
- **Create Track Map** publie une carte web des trains sur le port **3876** du serveur (à ouvrir dans le pare-feu si vous la voulez).
- **Overgeared** (bêta 1.21.1) : outils et armures se forgent sur une enclume (mini-jeu). Ses recettes ne s'affichent que dans **EMI**, d'où EMI en plus de JEI.
- **Explosion Overhaul** (alpha 1.21.1) : effets d'explosion (lumières qui clignotent, onde de choc, sons selon la distance). `enableAsyncCrater = false` pour que les explosions cassent les engins Aeronautics/Sable ; les bombes de Project: Explosive sont laissées à leur propre système (`ExplosionSourceBlacklist.json`).
- **Exploration** : Terralith (biomes), YUNG's (donjons, mines, forteresses…), Towns and Towers, Explorify, Create Structures Arise / Rustic Structures / Aeronautics Structures, Friends&Foes, Mowzie's Mobs, Deeper and Darker, Ore Zombies. Ils ne s'appliquent qu'aux **nouveaux chunks** : à installer avant de créer le monde.
- **Options par défaut** (Default Options) : français, touches de déplacement physiques (ZQSD sur AZERTY), distance 10. Appliquées seulement au premier lancement, jamais par-dessus les réglages d'un joueur.
- **Visuels** : Complementary Reimagined (shader, compatible Iris + Distant Horizons, désactivé par défaut : Options vidéo → Shaders) ; Fresh Animations (activé par défaut, nécessite EMF + ETF inclus). Packs Create-style (Sophisticated Backpacks, Tom's Storage, Create Ultra PBR) écartés : licence « All Rights Reserved ».
- **Railways Untold** (bêta) : génère un réseau de rails Create infini, avec gares dans les villages, à la création des
  chunks (donc aussi pendant la pré-génération Chunky). Sur un monde existant, seuls les nouveaux chunks en ont ; le
  « Track Generator » de **Railways Additions** en pose ailleurs. Railways Additions ajoute aussi le régulateur de vitesse
  des trains et le relieur de gares.
- **Discord** : Simple Discord Rich Presence (client seul).
- **1.5.0, non testée en jeu** : Kaleidoscope Cookery (cuisine ; son moulin accepte le moulin de Create ; cuisines dans les
  villages des nouveaux chunks), Durability Tooltip (durabilité dans l'info-bulle), Inventory Profiles Next (boutons de tri
  des coffres, tri par nom réglé dans `config/inventoryprofilesnext/inventoryprofiles.json`). Les deux derniers sont client seul.
- **SecurityCraft** (1.4.0) : les blocs renforcés sont cassables par les obus CBC (CBC Security Breach, points de vie par
  matériau), Superb Warfare et la TNT (SC-SuperB Bridge). **Pas par les bombes et ICBM de Project: Explosive**, qui sautent
  les blocs de dureté -1. **C'est voulu** : les blocs renforcés servent de **bunkers anti-nucléaires**
  (mais pas contre l'artillerie, Superb Warfare ni la TNT).
  Propriété partagée avec l'équipe FTB (`enable_team_ownership`), caméras sans chargement de chunks.
- **CC: Tweaked** (1.4.0) : CC:CBC (pilotage des canons), Create: Avionics (blocs Aeronautics/Simulated/Offroad),
  Create: CC Better Recipes (recettes via Create). Create 6 gère CC: Tweaked de base (trains, gares, compteurs).
- `allow-flight=true` est obligatoire sur le serveur (avions, jetpacks), sinon les joueurs sont expulsés.

| Mod | Fichier | Côté | Source |
|---|---|---|---|
| Almost Unified | `almostunified-neoforge-1.21.1-1.4.2.jar` | client + serveur | CurseForge |
| AppleSkin | `appleskin-neoforge-mc1.21-3.0.9.jar` | client + serveur | CurseForge |
| Architectury API | `architectury-13.0.11-neoforge.jar` | client + serveur | CurseForge |
| AttributeFix | `attributefix-neoforge-1.21.1-21.1.3.jar` | client + serveur | CurseForge |
| Azimuth API | `azimuth-1.4.10.jar` | client + serveur | CurseForge |
| Balm | `balm-neoforge-1.21.1-21.0.66.jar` | client + serveur | CurseForge |
| Better Advanced Tooltips | `better-advanced-tooltips-2101.1.0-build.5.jar` | client + serveur | CurseForge |
| Better Advancements | `BetterAdvancements-NeoForge-1.21.1-0.4.3.21.jar` | client seul | CurseForge |
| Better Library | `better_lib-neoforge-1.21.1-1.0.111.jar` | client + serveur | CurseForge |
| Bookshelf | `bookshelf-neoforge-1.21.1-21.1.81.jar` | client + serveur | CurseForge |
| Burnt + Additions | `burnt-1.10.5.2-neoforge-1.21.1.jar` | client + serveur | CurseForge |
| Carry On | `carryon-neoforge-1.21.1-2.2.6.13.jar` | client + serveur | CurseForge |
| CBC: Locometal Armor | `locometal_armor-2.0.0.jar` | client + serveur | Modrinth (inclus dans le zip) |
| CBC Neo Warfare | `cbcmodernwarfare-0.0.6v+mc.1.21.1-neoforge.jar` | client + serveur | CurseForge |
| CBC Terminal Ballistics | `cbc_terminal_ballistics-2.3.0.jar` | client + serveur | CurseForge |
| CC: Tweaked | `cc-tweaked-1.21.1-forge-1.120.2.jar` | client + serveur | Modrinth (inclus dans le zip) |
| CC:CBC | `cc-cbc-v1.1.1-1.21.1-neoforge.jar` | client + serveur | CurseForge |
| Create Big Cannons: Security Breach | `create_big_cannons_security_breach-1.0.jar` | client + serveur | CurseForge |
| Create: Avionics | `create_avionics-neoforge-1.21.1-0.6.0.jar` | client + serveur | Modrinth (inclus dans le zip) |
| Create: CC Better Recipes | `ccbr-1.2.1-neoforge-1.21.1.jar` | client + serveur | Modrinth (inclus dans le zip) |
| Create: Gunsmithing | `create-gunsmithing-1.21.1-1.4.9.jar` | client + serveur | CurseForge |
| Chunky (Forge/NeoForge) | `Chunky-NeoForge-1.4.23.jar` | serveur seul | CurseForge |
| Cloth Config API (Fabric/Forge/NeoForge) | `cloth-config-15.0.140-neoforge.jar` | client + serveur | CurseForge |
| Clumps | `Clumps-neoforge-1.21.1-19.0.0.1.jar` | client + serveur | CurseForge |
| Comforts | `comforts-neoforge-9.0.5+1.21.1.jar` | client + serveur | CurseForge |
| Configured | `configured-neoforge-1.21.1-2.6.3.jar` | client + serveur | CurseForge |
| Connectivity | `connectivity-1.21.1-7.7.jar` | client + serveur | CurseForge |
| Controlling | `Controlling-neoforge-1.21.1-19.0.5.jar` | client seul | CurseForge |
| Create: Copycats+ | `copycats-3.0.9+mc.1.21.1-neoforge.jar` | client + serveur | CurseForge |
| Corpse | `corpse-neoforge-1.21.1-1.1.21.jar` | client + serveur | CurseForge |
| Crash Assistant | `CrashAssistant-neoforge-1.20.6-1.21.4-1.11.12.jar` | client seul | CurseForge |
| Create: Additional Logistics | `createadditionallogistics-1.21.1-1.4.5.jar` | client + serveur | CurseForge |
| Create Additions - Sable Compat | `sable_createaddition_compat-0.1.13.jar` | client + serveur | CurseForge |
| Create Aero Radars | `create_aero_radar-0.1.1-1.21.1.jar` | client + serveur | CurseForge |
| Create Aeronautics Structures | `cr_aero_structures.jar` | client + serveur | CurseForge |
| Create Aeronautics | `create-aeronautics-bundled-1.21.1-1.3.2.jar` | client + serveur | CurseForge |
| Create: AeroWarptics | `aerowarptics-1.3.0.jar` | client + serveur | CurseForge |
| Create: Armored Constructs | `arcon-1.0.0.jar` | client + serveur | CurseForge |
| Create Better Motors (Crafts & Additions Addon) | `create_better_motors-4.0.0.jar` | client + serveur | CurseForge |
| Create Big Cannons: Advanced Technologies | `cbc_at_Neoforge_1.21.1_0.1.4c.jar` | client + serveur | CurseForge |
| Create Big Cannons | `createbigcannons-5.11.7+mc.1.21.1.jar` | client + serveur | CurseForge |
| Create: Bits 'n' Bobs | `bits_n_bobs-2.3.5.jar` | client + serveur | CurseForge |
| Create: Blocks & Bogies | `create_bb-1.0.8-1.21.1.jar` | client + serveur | CurseForge |
| Create: Central Kitchen | `create-central-kitchen-2.6.2b.jar` | client + serveur | CurseForge |
| Create Cobblestone | `createcobblestone-1.5.0+neoforge-1.21.1-153.jar` | client + serveur | CurseForge |
| Create Confectionery | `create-confectionery1.21.1_v1.1.3b.jar` | client + serveur | CurseForge |
| Create: Configurable Machine Outputs - Now for Millstones , Presses and Crushing Wheels ! | `createcmo-1.3.0-create-6.0.x-neoforge-1.21.1.jar` | client + serveur | CurseForge |
| Create: Connected | `create_connected-1.3.3-mc1.21.1.jar` | client + serveur | CurseForge |
| Create: Copper & Zinc | `create_copper_and_zinc-2.0.0-neoforge-1.21.1.jar` | client + serveur | CurseForge |
| Create: Crafts & Additions and Aeronautics Compat | `aeroadditionbridge-1.0.0.jar` | client + serveur | CurseForge |
| Create Deco | `createdeco-2.1.3.jar` | client + serveur | CurseForge |
| Create: Design n' Decor | `Design-n-Decor-1.21.1-2.2b.jar` | client + serveur | CurseForge |
| Create: Diesel Generators | `createdieselgenerators-1.21.1-1.3.15.jar` | client + serveur | CurseForge |
| Create: Dragons Plus | `CreateDragonsPlus-1.11.9.jar` | client + serveur | CurseForge |
| Create: Dreams & Desires | `DnDesires-1.21.1-2.3a-BETA.jar` | client + serveur | Modrinth (inclus dans le zip) |
| Create: Easy Structures | `create_easy_structures-0.2a-neoforge-1.21.1.jar` | client + serveur | CurseForge |
| Create Encased | `Create Encased-1.21.1-1.9.0-ht3.jar` | client + serveur | CurseForge |
| Create: Enchantable Machinery | `createenchantablemachinery-3.6.0+mc1.21.1-neoforge.jar` | client + serveur | CurseForge |
| Create: Enchantment Industry | `create-enchantment-industry-2.5.4b.jar` | client + serveur | CurseForge |
| Create: Escalated | `escalated-1.3.2-mc.1.21.1.jar` | client + serveur | CurseForge |
| Create Factory Logistics | `create_factory_logistics-1.21.1-1.6.1-all.jar` | client + serveur | CurseForge |
| Create: Framed | `createframed-1.21.1-1.8.2.jar` | client + serveur | CurseForge |
| Create: Garnished | `garnished-2.1.9.2+1.21.1-neoforged.jar` | client + serveur | Modrinth (inclus dans le zip) |
| Create Goggles (Create Plus) | `creategoggles-1.21.1-6.1.1-[NEOFORGE].jar` | client + serveur | CurseForge |
| Create Jetpack | `create_jetpack-forge-5.2.1.jar` | client + serveur | CurseForge |
| Create: Lift n' Load |  Simulated/Aeronautics Addon | `Lift-n-Load-1.21.1-1.1.prev.b.jar` | client + serveur | CurseForge |
| Create: Liquid Fuel | `createliquidfuel-3.0.0-1.21.1.jar` | client + serveur | CurseForge |
| Create Mechanical Extruder | `create_mechanical_extruder-1.21.1-2.2.2-6.0.10.jar` | client + serveur | CurseForge |
| Create Missiles | `createmissiles-1.0.3+neoforge-1.21.1.jar` | client + serveur | CurseForge |
| Create: Mobile Packages | `create_mobile_packages-1.21.1-0.7.8.jar` | client + serveur | CurseForge |
| Create: More Drill Heads | `createmoredrillheads-2.1.0-1.21.1.jar` | client + serveur | CurseForge |
| Create: Netherless ((Neo)Forge) | `create_netherless-2.0.0-1.21.1.jar` | client + serveur | CurseForge |
| Create: New Age | `create-new-age-1.2.0+neoforge-mc1.21.1.jar` | client + serveur | CurseForge |
| Create Ore Excavation | `createoreexcavation-1.21-1.6.8.jar` | client + serveur | CurseForge |
| Create: Power Loader | `create_power_loader-2.0.5-mc1.21.1.jar` | client + serveur | CurseForge |
| Create Propulsion: Simulated | `createpropulsion-1.1.5.jar` | client + serveur | CurseForge |
| Create: Radars | `create_radar-0.4.9.4-1.21.1.jar` | client + serveur | CurseForge |
| Create : radiologistics | `CreateRadiologistics-1.1.3.jar` | client + serveur | CurseForge |
| Create Railways Navigator | `createrailwaysnavigator-neoforge-1.21.1-beta-0.10.0-C6.jar` | client + serveur | CurseForge |
| Create: Recipe Conflict Fixer | `createrecipeconflictfixer-2.0.0.jar` | client + serveur | CurseForge |
| Create: Rustic Structures | `create_rustic_structures-1.0.1-neoforge-1.21.1.jar` | client + serveur | CurseForge |
| Create: Simulated Thrusters | `create_simulated_thrusters-1.21.1-NeoForge-1.1.1.jar` | client + serveur | CurseForge |
| Create: Sophisticated Backpacks Compat | `create_sophback_compat-1.0.jar` | client + serveur | CurseForge |
| Create: Structures Arise | `Create-Structures-Arise-1.21.1-NeoForge-176.49.50.jar` | client + serveur | CurseForge |
| Create: structures | `create_structure-1.0.9.5-neoforge-1.21.1.jar` | client + serveur | CurseForge |
| Create Stuff 'N Additions | `create-stuff-additions1.21.1_v2.1.4b.jar` | client + serveur | CurseForge |
| Create: The Air War | `create_the_air_wars-5.00.jar` | client + serveur | CurseForge |
| Create: Track Map (UNOFFICIAL FORK) | `create-track-map-2.1+mc1.21.1-neoforge.jar` | client + serveur | CurseForge |
| Create: Train Perspective | `create_train_perspective-1.1.1.jar` | client seul | CurseForge |
| Create: Tweaked Controllers | `create_tweaked_controllers-1.21.1-1.2.7.jar` | client + serveur | CurseForge |
| Create | `create-1.21.1-6.0.10.jar` | client + serveur | CurseForge |
| Create Crafts & Additions | `createaddition-1.7.2.jar` | client + serveur | CurseForge |
| Create Nuclear | `createnuclear-2.0.0-neoforge.jar` | client + serveur | CurseForge |
| Cristel Lib | `cristellib-neoforge-1.21.1-3.1.7.jar` | client + serveur | CurseForge |
| Cupboard | `cupboard-1.21.1-4.2.jar` | client + serveur | CurseForge |
| Deeper and Darker | `deeperdarker-neoforge-1.21.1-1.4.1.jar` | client + serveur | CurseForge |
| Default Options | `defaultoptions-neoforge-1.21.1-21.1.8.jar` | client + serveur | CurseForge |
| Distant Horizons: A Level of Detail mod | `DistantHorizons-3.3.3-1.21.1-fabric-neoforge.jar` | client seul | CurseForge |
| DragonLib | `dragonlib-3.0.29+mc1.21.1-neoforge.jar` | client + serveur | CurseForge |
| Durability Tooltip | `durabilitytooltip-1.2.0-neoforge-mc1.21.jar` | client seul | CurseForge |
| EMI | `emi-1.1.24+1.21.1+neoforge.jar` | client + serveur | CurseForge |
| Enchantment Descriptions | `enchdesc-neoforge-1.21.1-21.1.11.jar` | client + serveur | CurseForge |
| [EMF] Entity Model Features [Fabric & Forge] | `entity_model_features-3.3.12-1.21-neoforge.jar` | client seul | CurseForge |
| [ETF] Entity Texture Features - [Fabric & Forge] | `entity_texture_features-7.2.6-1.21-neoforge.jar` | client seul | CurseForge |
| Entity Culling Fabric/Forge | `entityculling-neoforge-1.11.3-mc1.21.1.jar` | client seul | CurseForge |
| Explorify – Dungeons & Structures | `Explorify v1.6.5.mod.jar` | client + serveur | CurseForge |
| Explosion Overhaul: A new level of destruction | `explosionoverhaul-0.2.3.0-neoforge.jar` | client + serveur | CurseForge |
| FastFurnace | `FastFurnace-1.21.1-9.0.1.jar` | client + serveur | CurseForge |
| FastSuite | `FastSuite-1.21.1-6.0.7.jar` | client + serveur | CurseForge |
| FerriteCore ((Neo)Forge) | `ferritecore-7.0.3-neoforge.jar` | client + serveur | CurseForge |
| Friends&Foes (Forge/NeoForge) | `friendsandfoes-neoforge-5.0.3+mc1.21.1.jar` | client + serveur | CurseForge |
| FTB Chunks (NeoForge) | `ftb-chunks-neoforge-2101.1.22.jar` | client + serveur | CurseForge |
| FTB Essentials (Forge & Fabric) | `ftb-essentials-neoforge-2101.1.10.jar` | client + serveur | CurseForge |
| FTB Library (NeoForge) | `ftb-library-neoforge-2101.1.37.jar` | client + serveur | CurseForge |
| FTB Quests (NeoForge) | `ftb-quests-neoforge-2101.1.36.jar` | client + serveur | CurseForge |
| FTB Teams (NeoForge) | `ftb-teams-neoforge-2101.1.11.jar` | client + serveur | CurseForge |
| GeckoLib | `geckolib-neoforge-1.21.1-4.9.3.jar` | client + serveur | CurseForge |
| Create: Hypertubes | `create_hypertube-0.6.0-NEOFORGE.jar` | client + serveur | Modrinth (inclus dans le zip) |
| Iceberg [Neo/Forge] | `Iceberg-1.21.1-neoforge-1.3.2.jar` | client seul | CurseForge |
| ImmediatelyFast | `ImmediatelyFast-NeoForge-1.6.15+1.21.1.jar` | client seul | CurseForge |
| Create: Interiors | `interiors-0.6.1 v3.jar` | client + serveur | CurseForge |
| Inventory Essentials | `inventoryessentials-neoforge-1.21.1-21.1.20.jar` | client + serveur | CurseForge |
| Inventory Profiles Next | `InventoryProfilesNext-neoforge-1.21.1-2.2.5.jar` | client seul | CurseForge |
| Iris Shaders | `iris-neoforge-1.8.14-beta.1+mc1.21.1.jar` | client seul | CurseForge |
| Item Borders [Neo/Forge] | `ItemBorders-1.21-neoforge-1.2.5.jar` | client seul | CurseForge |
| Jade 🔍 | `Jade-1.21.1-NeoForge-15.10.6.jar` | client + serveur | CurseForge |
| Just Enough Items (JEI) | `jei-1.21.1-neoforge-19.64.0.455.jar` | client + serveur | CurseForge |
| Just Enough Effect Descriptions (JEED) | `jeed-1.21-2.3.5-neoforge.jar` | client + serveur | CurseForge |
| Just Enough Resources (JER) | `JustEnoughResources-NeoForge-1.21.1-1.6.0.17.jar` | client + serveur | CurseForge |
| Kaleidoscope Cookery | `kaleidoscopecookery-1.6.0-neoforge+mc1.21.1.jar` | client + serveur | CurseForge |
| Kotlin for Forge | `kotlinforforge-5.12.0-all.jar` | client + serveur | CurseForge |
| KubeJS | `kubejs-neoforge-2101.7.2-build.377.jar` | client + serveur | CurseForge |
| libIPN | `libIPN-neoforge-1.21.1-6.6.3.jar` | client seul | CurseForge |
| Lithostitched | `lithostitched-1.8.1-neoforge-21.1.jar` | client + serveur | CurseForge |
| Mechanicals Lib | `mechanicals-1.21.1-1.1.6.jar` | client + serveur | CurseForge |
| MezzConfig | `mezz_config-1.21.1-neoforge-0.6.8-standalone.jar` | client + serveur | CurseForge |
| ModernFix | `modernfix-neoforge-5.27.26+mc1.21.1.jar` | client + serveur | CurseForge |
| Mouse Tweaks | `MouseTweaks-neoforge-mc1.21-2.26.1.jar` | client seul | CurseForge |
| Mowzie's Mobs | `mowziesmobs-1.21.1-1.8.2.jar` | client + serveur | CurseForge |
| Noisium | `noisium-neoforge-2.3.0+mc1.21-1.21.1.jar` | serveur seul | Modrinth (inclus dans le zip) |
| [NTGL] NukaTeam's Gun Lib | `ntgl-1.21.1-3.2.0.jar` | client + serveur | CurseForge |
| Ore Zombies: The Extinto | `ore_zombies_the_extinto-2.0.1-neoforge-1.21.1.jar` | client + serveur | CurseForge |
| Overgeared | `overgeared-neoforge-1.21.1-1.6.19.jar` | client + serveur | CurseForge |
| Packet Fixer | `packetfixer-3.3.1-1.20.5-1.21.X-merged.jar` | client + serveur | CurseForge |
| Placebo | `Placebo-1.21.1-9.9.4.jar` | client + serveur | CurseForge |
| playerAnimator | `player-animation-lib-forge-2.0.4+1.21.1.jar` | client + serveur | CurseForge |
| Polymorph | `polymorph-neoforge-1.2.0+1.21.1.jar` | client + serveur | CurseForge |
| Prickle | `prickle-neoforge-1.21.1-21.1.11.jar` | client + serveur | CurseForge |
| Prism [Neo/Forge] | `Prism-1.21.1-neoforge-1.0.11.jar` | client seul | CurseForge |
| Project: Explosive | `projectexplosive-0.7.2-beta.jar` | client + serveur | CurseForge |
| Railways Additions | `railwaysuntold_additions-neoforge-1.0.1a.jar` | client + serveur | CurseForge |
| Railways Untold | `railwaysuntold-1.21.1-neoforge-1.2.2.jar` | client + serveur | CurseForge |
| Resourceful Lib | `resourcefullib-neoforge-1.21-3.0.12.jar` | client + serveur | CurseForge |
| Rhino | `rhino-2101.2.8-build.91.jar` | client + serveur | CurseForge |
| Ritchie's Projectile Library | `ritchiesprojectilelib-2.1.2-mc.1.21.1-neoforge.jar` | client + serveur | CurseForge |
| Sable Long Range Warfare | `createarms-1.0.0.jar` | client + serveur | CurseForge |
| Sable | `sable-neoforge-1.21.1-2.0.6.jar` | client + serveur | CurseForge |
| SC-SuperB Bridge | `sc_superb_bridge-1.2.0.jar` | client + serveur | CurseForge |
| Searchables | `Searchables-neoforge-1.21.1-1.0.2.jar` | client seul | CurseForge |
| Security Craft | `[1.21.1] SecurityCraft v1.10.2.1.jar` | client + serveur | CurseForge |
| Simple Discord Rich Presence (Forge /  Fabric) | `SimpleDiscordRichPresence-neoforge-88.0.1-build.54+mc1.21.1.jar` | client seul | CurseForge |
| Simple Voice Chat | `voicechat-neoforge-1.21.1-2.6.24.jar` | client + serveur | CurseForge |
| Create Slice & Dice | `sliceanddice-4.3.4-neoforge.jar` | client + serveur | CurseForge |
| Sodium | `sodium-neoforge-0.8.13+mc1.21.1.jar` | client seul | CurseForge |
| Sophisticated Backpacks | `sophisticatedbackpacks-1.21.1-3.26.12.2208.jar` | client + serveur | CurseForge |
| Sophisticated Core | `sophisticatedcore-1.21.1-1.5.14.2413.jar` | client + serveur | CurseForge |
| spark | `spark-1.10.124-neoforge.jar` | client + serveur | CurseForge |
| Steam 'n' Rails Neoforge | `railways-0.3.0-beta.2+neoforge-mc1.21.1.jar` | client + serveur | CurseForge |
| Strut Your Stuff (Struts) | `struts-1.3.1.jar` | client + serveur | CurseForge |
| SuperMartijn642's Config Lib | `supermartijn642configlib-1.1.8-neoforge-mc1.21.jar` | client seul | CurseForge |
| Terralith | `Terralith_1.21.x_v2.6.2.jar` | client + serveur | CurseForge |
| Create: TFMG Community Edition | `tfmg-1.21.1-1.3.2a-community.jar` | client + serveur | CurseForge |
| Tom's Simple Storage Mod | `toms_storage-1.21-2.4.2.jar` | client + serveur | CurseForge |
| Towns and Towers | `t_and_t-fabric-neoforge-1.13.11.jar` | client + serveur | CurseForge |
| Waystones: Sable (Create Aeronautics Addon) | `waystonessable-1.0.7.jar` | client + serveur | CurseForge |
| Waystones | `waystones-neoforge-1.21.1-21.1.46.jar` | client + serveur | CurseForge |
| Xaero Train Map | `xaerotrainmap-1.0.0.jar` | client + serveur | CurseForge |
| Xaero's Minimap | `xaerominimap-neoforge-1.21.1-26.6.0.jar` | client seul | CurseForge |
| Xaero's World Map | `xaeroworldmap-neoforge-1.21.1-1.47.0.jar` | client seul | CurseForge |
| YUNG's API (NeoForge) [1.20.4-1.21.1 ONLY] | `YungsApi-1.21.1-NeoForge-5.1.9.jar` | client + serveur | CurseForge |
| YUNG's Better Desert Temples (NeoForge) [1.20.4 - 1.21.1 ONLY] | `YungsBetterDesertTemples-1.21.1-NeoForge-4.1.5.jar` | client + serveur | CurseForge |
| YUNG's Better Dungeons (NeoForge) [1.20.4 - 1.21.1 ONLY] | `YungsBetterDungeons-1.21.1-NeoForge-5.1.4.jar` | client + serveur | CurseForge |
| YUNG's Better Jungle Temples (NeoForge) [1.20.4 - 1.21.1 ONLY] | `YungsBetterJungleTemples-1.21.1-NeoForge-3.1.2.jar` | client + serveur | CurseForge |
| YUNG's Better Mineshafts (NeoForge) [1.20.4 - 1.21.1 ONLY] | `YungsBetterMineshafts-1.21.1-NeoForge-5.1.1.jar` | client + serveur | CurseForge |
| YUNG's Better Nether Fortresses (NeoForge) [1.20.4 - 1.21.1 ONLY] | `YungsBetterNetherFortresses-1.21.1-NeoForge-3.1.5.jar` | client + serveur | CurseForge |
| YUNG's Better Ocean Monuments (NeoForge) [1.20.4 - 1.21.1 ONLY] | `YungsBetterOceanMonuments-1.21.1-NeoForge-4.1.2.jar` | client + serveur | CurseForge |
| YUNG's Better Strongholds (NeoForge) [1.20.4 - 1.21.1 ONLY] | `YungsBetterStrongholds-1.21.1-NeoForge-5.1.3.jar` | client + serveur | CurseForge |
| YUNG's Better Witch Huts (NeoForge) [1.20.4 - 1.21.1 ONLY] | `YungsBetterWitchHuts-1.21.1-NeoForge-4.1.1.jar` | client + serveur | CurseForge |
| Fresh Animations (pack de ressources) | `FreshAnimations_v1.10.4.zip` | client seul | CurseForge |
| Complementary Shaders - Reimagined (shader) | `ComplementaryReimagined_r5.9.3.zip` | client seul | CurseForge |
