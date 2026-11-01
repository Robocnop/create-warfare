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
  Sur l'accueil, les images défilent toutes les 8 s (départ au hasard) ; un bouton discret à droite des points
  les remplace par l'illustration du thème (réglage `homePhotos`, gardé).
  Une image distante est téléchargée une fois puis mise en cache (`%LOCALAPPDATA%\CreateWarfareLauncher\images`).
  Une image illisible est ignorée ; sans aucune image, l'illustration dessinée (engrenages) s'affiche.
  Discord et Site restent cachés tant que leur adresse est vide.
- Essai local : `CW_LAUNCHER_CONTENT=C:\chemin\launcher-content.json`.

### Thèmes (1.3)
Base (acier et orange), Halloween, Pâques, Noël, Été. Chaque thème est une surcouche : il change les couleurs et le décor,
jamais le code des composants.

| Pièce | Fichier |
|---|---|
| Dates des saisons (le seul fichier à toucher pour les changer) | `src/CreateWarfare.Launcher.Core/Services/ThemeSchedule.cs` |
| Clés des thèmes, choix du thème (réglage, date, `CW_THEME`) | `src/CreateWarfare.Launcher.Core/Services/SeasonalTheme.cs` |
| Liste des thèmes (palette, illustration, décor) | `src/CreateWarfare.Launcher/Themes/ThemeRegistry.cs` |
| Couleurs (mêmes clés dans chaque fichier) | `src/CreateWarfare.Launcher/Themes/Palettes/<Thème>.xaml` |
| Illustrations (engrenages réinterprétés) | `src/CreateWarfare.Launcher/Controls/Scenes/<Thème>Scene.cs` |
| Décor d'ambiance de l'accueil (neige, pétales, lumières) | `src/CreateWarfare.Launcher/Controls/Scenes/Ambient.cs` |

**Calendrier par défaut** : Halloween 15/10 → 02/11, Noël 01/12 → 06/01, Pâques de J-14 au lundi de Pâques (date calculée
chaque année, algorithme de Meeus/Jones/Butcher), Été 21/06 → 22/09, thème de base le reste du temps. Bornes incluses.
Si deux périodes se chevauchent, la priorité la plus haute gagne (Halloween 40 > Noël 30 > Pâques 20 > Été 10).
En mode automatique, la date est revérifiée chaque minute : le thème change seul à minuit.

**Changer une date** : modifier la ligne dans `ThemeSchedule.Periods`, par exemple `Fixed(10, 15)` (15 octobre) ou
`Easter(-14)` (14 jours avant Pâques). Une période dont la fin précède le début se termine l'année suivante.
Relancer `dotnet test` (les bornes sont testées), puis publier le launcher.

**Ajouter un thème** (ex. Nouvel An) :
1. une clé dans `SeasonalTheme` (et dans `SeasonalTheme.All`) ;
2. une période dans `ThemeSchedule.Periods` si le thème est saisonnier ;
3. une palette `Themes/Palettes/<Nom>.xaml` (copier une palette existante, garder toutes les clés) ;
4. une scène `Controls/Scenes/<Nom>Scene.cs` (hériter de `HeroScene` ; `SceneKit.Train` donne les engrenages engrenés),
   et au besoin un `AmbientDecor` ;
5. une ligne dans `ThemeRegistry.All` ;
6. son nom (`s.theme.<clé>`) dans `Strings/fr.xaml` et `Strings/en.xaml`.
Le menu, les aperçus des paramètres et le planificateur le prennent ensuite en compte seuls.

**Joueur** : Paramètres → Apparence. Mode Automatique (saisonnier), Thème de base uniquement ou Choisir un thème ;
aperçus animés de chaque thème (un clic l'applique) ; « Animer le décor » (aussi coupé quand Windows réduit les
animations). Changement en fondu, sans redémarrer.

**Essais** :
- `CW_THEME=default|halloween|easter|christmas|summer` force un thème (prime sur le réglage) ;
- `CW_DATE=2027-03-28` simule la date du jour (le mode automatique s'en sert) ;
- `CW_START_PAGE=settings:appearance` ouvre directement l'onglet (autres : `game`, `launcher`, `instance`, `advanced`).

### Textes et langue
- Tous les textes de l'interface sont dans `Strings/fr.xaml` (référence) et `Strings/en.xaml` (mêmes clés). Essai : `CW_LANG=en`.
- Quelques messages d'état de la logique d'installation (MainViewModel.cs) restent en français.

### Essais de l'interface
- `CW_START_PAGE=news|gallery|settings|logs|about` ouvre directement une page (captures d'écran).
