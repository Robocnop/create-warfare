# Create Warfare Launcher

Application Windows (WPF, .NET 8, un seul exe autonome) qui installe et met à jour l'instance CurseForge
« Create Warfare » sans toucher aux réglages du joueur.

## Pour les joueurs

1. Télécharger `CreateWarfareLauncher.exe` depuis les releases `launcher-v…` de `Robocnop/create-warfare`.
2. Le lancer. L'exe n'est pas signé : SmartScreen affiche « Windows a protégé votre ordinateur ».
   Cliquer sur **Informations complémentaires**, puis **Exécuter quand même** (une seule fois).
3. **Première installation** : le launcher télécharge le zip du pack et guide l'import dans CurseForge
   (Minecraft → Créer un profil personnalisé → Importer). CurseForge installe Minecraft, NeoForge et Java.
4. **Ensuite** : ouvrir le launcher. S'il affiche « Mise à jour disponible », fermer CurseForge, cliquer sur
   *Mettre à jour*, vérifier la liste, puis confirmer. *Jouer* ouvre CurseForge.

Ce qui n'est **jamais** modifié : `options.txt` (touches), `servers.dat`, `saves/`, `screenshots/`, `resourcepacks/`,
`shaderpacks/`, `schematics/`, les cartes Xaero, `config/inventoryprofilesnext/` (raccourcis d'IPN), et tout mod
ajouté à la main. Un mod désactivé dans CurseForge (`.jar.disabled`) reste désactivé.

En cas de souci : `LOGS` dans la barre du haut ouvre `%LOCALAPPDATA%\CreateWarfareLauncher\logs`.
Les fichiers remplacés ou supprimés sont sauvegardés dans `<instance>\.cw-backup\<date>\` (les 2 dernières mises à jour).

## Fonctionnement

| Étape | Code |
|---|---|
| Trouver l'instance (`storage.json` de CurseForge, champ `name` de `minecraftinstance.json`) | `Core/Services/CurseForgeLocator.cs`, `InstanceFinder.cs` |
| Lire `version.json` (HTTP sans token, ou chemin local) | `Downloader.FetchManifestAsync` |
| Télécharger le zip, extraire `overrides/` | `Downloader.GetOverridesAsync` |
| Comparer l'instance (sha256 des overrides, sha1 des jars) → plan | `UpdatePlanner.Plan` |
| Télécharger les jars (Modrinth, puis CDN CurseForge), vérifier, sauvegarder, appliquer, revenir en arrière en cas d'erreur, écrire `.cw-launcher.json` en dernier | `UpdateApplier.ApplyAsync` |
| Auto-update (`.new` → `.old`, relance) | `SelfUpdater` |
| Interface | `CreateWarfare.Launcher/MainWindow.xaml`, `ViewModels/MainViewModel.cs` |

Règles de suppression : le launcher ne supprime que ce que le pack a posé, c'est-à-dire :
- les fichiers de son état `.cw-launcher.json` ;
- s'il n'a pas encore d'état, la liste `modpackOverrides` écrite par CurseForge à l'import ;
- un jar d'une ancienne version (`retiredMods`) seulement si son **sha1** correspond.

Un jar du joueur qui porte le même nom n'est donc pas touché.

Les jars ne sont **pas** redistribués (mods « All Rights Reserved ») : `version.json` donne pour chaque jar son sha1 et
ses URL (Modrinth, CDN CurseForge), et le launcher les télécharge lui-même, comme `tools/fetch_mods.py`.

## Développer

```bash
cd launcher
dotnet build
dotnet test                       # 47 tests, fausse instance dans %TEMP%
```

Tester contre un `version.json` local (sans rien publier) :

```bash
python tools/publish_launcher.py --local          # dist/launcher/version.json, URL = chemins locaux
CW_LAUNCHER_MANIFEST="C:\...\dist\launcher\version.json" dotnet run --project launcher/src/CreateWarfare.Launcher
```

Dry-run sur une vraie instance (lecture seule, rien n'est écrit) :

```bash
CW_REAL_INSTANCE="C:\Users\roboc\curseforge\minecraft\Instances\Create Warfare (1)" \
CW_LAUNCHER_MANIFEST="C:\...\dist\launcher\version.json" \
dotnet test launcher/tests/CreateWarfare.Launcher.Tests --filter RealInstanceDryRun --logger "console;verbosity=detailed"
```

Image de gauche : déposer `src/CreateWarfare.Launcher/Assets/hero.jpg` (ou `.png`), puis recompiler. Sans image,
une illustration dessinée (engrenages) est affichée. Icône : `Assets/icon.ico`.

## Publier

### Une version du pack

1. Suivre « Maintenir le pack » dans le README principal (test serveur, `tools/build.sh`, release `v<version>`).
2. `python tools/publish_launcher.py --upload`
   - ajoute le zip à la release `v<version>` de `Robocnop/create-warfare` s'il y manque (et crée la release si besoin) ;
   - commite `version.json` à la racine du dépôt et pousse `master`. Les launchers le lisent sur
     `raw.githubusercontent.com/Robocnop/create-warfare/master/version.json` (cache d'environ 5 min). Le fichier est exclu du pack par `.packwizignore`.
   - Option `--notice "texte"` : annonce affichée en haut de l'accueil.

### Une version du launcher

1. Incrémenter `<Version>` dans `src/CreateWarfare.Launcher/CreateWarfare.Launcher.csproj`.
2. `dotnet publish launcher/src/CreateWarfare.Launcher -c Release -o dist/launcher`
3. `python tools/publish_launcher.py --upload` : crée la release `launcher-v<version>` avec l'exe et met son
   SHA-256 dans `version.json`. Les launchers installés se mettent à jour au clic.
   Le launcher ne lit `version.json` qu'à l'ouverture, et `raw.githubusercontent.com` le garde en cache jusqu'à 5 min :
   un launcher ouvert juste après la publication affiche encore « à jour ». Le rouvrir après quelques minutes.
   Vérifier ce que GitHub sert : `curl -s https://raw.githubusercontent.com/Robocnop/create-warfare/master/version.json`.
   Testé le 28/10 : l'exe 1.0.0 de la release passe seul en 1.0.1 (remplacement, relance, `.old` supprimé).
4. Si un nouveau pack a besoin du nouveau launcher : `--min-launcher <version>`. Les anciens launchers bloquent
   alors la mise à jour du pack et proposent d'abord celle du launcher.

## Interface 1.2 : contenu, images, thèmes

### Images du carrousel et de la galerie
- **Intégrées à l'exe** : déposer des `.png` / `.jpg` dans `launcher/src/CreateWarfare.Launcher/Assets/screenshots/`, puis recompiler
  et publier le launcher. Ordre d'affichage = ordre alphabétique des noms (`01-base.png`, `02-artillerie.png`…). Idéal : 1920×1080.
- **Sans republier** : `launcher-content.json` à la racine du dépôt (lu au démarrage, gardé en copie locale pour le hors-ligne) :
  ```json
  { "gallery": [ { "image": "https://raw.githubusercontent.com/Robocnop/create-warfare/master/launcher/screens/base.jpg", "caption": "La base" },
                 { "image": "01-base.png", "caption": "Légende d'une image intégrée" } ],
    "news":    [ { "title": "Saison 2", "date": "2026-11-15", "tag": "Événement", "body": "Texte…", "image": "https://…" } ],
    "links":   { "discord": "", "website": "", "github": "https://github.com/Robocnop/create-warfare" },
    "server":  { "name": "Serveur Create Warfare", "host": "jeu.robocnop.be", "port": 27018 } }
  ```
  Une image distante est téléchargée une fois puis mise en cache (`%LOCALAPPDATA%\CreateWarfareLauncher\images`).
  Une image illisible est ignorée ; sans aucune image, l'illustration dessinée (engrenages) s'affiche.
  Discord et Site restent cachés tant que leur adresse est vide.
- Essai local : `CW_LAUNCHER_CONTENT=C:\chemin\launcher-content.json`.

### Thèmes
- Identité par défaut : `Themes/Palettes/Default.xaml` (acier + orange). Saisons : `Halloween.xaml` (15/10–05/11),
  `Christmas.xaml` (01/12–06/01), `Summer.xaml` (juillet–août). Mêmes clés de couleur dans chaque palette.
- Le joueur choisit dans Paramètres → Launcher → Thème (« Automatique » suit la date). Essai : `CW_THEME=halloween|christmas|summer|default`.
- Ajouter une saison : copier une palette, l'ajouter à `SeasonalTheme.All` et à `ThemeManager.Apply`, puis à `ThemeOptions`.

### Textes et langue
- Tous les textes de l'interface sont dans `Strings/fr.xaml` (référence) et `Strings/en.xaml` (mêmes clés). Essai : `CW_LANG=en`.
- Quelques messages d'état de la logique d'installation (MainViewModel.cs) restent en français.

### Essais de l'interface
- `CW_START_PAGE=news|gallery|settings|logs|about` ouvre directement une page (captures d'écran).
