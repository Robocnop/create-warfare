# Journal des versions

Les versions 1.1.0 à 1.2.0 sont détaillées dans `docs/journal/2026-10-22.md` et sur la page des releases GitHub.

## 1.7.3 — 2026-10-31

- **Pistolets de compression** (Pocket Sized) : on peut de nouveau rétrécir ou agrandir les autres joueurs
  (avant, le message « X has PVP Scaling disabled » s'affichait). Les pistolets restent limités de 1/4 à 4 fois.
- **Traductions** : 285 textes ajoutés pour les noms qui s'affichaient en clé brute (Create: Arms, armes NTGL,
  blocs de Burnt dans Jade, bogies de Bits 'n' Bogeys, obus de CBC AT, J-16, drone FPV, caméra de SecurityCraft…),
  et 10 textes français faux corrigés (fusée à retardement de CBC, Waystones, Create Railways Navigator…).

## 1.7.1 — 2026-10-30

- **Correctif du plantage au lancement** (client) : CBC Optics modifie Entity Culling et cherche un champ que la
  version 1.11 d'Entity Culling a supprimé. Entity Culling revient en 1.10.5 (bloquée à cette version) en attendant
  une mise à jour de CBC Optics. Le serveur n'était pas touché (Entity Culling est un mod client).

## 1.7.0 — 2026-10-30

- **Create: Power Grid** : électricité simulée pour de vrai (loi d'Ohm, circuits en boucle, pertes dans les longs fils,
  transformateurs). Générateurs à construire pièce par pièce, moteurs électriques, servomoteurs, panneaux solaires,
  circuits imprimés, perceuse et scie sur batterie, Electro-Zapper. Son onduleur FE le relie aux autres mods électriques.
  Cinq quêtes au chapitre Électricité.
- **Create: Pocket Sized** (+ Pehkui) : rétrécir ou agrandir les engins Sable, les ranger dans une boîte, les coller
  entre échelles différentes. Réglé pour le PvP : engins de 1/4 à 4 fois, joueur de 1/2 à 2 fois, et **impossible de
  redimensionner un autre joueur**. Une quête au chapitre Aviation (canon de compression).
- Repris du modpack Create: Complete : Create Aeronautics Compatibility, Copycats+ aeronautics weight (poids réalistes
  des blocs Copycats+ sur les engins), Bells & Whistles (décor de trains), Vibrant Vaults (coffres-forts de couleur,
  boîtiers en zinc), Pattern Schematics (schémas répétés), Trading Floor (commerce automatisé avec les villageois),
  Jade Addons, Shulker Box Tooltip, Easy Shulker Boxes, Visual Workbench.
- Côté client : Explosive Enhancement (explosions), Cut Through (frapper à travers l'herbe), Ok Zoomer (zoom),
  Effectual, Visuality, Cosy Critters et Particle Effects (ambiance).
- **Traductions françaises** : 2 059 textes ajoutés (nouveaux mods et ceux de la 1.6.0 : Numismatics, CBC Ballistics,
  CBC Optics, Firepower Components, plus des restes de Create Radar, Railways Untold, Avionics...).
- Testé sur le serveur local : démarrage, 212 quêtes valides, dépendances des mods client vérifiées.
  Pas encore essayé en jeu.

## Launcher 1.1.1 — 2026-10-29

- **Thème Halloween**, actif tout seul du 15 octobre au 5 novembre : engrenages-citrouilles animés sous la lune,
  chauves-souris, cimetière, toiles d'araignée, couleurs orange et violet, titres en Creepster (police libre OFL).
  Le reste de l'année, le launcher garde son apparence habituelle.

## 1.6.0 — 2026-10-29

- **Artillerie** : CBC Ballistics (calculateur balistique, télémètre, commande de canon, coordinateur d'artillerie,
  radio), CBC Optics (viseurs de canon et d'autocanon), CBC: Firepower Components (affûts compacts, chargeurs de
  munitions automatisables). Deux nouvelles quêtes au chapitre Artillerie : viseur et calculateur.
- CBC Ballistics range encore ses recettes, ses tables de butin et ses tags dans les dossiers de la 1.20, ignorés en
  1.21 : sans correctif, ses objets ne se fabriquaient pas et ses blocs ne se récupéraient pas. Copiés dans `kubejs/data/`.
- **Lootr** : chaque joueur a son propre butin dans les coffres des structures. Seulement dans les nouveaux chunks.
- **Create: Numismatics** : pièces, comptes en banque, distributeurs et ventes automatisées entre équipes.
- **Performances** : Create: Threaded Trains (trains calculés à part), Lithium, AllTheLeaks (fuites de mémoire),
  Alternate Current (redstone) ; côté client BadOptimizations et Create Better FPS.
- Testé sur le serveur local : démarrage, 206 quêtes valides, pré-génération de 10 000 chunks à 20 TPS.
  Pas encore essayés en jeu : trains avec Threaded Trains, viseurs, calculateur, Numismatics.

## Launcher 1.1.0 — 2026-10-29

- **Choix de la version** du pack, comme dans le launcher Minecraft : « Dernière version » ou « Pré-release ».
- **Réglages → Mode développeur** : affiche les pré-releases (versions de test) dans ce choix.
- **Réglages → Désinstaller** : supprime le profil CurseForge, puis le launcher et ses réglages. Les mondes, captures
  et schémas sont d'abord copiés dans `Documents\Create Warfare (sauvegarde)` (case cochée par défaut).

## 1.5.5 — 2026-10-29

- **Chat Heads** (client) : la tête du joueur s'affiche à côté de ses messages dans le chat.
- Traductions françaises complétées pour Chat Heads, Serene Seasons (noms des saisons), YACL et Advanced Weather.
- Solo/LAN : plus de plantage « ThreadLocalRandom accessed from a different thread » quand des feuilles se décomposent
  (Falling Leaves + Create: The Air Wars, détecté par C2ME) : `config/c2me.toml` en fait un simple avertissement.
- **Advanced Weather** : météo simulée (pression, vent, 28 types de temps, grêle, blizzards), instruments, vent sur
  les moulins Create et les engins d'Aeronautics. Livré **patché** : la 0.1.2 faisait planter les serveurs dédiés
  (deux mixins client déclarés communs).
- **Serene Seasons** : saisons, avec la météo et la température d'Advanced Weather qui les suivent.
- Particle Rain retiré : Advanced Weather remplace le rendu de la pluie.
- Advanced Weather se déclare incompatible avec les shaders Iris (brouillard) : à vérifier si tu les actives.

## Launcher 1.0.1 — 2026-10-28

- L'exe et la fenêtre ont une icône (engrenage). Le launcher 1.0.0 se met à jour tout seul : clic sur « v1.0.1 disponible ».

## 1.5.3 — 2026-10-28

- **PureBDcraft 256x** pré-activé, au-dessus de Fresh Animations (options par défaut). Le pack n'est **pas inclus** :
  il n'est pas sur CurseForge et ses conditions d'utilisation interdisent de le redistribuer. Chaque joueur le télécharge
  sur https://bdcraft.net et le dépose dans `resourcepacks/` sous le nom exact `PureBDcraft 256x MC121.zip`.
  Sans le fichier, Minecraft l'ignore. Les options par défaut ne s'appliquent qu'à une nouvelle instance :
  sur une instance existante, l'activer à la main (Options → Packs de ressources).
- Mises à jour : Create: Radars 0.4.9.4 → **5.0.2** (grosse version, à vérifier en jeu), Create: Avionics 0.7.0,
  Create Encased 1.10.0, Create Cobblestone, Create Radiologistics, Fresh Animations 1.11.1, JEI, JER, FTB Quests,
  FTB Library, Overgeared, Sophisticated Backpacks/Core, Simple Voice Chat, C2ME, ModernFix, Corpse, Friends&Foes,
  Burnt, Better Library, MezzConfig, Fzzy Config.
- Serveur de test : démarrage vérifié, aucune quête invalide. Rien n'a été essayé en jeu.

## 1.5.2 — 2026-10-28

- Le serveur **Create Warfare** (`jeu.robocnop.be:27018`) est déjà dans la liste Multijoueur.
  Une nouvelle installation le reçoit avec le zip (`servers.dat`). Une instance existante le reçoit par le launcher,
  qui l'ajoute **à la fin** de la liste du joueur sans retirer les autres serveurs, et une seule fois : un joueur qui le supprime ne le revoit pas.
- Aucun mod, aucune config, aucune quête ne change. Rien ne change côté serveur.

## 1.5.1 — 2026-10-27

- **Traductions françaises** des mods ajoutés en 1.4.0 et 1.5.0 : 1034 textes (`kubejs/assets/<mod>/lang/fr_fr.json`).
  Kaleidoscope Cookery (577 : objets, plats, effets, progrès, Ponder), Inventory Profiles Next (389, menus de config
  et infobulles des boutons), libIPN, Durability Tooltip, CC Better Recipes, CC:CBC, et les quelques textes manquants
  de SecurityCraft et CC: Tweaked (déjà traduits par leurs auteurs). Security Breach était déjà complet.
- Rien ne change côté serveur : ce sont des textes affichés par le client.

## 1.5.0 — 2026-10-27 (⚠️ non testée en jeu)

> Seul le démarrage du serveur de test a été vérifié (aucune quête invalide). Rien n'a été essayé en jeu :
> à tester avant de la déployer sur le serveur.

### Ajouts
- **Kaleidoscope Cookery** : cuisine immersive (fourneau, wok, marmite, planche à découper, cuiseur vapeur, plus de 400 recettes).
  Son moulin accepte le moulin de Create (compatibilité intégrée), ses entités suivent les engins Sable.
  Ses cuisines de village n'apparaissent que dans les nouveaux chunks.
- **Durability Tooltip** (+ SuperMartijn642's Config Lib) : la durabilité s'affiche dans l'info-bulle des objets. Client seul.
- **Inventory Profiles Next** (+ libIPN) : boutons de tri et de dépôt rapide sur les coffres et l'inventaire, tri **par nom**
  par défaut. Client seul.
- **Chapitre de quêtes « Cuisine »** (11 quêtes, 169 au total).

### Erreur connue, sans gravité
- `Couldn't load tag kaleidoscope_cookery:cookery_mod_items` : le tag vise des tabourets compatibles Quark, absent du pack.

## 1.4.1 — 2026-10-27

- Quêtes « Renforceur universel » et « Murs renforcés » : elles présentent les blocs renforcés comme des **bunkers
  anti-nucléaires** (les bombes et ICBM de Project: Explosive n'y font rien, l'artillerie CBC, Superb Warfare et la TNT si).
- Aucun mod ni monde modifié : seul le texte des quêtes change.

## 1.4.0 — 2026-10-27

### Ajouts
- **SecurityCraft** : blocs renforcés, claviers à code, caméras, mines, tourelles, système Trophy...
  - **CBC Security Breach** : les obus Create Big Cannons abîment puis cassent les blocs renforcés (points de vie par matériau,
    fissures visibles qui se referment lentement).
  - **SC-SuperB Bridge** : les explosions de Superb Warfare et la TNT entament aussi les blocs renforcés (à partir de 0,75 TNT).
  - Testé sur un monde jetable : TNT sur pierre renforcée → 8 à 13 blocs cassés (87 sur pierre normale).
    **Les bombes et ICBM de Project: Explosive ne cassent pas les blocs renforcés** : ils sautent les blocs incassables
    (dureté -1), et rendre ces blocs cassables permettrait à n'importe qui de les miner à la main.
    **Gardé volontairement (choix de Robo)** : ça permet de construire des **bunkers anti-nucléaires**. Une base renforcée
    reste vulnérable à l'artillerie CBC, à Superb Warfare et à la TNT, mais protège de Fat Man et des ICBM.
  - Recettes liées à Create (`kubejs/server_scripts/balance/securitycraft.js`) : renforceurs avec un mécanisme de précision,
    tourelle avec acier TFMG et tubes électroniques, système Trophy avec circuits imprimés TFMG.
  - Config : blocs partagés avec l'équipe FTB, les caméras ne chargent plus de chunks (performances).
- **CC: Tweaked** : ordinateurs, tortues, moniteurs, modems.
  - **CC:CBC** : un ordinateur vise et fait tirer un affût de canon (DCA automatique avec les radars).
  - **Create: Avionics** : périphériques pour Aeronautics, Simulated et Offroad (pilote automatique).
  - **Create: CC Better Recipes** : les recettes de CC: Tweaked passent par Create.
- **Chapitre de quêtes « Défense & informatique »** (15 quêtes, 158 au total).

### Mise à jour
- Monde conservé : aucun de ces mods ne génère de minerai ou de structure.
- Mekanism et Electrodynamics étudiés et écartés (industrie et électricité parallèles à Create, MekaSuit déséquilibrée en PvP).

## 1.3.2 — 2026-10-27 (serveur seulement)

- Profil spark pris en volant vite sur le serveur : **58 % du thread principal** passait à écrire dans `debug.log`.
  Railways Untold vérifie les structures le long de ses routes, et Create Structures Arise écrit une ligne DEBUG à chaque
  lecture de sa config. `-Dforge.logging.debugFile.level=info` ajouté aux arguments du serveur (`user_jvm_args.txt`,
  et startup du panel sur le serveur réel) : plus de lignes DEBUG une fois le serveur lancé, `latest.log` inchangé.
- Rien ne change pour les joueurs : inutile de réinstaller le pack côté client.

## 1.3.1 — 2026-10-27

### Performances : cause du lag en combat trouvée
- Analyse du log du vrai serveur (combat du 22/10, 16:17–16:55, 2 joueurs) : 8 retards « Can't keep up » (jusqu'à
  **16 s**) et 43 ticks bloqués. **24 des 43** attendaient un chunk chargé par les projectiles de Superb Warfare
  (`ProjectileChunkSavedData.loadChunkNoGenerate`), les autres aussi des attentes de chunk.
- `projectile_chunk_loading = false` dans `config/superbwarfare-server.toml` : un obus de Superb Warfare qui sort de
  la zone chargée par les joueurs s'arrête au lieu de figer le serveur. Les canons Create Big Cannons ne sont pas concernés.
- Le monde de ce combat était neuf, **sans bordure ni pré-génération** : des chunks étaient générés jusqu'à 22 000 blocs
  du centre pendant la partie. Bordure 6000 et pré-génération Chunky (rayon 3600) indispensables avant de jouer.

## 1.3.0 — 2026-10-27

### Ajouts
- **Railways Untold** + **Railways Additions** : réseau de rails Create généré dans le monde (gares dans les villages,
  ponts, tunnels), régulateur de vitesse des trains, relieur de gares et « Track Generator » pour les anciens chunks.
  Les rails n'apparaissent que dans les **nouveaux chunks** (ou pendant la pré-génération Chunky).
- **Chapitre de quêtes « La forge »** (en tête du livre, 17 quêtes) : guide d'Overgeared, de la roche taillée à la
  pioche en diamant (enclumes, pinces, lingots chauffés, mini-jeu de forge, plans, acier, gabarit de diamant).

### Corrections
- **Chapitre 7 (Blindés) débloqué** : l'acier au carbone et l'ERA de Neo Warfare n'avaient aucune recette.
  `kubejs/server_scripts/balance/blindage.js` : lingot = acier + coke au mélangeur chauffé, bloc = 4 lingots,
  ERA = charge de poudre déployée sur un bloc d'acier au carbone (+ variantes vert / sable, dalles, escaliers).
  Quêtes ramenées à 16 blocs d'acier au carbone et 8 d'ERA.
- **Neo Warfare : les blocs cassés ne lâchaient rien.** Ses 106 tables de butin étaient dans l'ancien dossier 1.20
  (`loot_tables/`) ; elles sont recopiées au chemin 1.21 dans `kubejs/data/cbcmodernwarfare/loot_table/`.
- Quête « Moule de canon » : demande `very_small_cast_mould` (le `cannon_cast` est un bloc sans objet).
- Quête « Brouilleur » remplacée par « Radar d'avion » : `create_radar:jammer` n'existe pas dans le jeu.

### Performances (combats de véhicules)
- **Superb Warfare** (`config/superbwarfare-server.toml`) : synchronisation des entités hors de portée toutes les
  4 ticks au lieu de chaque tick ; projectiles limités à 64 chunks chargés (8 nouveaux par tick) au lieu de 256 (32).
- **Serveur** (`server-files/`) : `view-distance` 10 → 8, `simulation-distance` 8 → 6 ; arguments G1 complets
  (réglages d'Aikar pour 10 Go).
- À appliquer à la main sur un serveur déjà installé : copier `user_jvm_args.txt`, les deux distances de
  `server.properties` et `config/superbwarfare-server.toml`, puis redémarrer.
