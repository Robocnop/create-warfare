# Journal des versions

Les versions 1.1.0 à 1.2.0 sont détaillées dans `docs/journal/2026-10-22.md` et sur la page des releases GitHub.

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
