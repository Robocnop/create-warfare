# Create Warfare

Modpack **Minecraft 1.21.1 / NeoForge 21.1.264** construit autour de **Create 6** : on bâtit une industrie, puis on fait la guerre avec.
PvP entre équipes sur serveur dédié, textes et quêtes en français.

| | |
|---|---|
| **Serveur** | `jeu.robocnop.be:27018` (déjà dans la liste Multijoueur depuis la 1.5.2) |
| **Mods** | 199 (167 client + serveur, 30 client seul, 2 serveur seul) — détail dans [MODLIST.md](MODLIST.md) |
| **Quêtes** | 204 quêtes FTB en français, 14 chapitres, de la première roue dentée à l'ICBM et à l'espace |
| **RAM conseillée** | 10 Go (réglée d'office à l'import CurseForge) |
| **Versions** | [CHANGELOG.md](CHANGELOG.md) |

## Au programme

- **Industrie** : Create et ses addons, électricité (Create: New Age, Create Crafts & Additions), pétrole et acier (TFMG), trains (Steam 'n' Rails).
- **Aviation physique** : Create Aeronautics + Sable, avec avionique pilotable par ordinateur (CC: Tweaked, Create: Avionics).
- **Artillerie et blindés** : Create Big Cannons et ses addons, véhicules et armes modernes (Superb Warfare).
- **Missiles et nucléaire** : radars, missiles guidés, bombes et ICBM nucléaires (Project: Explosive). Les ICBM ne se posent qu'avec des codes de lancement obtenus par une quête.
- **Défense** : SecurityCraft. Ses blocs renforcés résistent aux bombes, de vrais bunkers anti-nucléaires.
- **Le reste** : exploration (Terralith, structures YUNG's), cuisine (Kaleidoscope Cookery), cartes Xaero, JEI et EMI, Jade, shaders.

## Jouer

### Avec le launcher (recommandé)

1. Télécharger **`CreateWarfareLauncher.exe`** depuis les [releases `launcher-v…`](https://github.com/Robocnop/create-warfare/releases?q=launcher) de ce dépôt.
2. Le lancer. Windows peut afficher « Windows a protégé votre ordinateur » parce que l'exe n'est pas signé :
   cliquer sur **Informations complémentaires**, puis **Exécuter quand même**.
3. Suivre les 3 étapes de la première installation : télécharger le zip, l'importer dans CurseForge, revenir.
4. Ensuite, le launcher annonce chaque nouvelle version. **Mettre à jour** remplace seulement les fichiers du pack.
   Les touches, réglages, mondes, cartes, packs de textures et mods ajoutés à la main ne sont pas touchés.
   Tout est sauvegardé avant d'être remplacé. **Jouer** ouvre CurseForge.
5. Le launcher se met aussi à jour tout seul : quand la case LAUNCHER affiche « vX disponible », un clic suffit,
   pas besoin de retélécharger l'exe. Une nouvelle version peut mettre jusqu'à 5 minutes à apparaître : rouvrir le launcher.

### Pack de textures PureBDcraft (optionnel)

PureBDcraft 256x est pré-activé (au-dessus de Fresh Animations) mais **n'est pas fourni** : ses conditions interdisent
de le redistribuer. Le télécharger sur [bdcraft.net](https://bdcraft.net), le déposer dans le dossier `resourcepacks/`
de l'instance sous le nom exact `PureBDcraft 256x MC121.zip`. Sur une instance déjà créée avant la 1.5.3, l'activer
à la main : Options → Packs de ressources.

### À la main (CurseForge)

1. Télécharger `CreateWarfare-<version>.zip` depuis les releases.
2. CurseForge → Minecraft → **Créer un profil personnalisé** → **Importer** → choisir le zip.
3. Lancer. Le livre de quêtes s'ouvre depuis l'icône FTB Quests de l'inventaire.
4. Shader (optionnel, PC costaud) : Options → Vidéo → Shaders → **Complementary Reimagined**.

## Héberger un serveur

Prérequis : **Java 21**, **Python 3.11+** et 10 Go de RAM.

1. Décompresser `CreateWarfare-Server-<version>.zip` dans un dossier vide.
2. Lancer `install.bat` (Windows) ou `install.sh` (Linux). Le script installe NeoForge et télécharge les mods côté serveur.
3. Mettre `eula=true` dans `eula.txt` (acceptation de l'EULA Minecraft).
4. Lancer `start.bat` / `start.sh`. Port par défaut : 25565.
5. Avant d'ouvrir : poser une bordure de monde et pré-générer la carte avec Chunky. Les explosions nucléaires gèlent le serveur
   quand elles touchent des chunks pas encore générés : pré-générer au-delà de la bordure (rayon de l'explosion, environ 600 blocs).

Déjà réglé : `pvp=true`, `allow-flight=true` (avions et jetpacks), `max-tick-time=-1` (pas d'arrêt pendant les grosses explosions).

### Règles de guerre (à décider entre vous)

- Par défaut, les chunks réclamés avec **FTB Chunks** sont protégés des explosions des autres équipes.
- **Guerre totale** : dans `world/serverconfig/ftbchunks-world.snbt`, mettre `disable_protection: true`. Plus aucun claim ne protège alors.
- Puissance et destruction : `config/projectexplosive-common.toml` (section `[explosion]`) et
  `world/serverconfig/createbigcannons-server.toml` (dégâts des obus).

## Contenu du dépôt

| Dossier / fichier | Rôle |
|---|---|
| `pack.toml`, `index.toml`, `mods/` | Le pack, au format [packwiz](https://packwiz.infra.link/) (métadonnées des mods, pas les jars) |
| `config/`, `kubejs/`, `servers.dat` | Fichiers livrés dans le zip : configs, recettes et scripts KubeJS, traductions FR, quêtes, liste de serveurs |
| `server-files/` | Scripts d'installation et de démarrage du serveur |
| `tools/` | Construction (`build.sh`), test serveur (`test_server.sh`), quêtes (`gen_quests.py`), téléchargement des mods (`fetch_mods.py`), publication (`publish_launcher.py`) |
| `launcher/` | Le launcher Windows (WPF .NET 8), voir [launcher/README.md](launcher/README.md) |

## Maintenir le pack

1. Ajouter un mod : `packwiz cf add https://www.curseforge.com/minecraft/mc-mods/<slug>`. Toujours l'URL complète :
   avec un simple nom, packwiz peut prendre un modpack au lieu du mod.
2. Mettre à jour : `packwiz update --all`, puis **tester** (des addons Create cassent parfois les mondes existants).
3. Tester le serveur : `bash tools/test_server.sh` (démarre `server-test/` jusqu'à « Done » et affiche les erreurs).
4. Quêtes : modifier le tableau `CHAPTERS` de `tools/gen_quests.py`, puis `python tools/gen_quests.py`.
   Les IDs de quête sont stables, donc la progression des joueurs est gardée.
5. Serveurs de la liste Multijoueur : `SERVERS` dans `tools/gen_servers_dat.py`, puis `python tools/gen_servers_dat.py`.
6. Publier : incrémenter `version` dans `pack.toml`, `bash tools/build.sh`, commit, `gh release create v<version> dist/CreateWarfare*-<version>.zip`,
   puis `python tools/publish_launcher.py --upload` pour les joueurs qui utilisent le launcher.

## Licence

Le travail original de ce dépôt (outils, launcher, scripts KubeJS, quêtes, configs, documentation) est sous [licence MIT](LICENSE).
**Les mods ne sont pas concernés** : chacun appartient à ses auteurs et garde sa propre licence. Les mods « All Rights Reserved »
ne sont jamais redistribués : CurseForge ou le launcher les télécharge chez le joueur.
