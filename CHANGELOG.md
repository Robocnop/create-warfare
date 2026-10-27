# Journal des versions

Les versions 1.1.0 à 1.2.0 sont détaillées dans `docs/journal/2026-10-22.md` et sur la page des releases GitHub.

## 1.4.0 — 2026-10-27

### Ajouts
- **SecurityCraft** : blocs renforcés, claviers à code, caméras, mines, tourelles, système Trophy...
  - **CBC Security Breach** : les obus Create Big Cannons abîment puis cassent les blocs renforcés (points de vie par matériau,
    fissures visibles qui se referment lentement).
  - **SC-SuperB Bridge** : les explosions de Superb Warfare et la TNT entament aussi les blocs renforcés (à partir de 0,75 TNT).
  - Testé sur un monde jetable : TNT sur pierre renforcée → 8 à 13 blocs cassés (87 sur pierre normale).
    **Les bombes et ICBM de Project: Explosive ne cassent pas les blocs renforcés** : ils sautent les blocs incassables
    (dureté -1), et rendre ces blocs cassables permettrait à n'importe qui de les miner à la main.
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
