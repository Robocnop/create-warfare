# Create Warfare

Modpack Minecraft **1.21.1 / NeoForge** construit autour de **Create** : base industrielle, électricité,
pétrole, nucléaire, trains, aviation physique (Create Aeronautics + Sable), artillerie (Create Big Cannons),
blindés, radars, missiles guidés et ICBM nucléaires (Project: Explosive). PvP entre équipes, avec claims FTB Chunks.
Progression guidée par **122 quêtes FTB Quests en français** (10 chapitres, de l'engrenage à l'ICBM).

Liste complète des mods et notes de compatibilité : [MODLIST.md](MODLIST.md).

## Jouer (CurseForge)
1. Application CurseForge → Minecraft → **Créer un profil personnalisé** → **Importer** → choisir `dist/CreateWarfare-1.1.1.zip`.
2. Paramètres du profil → mémoire allouée : **10 Go** (réglée par défaut à l'import).
3. Lancer. Le livre de quêtes s'ouvre depuis l'icône FTB Quests dans l'inventaire.
4. Shader (optionnel, PC costaud) : Options → Vidéo → Shaders → **Complementary Reimagined**.
   Fresh Animations (mobs animés) est activé d'office ; le statut Discord s'affiche tout seul.

## Serveur dédié
Prérequis : **Java 21** et **Python 3.11+**. 10 Go de RAM conseillés.
1. Décompresser `dist/CreateWarfare-Server-1.1.1.zip` dans un dossier vide.
2. Lancer `install.bat` (Windows) ou `install.sh` (Linux). Le script installe NeoForge et télécharge les 114 mods côté serveur.
3. Mettre `eula=true` dans `eula.txt` (acceptation de l'EULA Minecraft).
4. Lancer `start.bat` / `start.sh`. Port par défaut : 25565.
5. Recommandé avant d'ouvrir le serveur : pré-générer la carte avec Chunky (`/chunky radius 3000` puis `/chunky start`).

Réglages déjà mis : `pvp=true`, `allow-flight=true` (avions et jetpacks), `max-tick-time=-1` (pas d'arrêt pendant les grosses explosions).

### Règles de guerre (à décider entre vous)
- Par défaut, les chunks réclamés avec **FTB Chunks** sont protégés des explosions des autres équipes.
- **Guerre totale** : dans `world/serverconfig/ftbchunks-world.snbt`, mettre `disable_protection: true`, et plus aucun claim ne protège.
- Puissance et destruction : `config/projectexplosive-common.toml` (section `[explosion]`) et
  `world/serverconfig/createbigcannons-server.toml` (dégâts des obus).

## Maintenir le pack
Le pack est géré avec [packwiz](https://packwiz.infra.link/) (installé via `go install github.com/packwiz/packwiz@latest`).
- Ajouter un mod : `packwiz cf add https://www.curseforge.com/minecraft/mc-mods/<slug>` (toujours l'URL complète :
  avec un simple nom, packwiz peut prendre un modpack au lieu du mod).
- Mettre à jour : `packwiz update --all`, **puis tester** (les addons Create cassent parfois les mondes existants, par exemple CBC 5.11.4).
- Tester le serveur : `bash tools/test_server.sh` (démarre `server-test/` jusqu'à « Done » et affiche les erreurs).
- Quêtes : modifier le tableau `CHAPTERS` dans `tools/gen_quests.py`, puis `python tools/gen_quests.py`
  (ajouter le chemin d'un `ids.json` des items pour vérifier les IDs). Les IDs de quête sont stables, donc la progression est conservée.
- Construire les zips : `bash tools/build.sh` (incrémenter `version` dans `pack.toml` avant).
