"""Generate the FTB Quests book (config/ftbquests/quests) from the CHAPTERS table below.

Usage: python tools/gen_quests.py [ids.json]
With an ids.json (item id -> name, dumped from the mod jars), every item id is checked first.
Quest ids are derived from the quest keys, so regenerating keeps player progress.
Texts are written to lang/en_us.snbt (fallback language), in French.
"""
import hashlib, json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "config", "ftbquests", "quests")

# Quest tuple: (key, item id, count, title, description, [dependency keys], xp reward[, extra])
# A dependency is a key of the same chapter, or "<chapter>:<key>" for another chapter.
# Optional extra dict: several consumed tasks, an item reward, repetition (see LAUNCH_CODES),
# or CHECK for an action without an item (checkmark task, the item id is only the icon).

# Repeatable quest giving the Launch Codes (kubejs:launch_codes, see kubejs/startup_scripts).
# The tasks consume their items, the reward is claimed once per team, then the quest can be
# done again after repeat_cooldown seconds. Linear: every dependency must be completed first.
LAUNCH_CODES = {
    "tasks": [("tfmg:circuit_board", 4), ("createnuclear:enriched_yellowcake", 8), ("create:precision_mechanism", 4)],
    "reward_item": ("kubejs:launch_codes", 1),
    "repeat_cooldown": 14400,
    "linear": True,
}
CHECK = {"checkmark": True}

CHAPTERS = [
    # Guide d'Overgeared (recettes lues dans overgeared-neoforge-1.21.1-1.6.19.jar) : fer et acier se forgent à l'enclume.
    ("00_forge", "La forge", "Overgeared ralentit le fer : outils et armures se forgent à l'enclume. Ce chapitre explique comment.", "overgeared:smithing_hammer", [
        ("rock", "overgeared:knappable_rock", 4, "Tailler la roche", "Clic droit répété sur un bloc de pierre avec un silex : tu obtiens de la roche (le silex peut casser). Les outils en pierre ne se fabriquent plus directement.", [], 10),
        ("stone_head", "overgeared:stone_pickaxe_head", 1, "Tête de pioche en pierre", "Une roche dans chaque main, clic droit : la grille de taille s'ouvre. Dessine la forme de la tête (JEI ou EMI montre les formes). Tête + bâton = pioche en pierre.", ["rock"], 15),
        ("copper_hammer", "overgeared:copper_smithing_hammer", 1, "Marteau de forge en cuivre", "2 lingots de cuivre et 2 bâtons : l'outil de base du forgeron.", ["stone_head"], 15),
        ("stone_anvil", "minecraft:stone", 1, "Enclume en pierre", "Accroupi, clic droit sur un bloc de pierre avec le marteau : il devient une enclume de forge en pierre. Elle casse après 64 forgeages. Coche la quête quand c'est fait.", ["copper_hammer"], 15, CHECK),
        ("tongs", "overgeared:wooden_tongs", 1, "Pinces", "5 bâtons. Un métal chauffé te brûle : tiens les pinces dans l'autre main pour le manipuler.", ["copper_hammer"], 10),
        ("heated_iron", "overgeared:heated_iron_ingot", 1, "Lingot de fer chauffé", "Au haut fourneau, le fer devient un lingot chauffé (le four classique donne toujours des lingots froids). Il refroidit en 60 s dans l'inventaire, ou tout de suite jeté dans l'eau.", ["tongs"], 20),
        ("forge_head", "overgeared:iron_pickaxe_head", 1, "Forger une tête de pioche", "Ouvre l'enclume, place 3 lingots chauffés en ligne, puis accroupi + clic droit sur l'enclume avec le marteau : le mini-jeu commence. Frappe quand la flèche est dans la zone. Le score donne la qualité, de « mal forgé » à « parfait ».", ["heated_iron", "stone_anvil"], 40),
        ("iron_pickaxe", "minecraft:iron_pickaxe", 1, "Pioche en fer", "Tête + bâton à l'établi. La qualité de la tête passe à l'outil.", ["forge_head"], 20),
        ("blueprint", "overgeared:blueprint", 1, "Plan de forge", "Plan vierge (3 papiers + colorant bleu) et établi = table à dessin, où tu choisis un type d'outil. Sans plan, une tête ne dépasse pas les qualités basses ; le plan s'améliore à chaque forge.", ["forge_head"], 20),
        ("iron_plate", "overgeared:iron_plate", 8, "Plaques de fer", "Un lingot de fer froid forgé donne une plaque. Les armures en fer se forgent avec ces plaques.", ["stone_anvil"], 20),
        ("iron_chestplate", "minecraft:iron_chestplate", 1, "Plastron en fer", "8 plaques de fer forgées sur l'enclume.", ["iron_plate"], 30),
        ("alloy_furnace", "overgeared:alloy_furnace", 1, "Fourneau à alliages", "8 blocs de briques autour d'un haut fourneau.", ["heated_iron"], 25),
        ("crude_steel", "overgeared:crude_steel", 4, "Acier brut", "3 lingots de fer + 1 charbon = 4 aciers bruts. Chauffe-les au haut fourneau, puis forge-les en lingots d'acier. Raccourci : l'acier TFMG (chapitre 4) passé au haut fourneau donne directement un lingot d'acier chauffé.", ["alloy_furnace"], 30),
        ("smithing_hammer", "overgeared:smithing_hammer", 1, "Marteau de forge en acier", "Tête forgée avec 2 lingots d'acier chauffés, plus un bâton.", ["crude_steel"], 40),
        ("smithing_anvil", "minecraft:anvil", 1, "Enclume de forge", "Accroupi, clic droit sur une enclume classique avec un marteau de forge : elle devient une enclume de forge, qui ne casse pas comme celle en pierre. Coche la quête quand c'est fait.", ["smithing_hammer"], 30, CHECK),
        ("steel_pickaxe", "overgeared:steel_pickaxe", 1, "Pioche en acier", "Plus solide que le fer, et la base de l'équipement en diamant.", ["smithing_hammer"], 40),
        ("diamond", "minecraft:diamond_pickaxe", 1, "Pioche en diamant", "Les recettes en diamant classiques sont désactivées. Table de forgeron : outil en acier + gabarit + diamant. Le gabarit se trouve dans les coffres (forteresses, temples, mines...) et se copie avec 7 diamants et 1 lingot d'acier.", ["steel_pickaxe"], 60),
    ]),
    ("01_engrenages", "L'âge de l'engrenage", "Les bases de Create : rotation, alliage et premières machines.", "create:cogwheel", [
        ("andesite_alloy", "create:andesite_alloy", 16, "Alliage d'andésite", "Mélange andésite et fer (ou zinc) : c'est la base de presque toutes les machines.", [], 50),
        ("wrench", "create:wrench", 1, "La clé", "Tourne, démonte et règle tes machines.", ["andesite_alloy"], 25),
        ("goggles", "create:goggles", 1, "Lunettes d'ingénieur", "Affiche le stress et la vitesse des réseaux.", ["andesite_alloy"], 25),
        ("shaft", "create:shaft", 16, "Arbres de transmission", "Transmettent la rotation en ligne droite.", ["andesite_alloy"], 25),
        ("cogwheel", "create:cogwheel", 8, "Roues dentées", "Pour changer d'axe et de vitesse.", ["shaft"], 25),
        ("large_cogwheel", "create:large_cogwheel", 4, "Grandes roues dentées", "Petite + grande roue = vitesse doublée ou divisée.", ["cogwheel"], 25),
        ("water_wheel", "create:water_wheel", 2, "Roue à eau", "Ta première source d'énergie mécanique.", ["shaft"], 50),
        ("andesite_casing", "create:andesite_casing", 16, "Boîtier d'andésite", "Indispensable pour ventilateurs, dépôts, etc.", ["andesite_alloy"], 50),
        ("millstone", "create:millstone", 1, "Meule", "Broie les minerais : premier doublage de minerai.", ["water_wheel"], 50),
        ("press", "create:mechanical_press", 1, "Presse mécanique", "Fabrique les plaques de fer et de cuivre.", ["andesite_casing"], 75),
        ("iron_sheet", "create:iron_sheet", 32, "Plaques de fer", "Il t'en faudra beaucoup.", ["press"], 50),
        ("fan", "create:encased_fan", 2, "Ventilateur", "Lavage, fumage, cuisson en masse.", ["andesite_casing"], 50),
        ("drill", "create:mechanical_drill", 1, "Foreuse mécanique", "Base de toute contraption minière.", ["andesite_casing"], 50),
        ("crushing_wheel", "create:crushing_wheel", 2, "Roues de broyage", "Le vrai doublage de minerai.", ["millstone", "large_cogwheel"], 150),
    ]),
    ("02_laiton", "Laiton & logistique", "Le laiton débloque l'automatisation avancée et la logistique.", "create:brass_ingot", [
        ("mixer", "create:mechanical_mixer", 1, "Mélangeur mécanique", "Avec un bassin, il fait les alliages.", [], 75),
        ("basin", "create:basin", 2, "Bassin", "Récipient de mélange et de pressage.", [], 25),
        ("blaze_burner", "create:blaze_burner", 1, "Brûleur à blaze", "Chaleur pour le mélange du laiton.", ["basin"], 100),
        ("zinc", "create:zinc_ingot", 32, "Zinc", "Le minerai de zinc se trouve en profondeur.", [], 50),
        ("brass", "create:brass_ingot", 32, "Laiton", "Cuivre + zinc chauffés dans le mélangeur.", ["mixer", "blaze_burner", "zinc"], 150),
        ("brass_casing", "create:brass_casing", 16, "Boîtier en laiton", "Pour les machines intelligentes.", ["brass"], 75),
        ("electron_tube", "create:electron_tube", 8, "Tubes électroniques", "Composant de la logique Create.", ["brass"], 50),
        ("precision_mechanism", "create:precision_mechanism", 4, "Mécanisme de précision", "Première vraie recette en séquence.", ["brass"], 200),
        ("deployer", "create:deployer", 2, "Déployeur", "Une main robotique.", ["brass_casing"], 100),
        ("arm", "create:mechanical_arm", 1, "Bras mécanique", "Déplace les objets entre machines.", ["precision_mechanism"], 150),
        ("crafter", "create:mechanical_crafter", 9, "Fabricants mécaniques", "Les grandes recettes de l'armement.", ["brass_casing", "electron_tube"], 200),
        ("packager", "create:packager", 2, "Emballeur", "Le réseau logistique de Create 6.", ["brass_casing"], 100),
        ("stock_link", "create:stock_link", 1, "Liaison de stock", "Commandes à distance de ton stock.", ["packager"], 100),
        ("schematicannon", "create:schematicannon", 1, "Schématicanon", "Construit automatiquement tes schémas.", ["brass_casing"], 150),
    ]),
    ("03_electricite", "Électricité", "Produire et transporter de l'énergie : Crafts & Additions, New Age, TFMG.", "createaddition:alternator", [
        ("copper_wire", "create_new_age:copper_wire", 16, "Fil de cuivre", "La base des circuits électriques.", [], 50),
        ("alternator", "createaddition:alternator", 1, "Alternateur", "Transforme la rotation en énergie (FE).", ["copper_wire"], 150),
        ("electric_motor", "createaddition:electric_motor", 1, "Moteur électrique", "Et l'inverse : l'énergie en rotation.", ["alternator"], 150),
        ("connector", "createaddition:connector", 8, "Connecteurs", "Relie tes machines avec des câbles.", ["copper_wire"], 50),
        ("accumulator", "createaddition:modular_accumulator", 4, "Accumulateur modulaire", "Stocke l'énergie.", ["alternator"], 150),
        ("tesla_coil", "createaddition:tesla_coil", 1, "Bobine Tesla", "Charge les objets... ou les ennemis.", ["accumulator"], 150),
        ("generator_coil", "create_new_age:generator_coil", 4, "Bobine de générateur (New Age)", "Générateurs multiblocs à aimants.", [], 150),
        ("magnet", "create_new_age:layered_magnet", 4, "Aimants", "Le cœur des générateurs New Age.", ["generator_coil"], 100),
        ("energiser", "create_new_age:basic_energiser", 1, "Énergiseur", "Surcharge les matériaux avec l'électricité.", ["magnet"], 150),
        ("tfmg_generator", "tfmg:generator", 1, "Générateur TFMG", "Réseau électrique industriel de TFMG.", ["alternator"], 200),
    ]),
    ("04_industrie", "Industrie lourde & trains", "Acier, pétrole, moteurs et chemins de fer.", "tfmg:steel_ingot", [
        ("coke", "tfmg:coal_coke", 32, "Coke", "Four à coke : carburant du haut fourneau.", ["02_laiton:brass"], 100),
        ("coke_oven", "tfmg:coke_oven", 4, "Four à coke", "Multibloc qui cuit le charbon.", ["02_laiton:brass"], 100),
        ("steel", "tfmg:steel_ingot", 32, "Acier", "Haut fourneau TFMG : l'acier de l'armement.", ["coke", "coke_oven"], 250),
        ("steam_engine", "create:steam_engine", 4, "Machine à vapeur", "Énorme puissance mécanique.", ["02_laiton:brass"], 200),
        ("crude_oil", "createdieselgenerators:oil_scanner", 1, "Scanner de pétrole", "Trouve les gisements de pétrole.", ["02_laiton:brass"], 100),
        ("pumpjack", "createdieselgenerators:pumpjack_bearing", 1, "Chevalet de pompage", "Extrait le pétrole brut.", ["crude_oil"], 200),
        ("distillation", "createdieselgenerators:distillation_controller", 1, "Distillerie", "Raffine le pétrole en diesel et essence.", ["pumpjack"], 250),
        ("diesel_engine", "createdieselgenerators:diesel_engine", 2, "Moteur diesel", "Puissance compacte, idéale pour les véhicules.", ["distillation"], 200),
        ("large_diesel", "createdieselgenerators:large_diesel_engine", 1, "Gros moteur diesel", "Pour les usines et les navires.", ["diesel_engine"], 250),
        ("track", "create:track", 64, "Rails", "Les trains transportent tes ressources.", ["02_laiton:brass"], 75),
        ("train_controls", "create:controls", 1, "Commandes de train", "Le poste de pilotage.", ["track"], 100),
        ("station", "create:track_station", 2, "Gares", "Automatise les trajets.", ["train_controls"], 150),
        ("plastic", "tfmg:plastic_sheet", 16, "Plastique", "Issu du raffinage : électronique avancée.", ["distillation"], 200),
    ]),
    ("05_artillerie", "Artillerie (Create Big Cannons)", "Fondre, forer et assembler de vrais canons.", "createbigcannons:cannon_mount", [
        ("cast_iron", "tfmg:cast_iron_ingot", 32, "Fonte", "Le métal des premiers canons.", ["04_industrie:coke_oven"], 100),
        ("casting_sand", "createbigcannons:casting_sand", 16, "Sable de moulage", "Pour couler les pièces de canon.", ["04_industrie:coke_oven"], 50),
        ("cannon_cast", "createbigcannons:very_small_cast_mould", 1, "Moule de canon", "Des bûches à la scie mécanique. Entouré de sable de moulage, le métal en fusion y prend la forme du canon.", ["casting_sand"], 100),
        ("cannon_barrel", "createbigcannons:cast_iron_cannon_barrel", 3, "Tube de canon", "Plus le tube est long, plus l'obus va loin.", ["cast_iron", "cannon_cast"], 150),
        ("cannon_chamber", "createbigcannons:cast_iron_cannon_chamber", 2, "Chambre", "Accueille les charges de poudre.", ["cannon_barrel"], 150),
        ("breech", "createbigcannons:cast_iron_sliding_breech", 1, "Culasse coulissante", "Pour recharger par l'arrière.", ["cannon_chamber"], 150),
        ("mount", "createbigcannons:cannon_mount", 1, "Affût", "Oriente le canon en hauteur et en direction.", ["breech"], 200),
        ("powder", "createbigcannons:powder_charge", 16, "Charges de poudre", "Propulsent l'obus.", ["04_industrie:coke_oven"], 75),
        ("he_shell", "createbigcannons:he_shell", 8, "Obus explosifs", "Ne pas utiliser à côté de ta base.", ["powder"], 150),
        ("ap_shell", "createbigcannons:ap_shell", 8, "Obus perforants", "Contre les blindages.", ["powder"], 150),
        ("impact_fuze", "createbigcannons:impact_fuze", 8, "Fusées d'impact", "Détonation au contact.", ["he_shell"], 75),
        ("autocannon", "createbigcannons:cast_iron_autocannon_breech", 1, "Autocanon", "Tir rapide antiaérien.", ["mount"], 200),
        ("flak", "createbigcannons:flak_autocannon_round", 32, "Munitions flak", "Pour descendre les avions.", ["autocannon"], 150),
        ("medium_cannon", "cbcmodernwarfare:cast_iron_mediumcannon_breech", 1, "Canon moyen (Modern Warfare)", "Le canon des chars modernes.", ["mount"], 250),
    ]),
    ("06_aviation", "Aviation (Create Aeronautics)", "Hélices, ballons et vrais avions physiques avec Sable.", "aeronautics:propeller_bearing", [
        ("physics_assembler", "simulated:physics_assembler", 1, "Assembleur physique", "Transforme ta construction en objet physique.", ["02_laiton:precision_mechanism"], 150),
        ("propeller_bearing", "aeronautics:propeller_bearing", 1, "Roulement d'hélice", "Monte une hélice.", ["physics_assembler"], 150),
        ("wooden_propeller", "aeronautics:wooden_propeller", 1, "Hélice en bois", "Ton premier vol.", ["propeller_bearing"], 100),
        ("andesite_propeller", "aeronautics:andesite_propeller", 1, "Hélice en andésite", "Plus de poussée.", ["wooden_propeller"], 150),
        ("smart_propeller", "aeronautics:smart_propeller", 1, "Hélice intelligente", "Contrôlable par redstone.", ["andesite_propeller"], 200),
        ("envelope", "aeronautics:white_envelope", 16, "Enveloppe de ballon", "Pour les dirigeables.", ["physics_assembler"], 100),
        ("burner", "aeronautics:adjustable_burner", 1, "Brûleur réglable", "Chauffe l'air du ballon.", ["envelope"], 150),
        ("levitite", "aeronautics:levitite", 8, "Lévitite", "Bloc qui flotte : cœur des engins volants.", ["physics_assembler"], 250),
        ("gyro", "aeronautics:gyroscopic_propeller_bearing", 1, "Roulement gyroscopique", "Stabilise ton appareil.", ["smart_propeller"], 200),
        ("aviator_goggles", "aeronautics:aviators_goggles", 1, "Lunettes d'aviateur", "Le style avant tout.", ["wooden_propeller"], 50),
        ("thruster", "createpropulsion:thruster", 1, "Propulseur", "Propulsion à carburant liquide. Boîtier et pompes en acier.", ["smart_propeller", "04_industrie:steel"], 250),
        ("wing", "createpropulsion:wing", 8, "Ailes", "Portance pour les vrais avions.", ["thruster"], 200),
        ("jetpack", "create_jetpack:jetpack", 1, "Jetpack", "Fonctionne à l'air comprimé.", ["02_laiton:precision_mechanism"], 150),
    ]),
    ("07_blindes", "Blindés & véhicules", "Roues, chenilles, blindage et tourelles.", "offroad:tire", [
        ("wheel_mount", "offroad:wheel_mount", 4, "Support de roue", "La base de tout véhicule terrestre.", ["04_industrie:steel"], 150),
        ("tire", "offroad:tire", 4, "Pneus", "Montés sur les supports de roue.", ["wheel_mount"], 100),
        ("large_tire", "offroad:large_tire", 4, "Gros pneus", "Pour les engins lourds.", ["tire"], 150),
        ("steel_armor", "cbcmodernwarfare:carbon_steel_block", 16, "Blocs d'acier au carbone", "Acier et coke au mélangeur chauffé donnent l'acier au carbone ; 4 lingots font un bloc. Protège tes véhicules et bunkers.", ["04_industrie:steel"], 200),
        ("era", "cbcmodernwarfare:era_block", 8, "Blindage réactif (ERA)", "Une charge de poudre déployée sur un bloc d'acier au carbone. Explose vers l'extérieur pour arrêter les obus.", ["steel_armor", "05_artillerie:powder"], 250),
        ("arcon_barrel", "arcon:arcon_cannon_barrel", 2, "Canon blindé (Armored Constructs)", "Tourelles pour tes chars.", ["steel_armor"], 250),
        ("turret", "createdieselgenerators:chemical_turret", 1, "Tourelle chimique", "Lance-flammes automatique.", ["04_industrie:distillation"], 200),
        ("periscope", "createarms:periscope_mast", 1, "Périscope", "Vision depuis l'intérieur du char.", ["steel_armor"], 150),
    ]),
    ("08_nucleaire", "Nucléaire civil", "Uranium, thorium et réacteurs : énergie massive, gros risques.", "createnuclear:reactor_core", [
        ("uranium", "createnuclear:raw_uranium", 16, "Uranium brut", "Minerai radioactif : protège-toi.", ["03_electricite:alternator", "04_industrie:steel"], 150),
        ("lead", "tfmg:lead_ingot", 32, "Plomb", "Écran contre les radiations.", ["03_electricite:alternator", "04_industrie:steel"], 100),
        ("anti_rad_helmet", "createnuclear:default_anti_radiation_helmet", 1, "Casque antiradiation", "Équipement de protection, pièce 1.", ["lead"], 75),
        ("anti_rad_chest", "createnuclear:default_anti_radiation_chestplate", 1, "Plastron antiradiation", "Pièce 2.", ["anti_rad_helmet"], 75),
        ("anti_rad_legs", "createnuclear:default_anti_radiation_leggings", 1, "Jambières antiradiation", "Pièce 3.", ["anti_rad_chest"], 75),
        ("anti_rad_boots", "createnuclear:default_anti_radiation_boots", 1, "Bottes antiradiation", "Pièce 4 : la tenue est complète.", ["anti_rad_legs"], 75),
        ("yellowcake", "createnuclear:enriched_yellowcake", 8, "Yellowcake enrichi", "Combustible enrichi.", ["uranium"], 200),
        ("graphite_rod", "createnuclear:graphite_rod", 8, "Barres de graphite", "Modèrent la réaction.", [], 150),
        ("reactor_casing", "createnuclear:reactor_casing", 32, "Boîtier de réacteur", "La coque du réacteur.", ["lead"], 150),
        ("reactor_controller", "createnuclear:reactor_controller", 1, "Contrôleur de réacteur", "Pilote le réacteur.", ["reactor_casing"], 250),
        ("uranium_rod", "createnuclear:uranium_rod", 4, "Barres d'uranium", "Le combustible du réacteur.", ["yellowcake"], 200),
        ("reactor_core", "createnuclear:reactor_core", 1, "Cœur de réacteur", "Là où tout se passe.", ["reactor_controller", "graphite_rod", "uranium_rod"], 400),
        ("reactor_alarm", "createnuclear:reactor_alarm", 1, "Alarme de réacteur", "Prévient avant la fusion.", ["reactor_core"], 100),
        ("thorium", "create_new_age:thorium", 16, "Thorium (New Age)", "Combustible des réacteurs New Age.", ["03_electricite:alternator", "04_industrie:steel"], 150),
        ("na_reactor_rod", "create_new_age:reactor_rod", 4, "Barres de réacteur New Age", "Second type de réacteur.", ["thorium"], 250),
    ]),
    ("09_missiles", "Missiles & radar", "Détecter, suivre et frapper à longue distance.", "create_radar:radar_bearing", [
        ("binoculars", "create_radar:binoculars", 1, "Jumelles", "Repérage et désignation de cibles.", ["03_electricite:alternator"], 50),
        ("radar_bearing", "create_radar:radar_bearing", 1, "Roulement de radar", "Base du radar rotatif.", ["03_electricite:alternator", "04_industrie:plastic"], 200),
        ("radar_dish", "create_radar:radar_dish_block", 4, "Antenne radar", "Détecte avions et navires.", ["radar_bearing"], 150),
        ("monitor", "create_radar:monitor", 4, "Écran radar", "Affiche les contacts.", ["radar_dish"], 150),
        ("fire_controller", "create_radar:fire_controller", 1, "Conduite de tir", "Relie radar et canons.", ["monitor"], 250),
        ("transponder", "create_radar:identification_transponder", 1, "Transpondeur IFF", "Ami ou ennemi ?", ["radar_dish"], 150),
        ("plane_radar", "create_radar:plane_radar", 1, "Radar d'avion", "Radar compact à embarquer sur tes appareils.", ["radar_dish"], 200),
        ("sidewinder", "createarms:aim_9_sidewinder", 2, "AIM-9 Sidewinder", "Missile air-air guidé.", ["fire_controller"], 250),
        ("patriot", "createarms:patriot", 1, "Patriot", "Défense antimissile.", ["sidewinder"], 300),
        ("tomahawk", "createarms:tomahawk", 1, "Tomahawk", "Missile de croisière longue portée : demande des Codes de lancement.", ["fire_controller", "10_adm:launch_codes"], 350),
        ("launch_pad", "createmissiles:launch_pad", 1, "Pas de tir (Create Missiles)", "Missiles physiques modulaires. Les pièces demandent de l'acier.", ["04_industrie:steel"], 200),
        ("warhead_assembly", "createmissiles:warhead_assembly", 1, "Ogive (Create Missiles)", "La charge utile. Les ogives destructrices demandent des obus HE.", ["launch_pad", "05_artillerie:he_shell"], 200),
    ]),
    ("10_adm", "Armes de destruction massive", "La fin de partie : bombes lourdes et ICBM nucléaires. Avec modération...", "projectexplosive:icbm_nuclear", [
        ("munitions_workbench", "projectexplosive:munitions_workbench", 1, "Établi de munitions", "Assemble bombes, ogives et missiles. Se fabrique avec de l'acier et des mécanismes.", ["08_nucleaire:reactor_core", "09_missiles:fire_controller"], 200),
        ("small_bomb", "projectexplosive:small_bomb", 4, "Bombe Mk 82", "Bombe d'aviation légère.", ["munitions_workbench"], 150),
        ("big_bomb", "projectexplosive:big_bomb", 2, "Bombe Mk 84", "Bombe d'aviation lourde.", ["small_bomb"], 250),
        ("airstrike", "projectexplosive:airstrike_caller", 1, "Appel de frappe aérienne", "Cinq Mk 82 sur la cible.", ["big_bomb"], 300),
        ("flash_goggles", "projectexplosive:flash_goggles", 1, "Lunettes anti-flash", "Pour regarder le champignon sans devenir aveugle.", ["munitions_workbench"], 100),
        ("geiger", "projectexplosive:radiation_detector", 1, "Compteur Geiger", "Mesure les retombées.", ["munitions_workbench"], 100),
        ("uranium_ingot", "projectexplosive:uranium_ingot", 8, "Lingots d'uranium", "Matière fissile : 4 yellowcakes enrichis et du plomb, compactés sous brûleur surchauffé.", ["munitions_workbench", "08_nucleaire:yellowcake"], 200),
        ("icbm_target", "projectexplosive:icbm", 1, "Système de visée", "Longue-vue, boussole et redstone : désigne la cible de l'ICBM.", ["munitions_workbench"], 100),
        ("launch_codes", "kubejs:launch_codes", 1, "Codes de lancement", "Indispensables pour armer un ICBM (tenus en main secondaire à la pose) et pour les missiles longue portée. Quête répétable : les objets sont consommés, une fois par équipe, puis délai de 4 h.", ["munitions_workbench", "08_nucleaire:reactor_core"], 100, LAUNCH_CODES),
        ("icbm_small", "projectexplosive:icbm_small", 1, "ICBM léger", "Premier missile balistique. Sa pose consomme des Codes de lancement.", ["icbm_target", "launch_codes"], 300),
        ("icbm_big", "projectexplosive:icbm_big", 1, "ICBM lourd", "Plus gros, plus loin.", ["icbm_small"], 400),
        ("nuclear_bomb", "projectexplosive:nuclear_bomb", 1, "Fat Man", "La bombe nucléaire larguée.", ["uranium_ingot", "big_bomb"], 500),
        ("icbm_nuclear", "projectexplosive:icbm_nuclear", 1, "ICBM nucléaire", "L'arme ultime. Le serveur s'en souviendra.", ["icbm_big", "nuclear_bomb", "flash_goggles"], 1000),
    ]),
]


def qid(*parts):
    """Stable 16-hex-digit FTB id from a name.
    The top bit is cleared: FTB Quests reads ids as signed longs, and an id starting with 8-F
    is lost (chapter shown as "Unnamed", quest unlinked)."""
    h = hashlib.sha1("/".join(("createwarfare",) + parts).encode()).hexdigest()[:16].upper()
    return format(int(h[0], 16) & 7, "X") + h[1:]


def ftb_text(s):
    """FTB Quests reads '&' as a formatting code: a literal '&' must be written '\\&'."""
    return s.replace("&", "\\&")


def dep_ref(cname, dep):
    """'key' -> 'chapter:key' in the same chapter; 'chapter:key' is kept as is."""
    return dep if ":" in dep else f"{cname}:{dep}"


def snbt_str(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def layout(quests):
    """x = dependency depth, y = rank within that depth."""
    keys = {q[0]: q for q in quests}
    depth = {}

    def d(k):
        if k not in depth:
            deps = [x for x in keys[k][5] if x in keys]
            depth[k] = 0 if not deps else 1 + max(d(x) for x in deps)
        return depth[k]

    rows, pos = {}, {}
    for q in quests:
        col = d(q[0])
        row = rows.get(col, 0)
        rows[col] = row + 1
        pos[q[0]] = (col * 2.0, row * 1.5)
    return pos


def main():
    known = json.load(open(sys.argv[1], encoding="utf-8")) if len(sys.argv) > 1 else None
    bad = []
    for cname, _, _, cicon, quests in CHAPTERS:
        for item in [cicon] + [q[1] for q in quests]:
            if known is not None and item not in known and not item.startswith("kubejs:"):
                bad.append(f"{cname}: {item}")
    all_keys = {f"{c[0]}:{q[0]}" for c in CHAPTERS for q in c[4]}
    for cname, _, _, _, quests in CHAPTERS:
        for q in quests:
            for dep in q[5]:
                if dep_ref(cname, dep) not in all_keys:
                    bad.append(f"{cname}: unknown dependency {dep}")
    if bad:
        print("Unknown item ids or dependencies:\n  " + "\n  ".join(bad))
        return 1

    os.makedirs(os.path.join(OUT, "chapters"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "lang"), exist_ok=True)
    lang = {}
    for ci, (cname, ctitle, csub, cicon, quests) in enumerate(CHAPTERS):
        cid = qid(cname)
        lang[f"chapter.{cid}.title"] = ftb_text(ctitle)
        lang[f"chapter.{cid}.chapter_subtitle"] = [ftb_text(csub)]
        pos = layout(quests)
        body = []
        for key, item, count, title, desc, deps, xp, *extra in quests:
            extra = extra[0] if extra else {}
            q = qid(cname, key)
            lang[f"quest.{q}.title"] = ftb_text(title)
            lang[f"quest.{q}.quest_desc"] = [ftb_text(desc)]
            dep_ids = ", ".join(snbt_str(qid(*dep_ref(cname, x).split(":"))) for x in deps)
            x, y = pos[key]
            rewards = [f"{{ id: \"{qid(cname, key, 'xp')}\", type: \"xp\", xp: {xp} }}"]
            if "reward_item" in extra:
                rid, rcount = extra["reward_item"]
                rewards.append(f"{{ id: \"{qid(cname, key, 'item')}\", item: {{ count: {rcount}, id: \"{rid}\" }}, team_reward: true, type: \"item\" }}")
            if "tasks" in extra:
                tasks = [f"{{ consume_items: true, count: {c}L, id: \"{qid(cname, key, 'task', str(i))}\", item: {{ count: 1, id: \"{t}\" }}, type: \"item\" }}"
                         for i, (t, c) in enumerate(extra["tasks"])]
            elif extra.get("checkmark"):
                tasks = [f"{{ id: \"{qid(cname, key, 'task')}\", type: \"checkmark\" }}"]
            else:
                tasks = [f"{{ count: {count}L, id: \"{qid(cname, key, 'task')}\", item: {{ count: 1, id: \"{item}\" }}, type: \"item\" }}"]
            flags = ""
            if extra.get("repeat_cooldown"):
                flags += f"\t\t\tcan_repeat: true\n\t\t\trepeat_cooldown: {extra['repeat_cooldown']}\n"
            if extra.get("linear"):
                flags += "\t\t\tprogression_mode: \"linear\"\n"
            if "tasks" in extra or extra.get("checkmark"):
                flags += f"\t\t\ticon: {{ id: \"{item}\" }}\n"
            body.append(
                "\t\t{\n"
                + flags
                + f"\t\t\tdependencies: [{dep_ids}]\n"
                f"\t\t\tid: \"{q}\"\n"
                f"\t\t\trewards: [{', '.join(rewards)}]\n"
                f"\t\t\ttasks: [{', '.join(tasks)}]\n"
                f"\t\t\tx: {x}d\n"
                f"\t\t\ty: {y}d\n"
                "\t\t}"
            )
        chapter = (
            "{\n"
            "\tdefault_hide_dependency_lines: false\n"
            "\tdefault_quest_shape: \"\"\n"
            f"\tfilename: \"{cname}\"\n"
            "\tgroup: \"\"\n"
            f"\ticon: {{ id: \"{cicon}\" }}\n"
            f"\tid: \"{cid}\"\n"
            f"\torder_index: {ci}\n"
            "\tquest_links: [ ]\n"
            "\tquests: [\n" + "\n".join(body) + "\n\t]\n"
            "}\n"
        )
        open(os.path.join(OUT, "chapters", cname + ".snbt"), "w", encoding="utf-8", newline="\n").write(chapter)

    data = (
        "{\n"
        "\tdefault_autoclaim_rewards: \"disabled\"\n"
        "\tdefault_consume_items: false\n"
        "\tdefault_quest_disable_jei: false\n"
        "\tdefault_quest_shape: \"circle\"\n"
        "\tdefault_reward_team: false\n"
        "\tdetection_delay: 20\n"
        "\tdisable_gui: false\n"
        "\tdrop_loot_crates: false\n"
        "\temergency_items_cooldown: 300\n"
        "\tgrid_scale: 0.5d\n"
        "\ticon: { id: \"create:cogwheel\" }\n"
        f"\tid: \"{qid('file')}\"\n"
        "\tlock_message: \"\"\n"
        "\tpause_game: false\n"
        "\tprogression_mode: \"flexible\"\n"
        "\tversion: 13\n"
        "}\n"
    )
    open(os.path.join(OUT, "data.snbt"), "w", encoding="utf-8", newline="\n").write(data)
    lines = []
    for k, v in lang.items():
        if isinstance(v, list):
            lines.append(f"\t{k}: [" + ", ".join(snbt_str(s) for s in v) + "]")
        else:
            lines.append(f"\t{k}: {snbt_str(v)}")
    open(os.path.join(OUT, "lang", "en_us.snbt"), "w", encoding="utf-8", newline="\n").write("{\n" + "\n".join(lines) + "\n}\n")
    nq = sum(len(c[4]) for c in CHAPTERS)
    print(f"{len(CHAPTERS)} chapters, {nq} quests written to {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
