# Create Warfare Launcher

Application Windows (WPF, .NET 8, autonome) qui installe et met à jour l'instance CurseForge
« Create Warfare » sans toucher aux réglages du joueur.

## Pour les joueurs

1. Télécharger `CreateWarfare-win-Setup.exe` depuis les releases `launcher-v…` de `Robocnop/create-warfare` (launcher 2.0 et
   suivants ; avant, `CreateWarfareLauncher.exe`, exe portable).
2. Le lancer : il s'installe seul dans `%LOCALAPPDATA%\CreateWarfare` (sans droits administrateur), crée les raccourcis du Bureau et du
   menu Démarrer, puis ouvre le launcher. Il n'est pas signé : SmartScreen affiche « Windows a protégé votre ordinateur ».
   Cliquer sur **Informations complémentaires**, puis **Exécuter quand même** (une seule fois). Désinstallation : bouton du launcher
   ou « Applications installées » de Windows. Le launcher se met ensuite à jour seul (deltas de quelques centaines de Ko).
   Un launcher 1.x (exe portable) se met à jour une dernière fois vers ce Setup, qui l'installe ; l'ancien exe, devenu le Setup,
   peut ensuite être supprimé.
3. **Première installation** : le launcher télécharge le zip du pack et guide l'import dans CurseForge
   (Minecraft → Créer un profil personnalisé → Importer). CurseForge installe Minecraft, NeoForge et Java.
4. **Ensuite** : ouvrir le launcher. S'il affiche « Mise à jour disponible », fermer CurseForge, cliquer sur
   *Mettre à jour*, vérifier la liste, puis confirmer.
5. **Jouer** (launcher 2.0, non publié) : le launcher compare d'abord tous les fichiers de l'instance au pack et répare
   ce qui manque (voir « Vérification au clic sur Jouer »), puis lance Minecraft lui-même (voir « Lancement natif »).
   Tant que le Client ID Azure est vide, *Jouer* ouvre CurseForge comme dans les versions 1.x.

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
| Auto-update du launcher installé (Velopack : deltas, `Update.exe`, relance) | `InstalledUpdater` (Windows seulement) |
| Auto-update de l'exe portable 1.x / migration vers le Setup (`.new` → `.old`, relance) ; versions ; Linux | `SelfUpdater` |
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
dotnet test                       # 199 tests (12/11/2026), fausse instance dans %TEMP%, aucun accès réseau
```

### Structure des projets

| Projet | Rôle |
|---|---|
| `src/CreateWarfare.Launcher.Core` | Logique sans interface. Cible `net8.0` (cœur portable, binaire Linux) **et** `net8.0-windows` (launcher WPF). `ProcessGuard.cs`, `CurseForgeLocator.cs` et tout le dossier `Native/` sont **exclus** de la cible `net8.0` (`<Compile Remove=…>` dans le csproj) : le lancement natif n'existe pas dans le build Linux, qui passe par Prism Launcher (`Services/Prism.cs`) |
| `src/CreateWarfare.Launcher` | Interface WPF (`MainWindow.xaml`, `ViewModels/MainViewModel*.cs`, thèmes, textes `Strings/fr.xaml` et `en.xaml`) |
| `src/CreateWarfare.Linux` | Binaire Linux (Prism), voir `../linux/README.md` |
| `tests/CreateWarfare.Launcher.Tests` | xUnit. `FakeInstance.cs` fabrique une fausse instance, `FakeHttp.cs` simule le réseau |

Fichiers du launcher 2.0 : `Core/Native/` (`Accounts.cs`, `GameInstaller.cs`, `GameVersion.cs`, `JavaLocator.cs`, `LaunchCommand.cs`,
`MicrosoftAuth.cs`, `NativeConfig.cs`, `NativeLauncher.cs`, `TokenVault.cs`), `Core/Services/PackVerifier.cs`, `Core/Services/RecommendedPack.cs`,
`Core/Services/ProcessGuard.cs` (corrigé), `ViewModels/MainViewModel.Play.cs` (vérification, carte BDcraft, `PlayAsync`) et
`ViewModels/MainViewModel.Native.cs` (compte, connexion, lancement). Tests : `PackVerifierTests`, `NativeLaunchTests`,
`MicrosoftAuthTests`, `RealNativeLaunchTrial` (essai réel, ignoré sans variables d'environnement).

Lancer un seul groupe : `dotnet test launcher/tests/CreateWarfare.Launcher.Tests --filter FullyQualifiedName~PackVerifierTests`
(ou `NativeLaunchTests`, `MicrosoftAuthTests`).

### Vérification au clic sur Jouer (2.0, étape 1)

`PlayAsync` (`MainViewModel.Play.cs`) appelle d'abord `VerifyBeforePlayAsync`, puis lance (natif, ou CurseForge si le repli est choisi).
La vérification utilise `PackVerifier` (`Core/Services/PackVerifier.cs`) :
1. récupère les `overrides/` du zip du pack (`Downloader.GetOverridesAsync`) ;
2. `PackVerifier.Check` = `UpdatePlanner.Plan(…, repair: false)` : compare le sha256 des fichiers d'overrides et le sha1 des jars de `mods/`
   à l'index du pack (`version.json`), sans rien écrire ;
3. s'il n'y a rien à changer : journal « tous les fichiers sont conformes », aucun fichier écrit, `.cw-launcher.json` non réécrit ;
4. sinon `UpdateApplier.ApplyAsync` (téléchargement des jars manquants, vérification des hashs, sauvegarde dans `.cw-backup\<date>\`,
   retour arrière en cas d'erreur, `.cw-launcher.json` en dernier) ; l'interface affiche « N fichier(s) réparé(s) avant le lancement ».

**Règles de réparation** (celles d'une mise à jour normale, voir `UpdatePlanner.Plan`) :
- jar de `mods/` absent ou dont le sha1 diffère : remis (un `.jar.disabled` présent compte comme conforme) ;
- fichier d'overrides absent : ajouté ; différent : remplacé, **sauf** s'il figure dans la liste `protected` du pack (jamais touché)
  ou si le pack livre le même contenu qu'à la dernière installation (le fichier a été changé par le joueur ou par un mod : il est gardé) ;
- ce bouton n'est donc **pas** le bouton « Réparer » (`repair: true`), qui remet aussi les configs modifiées par le joueur ;
- `resourcepacks/` : un pack manquant est créé, un pack présent mais différent n'est jamais remplacé ni supprimé ;
- suppressions : seulement ce que le pack avait posé (état `.cw-launcher.json`, ou `modpackOverrides` de CurseForge) et les jars
  retirés dont le sha1 correspond ; `options.txt`, `servers.dat`, `saves/`, jars ajoutés par le joueur restent intacts.

**Cas où la vérification est sautée** (le jeu se lance quand même) :
- aucune instance trouvée ou `version.json` injoignable (pas de manifeste distant : bouton « Hors ligne – Jouer ») ;
- CurseForge est ouvert ou le jeu tourne déjà : ils réécrivent l'instance, le launcher n'y touche pas (journal « Vérification avant le jeu ignorée »).

**Échecs** : annulation par le joueur = le jeu n'est pas lancé. Autre erreur (réseau coupé, accès refusé à un fichier) : une boîte
« Vérification impossible » propose *Lancer quand même* ou *Annuler*, et rien n'est modifié si le réseau tombe avant l'application.

### Pack de textures recommandé (carte BDcraft)

L'accueil affiche une carte pour **PureBDcraft 256x**. Le pack est « All Rights Reserved » : le launcher ne le télécharge et ne l'embarque
jamais. `RecommendedPack` (`Core/Services/RecommendedPack.cs`) vérifie seulement que `resourcepacks\PureBDcraft 256x MC121.zip` existe
dans l'instance (le contenu n'est pas contrôlé) et renvoie vers <https://bdcraft.net/downloads/purebdcraft-minecraft/>. Badge « installé » ou
« manquant » + bouton vers le site + bouton qui ouvre le dossier `resourcepacks`. La présence est relue à chaque rafraîchissement de l'accueil.
Le nom du zip est le même que dans `config/defaultoptions/options.txt` du pack (pré-activé dans le jeu).

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

### Lancement natif (2.0, étape 2)

Le bouton Jouer vérifie l'instance, puis lance Minecraft 1.21.1 + NeoForge 21.1.264 lui-même (`Core/Native/`, Windows
seulement : la version Linux passe par Prism). Même ligne de commande que CurseForge (profil vanilla + profil NeoForge :
module path, `--add-opens`, `BootstrapLauncher`), `--gameDir` = l'instance, RAM = `allocatedMemory` de l'instance
(`MemoryAdvisor.ReadAllocated`, sinon la RAM recommandée). Versions et empreintes figées dans `NativeConfig.cs`
(relevées le 12/11/2026).

**Quand il est actif** (`MainViewModel.NativeLaunch`) : Windows, repli CurseForge non choisi (Paramètres → Jeu,
« Ouvrir CurseForge au lieu du lancement direct »), et un Client ID Azure présent (ou `CW_OFFLINE_TEST`). Sinon Jouer ouvre
CurseForge. Si le jeu tourne déjà, le launcher affiche « Minecraft est déjà lancé » au lieu d'en lancer un second.

| Étape | Code |
|---|---|
| Connexion Microsoft : code d'appareil → Xbox Live → XSTS → Minecraft (profil = possession du jeu), jetons chiffrés DPAPI dans `settings.json` | `MicrosoftAuth.cs`, `Accounts.cs`, `TokenVault.cs` |
| Java 21 : CurseForge (`Install\java\java-runtime-delta`), launcher, launcher officiel, `JAVA_HOME`, Program Files ; sinon JRE Temurin (API Adoptium, sha256) | `JavaLocator.cs` |
| Jeu dans `%LOCALAPPDATA%\CreateWarfareLauncher\minecraft` : profils (empreintes figées), bibliothèques, assets, client. Un fichier déjà chez CurseForge avec la bonne empreinte y est **lié** (lien physique), sinon téléchargé. Client patché NeoForge : repris de CurseForge s'il a l'empreinte attendue, sinon installateur officiel `--install-client` (≈ 30 s, une fois) | `GameInstaller.cs`, `NativeConfig.cs` |
| Ligne de commande (fonction pure, jeton masqué dans le journal) | `LaunchCommand.cs` |
| Jeu en cours : PID mémorisé, `logs\latest.log` tenu par un java (Restart Manager), ligne de commande WMI. Sodium réécrit la ligne de commande du jeu (« net.caffeinemc.sodium / … ») : WMI seul ne le voyait plus, même lancé par CurseForge | `ProcessGuard.cs` |

#### Flux de connexion Microsoft (`MicrosoftAuth`, `AccountStore`)
1. `StartDeviceCodeAsync` : POST sur `login.microsoftonline.com/consumers/oauth2/v2.0/devicecode`, scope `XboxLive.signin offline_access`.
   L'interface affiche le code (`UserCode`) et l'adresse ; *Copier et ouvrir* copie le code et ouvre la page.
2. `WaitForDeviceCodeAsync` interroge `/token` à l'intervalle donné (`slow_down` = +5 s) jusqu'à la saisie, le refus, l'expiration ou l'annulation.
3. `LoginMinecraftAsync` : Xbox Live (`user.auth.xboxlive.com`) → XSTS (`rp://api.minecraftservices.com/`) → `login_with_xbox`
   (jeton Minecraft, 24 h par défaut) → `minecraft/profile`. Un profil absent (404) = compte sans Minecraft: Java Edition.
4. Parties suivantes (`GetSessionAsync`) : jeton Minecraft mémorisé encore valide (marge de 5 min) = aucun réseau ; sinon
   rafraîchissement silencieux avec le jeton de rafraîchissement ; sinon (`ReloginRequired`) nouvelle connexion par code.

Erreurs distinguées (`AuthError`) : réseau, annulation, code expiré, refus, session expirée, compte sans profil Xbox (XErr 2148916233),
Xbox indisponible dans le pays (…235), vérification d'âge (…236/237), compte enfant (…238), application non autorisée par Mojang (403),
compte sans Minecraft, trop de requêtes (429), Client ID refusé par Microsoft. Les messages ne contiennent jamais de jeton.

**Stockage du jeton** : `LauncherSettings.Account` (`settings.json`, dans `%LOCALAPPDATA%\CreateWarfareLauncher`). Le jeton de
rafraîchissement et le jeton Minecraft sont chiffrés par DPAPI (`CryptProtectData`, portée utilisateur, entropie propre au launcher),
stockés en base64 : illisibles hors de ce compte Windows (un autre compte Windows doit se reconnecter). Le nom, l'UUID et l'XUID
sont en clair. « Se déconnecter » efface le compte. `--clientId` (identifiant d'installation pour la télémétrie de Mojang) est un
GUID tiré au hasard une fois (`GameClientId`). Le jeton n'est jamais écrit dans le journal (`LaunchCommand.Redacted` le remplace par `***`).

#### Java (`JavaLocator`)
Java 21, 64 bits, `javaw.exe` et fichier `release` présents. Ordre : `CW_JAVA_HOME`, CurseForge (`Install\java\java-runtime-delta`,
`Install\runtime\…`, `Install\java\Jre_21`), Java déjà téléchargé par le launcher (`%LOCALAPPDATA%\CreateWarfareLauncher\java`),
launcher officiel (Microsoft Store, `.minecraft\runtime`), `JAVA_HOME`, puis les dossiers « 21 » de Eclipse Adoptium, Java, Microsoft, Zulu,
BellSoft, Amazon Corretto dans Program Files. Aucun trouvé : `JavaLocator.DownloadAsync` récupère le dernier JRE 21 Temurin
(API Adoptium, adresse contrôlée, sha256 vérifié) et l'extrait dans le dossier `java` du launcher.

#### Installation du client (`GameInstaller`)
1. profil vanilla 1.21.1 (sha1 figé) ; 2. installateur NeoForge (sha256 figé) dont on lit `version.json` et `install_profile.json` ;
3. client, bibliothèques, jar universal de NeoForge, index des assets ; 4. assets (≈ 4 000 fichiers, ensuite contrôlés par leur taille) ;
5. client patché NeoForge (`-client.jar`, `-srg.jar`, `-extra.jar`) : repris d'un miroir s'il a la bonne empreinte, sinon l'installateur
officiel est lancé avec `--install-client` (10 min au plus ; les 15 dernières lignes de sa sortie sont reprises en cas d'échec) ;
6. natives (rien à extraire en 1.21.1). Chaque fichier est vérifié par empreinte (8 en parallèle) ; un fichier sans empreinte n'est
jamais téléchargé. Rien n'est écrit chez CurseForge.

#### Ligne de commande (`LaunchCommandBuilder.Build`)
Arguments JVM du profil vanilla puis NeoForge (module path normalisé comme le classpath) → `-Xms` (512 Mo ou moins) et `-Xmx` (RAM de
l'instance) → options G1 du launcher officiel (`DefaultJvmArgs`) → `javaArgsOverride` de `minecraftinstance.json` (sans `-Xmx`/`-Xms`)
→ classe principale du profil NeoForge → arguments de jeu du profil (`--username`, `--uuid`, `--accessToken`, `--userType`, `--xuid`,
`--clientId`, `--gameDir`, `--assetsDir`, fenêtre 1024×768 au premier lancement). Avec l'option « connexion directe au serveur » :
`--quickPlayMultiplayer jeu.robocnop.be:27018`. Le processus est un `javaw` sans console, dossier de travail = l'instance ;
les variables `CW_*` ne lui sont pas transmises. Si le jeu s'arrête en moins de 5 minutes avec un code non nul, une alerte
propose d'ouvrir les logs de l'instance.

#### Client ID Azure : démarches
`AuthConfig.DefaultClientId` est vide. Tant qu'il l'est (et sans `CW_MS_CLIENT_ID`), le bouton Jouer
ouvre CurseForge comme avant. Pour l'activer :
1. portail Azure → *App registrations* → *New registration* : comptes « Personal Microsoft accounts only »,
   *Authentication → Allow public client flows* = **Yes** (flux par code d'appareil, aucune redirection nécessaire) ;
   pas de secret client (le launcher est une application publique) ;
2. demander l'accès aux services Minecraft pour ce Client ID : formulaire Mojang <https://aka.ms/mce-reviewappid>
   (sans cet accord, `login_with_xbox` répond 403 : erreur `AppNotApproved` dans le journal) ;
3. essayer avec `CW_MS_CLIENT_ID=<id>` avant de l'écrire dans `DefaultClientId`. Jamais le Client ID d'un autre launcher.

#### Variables d'environnement (jamais visibles dans l'interface)

| Variable | Effet |
|---|---|
| `CW_MS_CLIENT_ID` | Client ID Azure à utiliser à la place de `DefaultClientId` |
| `CW_OFFLINE_TEST=pseudo` | lance sans compte Microsoft (jeton `0`, UUID « OfflinePlayer », pseudo de 3 à 16 lettres, chiffres ou `_`, pas de connexion directe au serveur) : le serveur du pack, en mode online, refuse ce joueur. Active aussi le lancement natif sans Client ID |
| `CW_GAME_ROOT` | autre dossier pour bibliothèques/assets/versions |
| `CW_JAVA_HOME` | Java imposé (essayé en premier) |
| `CW_THEME`, `CW_DATE`, `CW_LANG`, `CW_START_PAGE`, `CW_LAUNCHER_MANIFEST`, `CW_LAUNCHER_CONTENT` | thème, date simulée, langue, page de départ, manifeste et contenu locaux : voir les sections plus bas |

#### Repli CurseForge
Paramètres → Jeu → « Ouvrir CurseForge au lieu du lancement direct » (`UseCurseForgeLaunch`) : Jouer vérifie l'instance puis ouvre
`CurseForge.exe` (le joueur lance le pack depuis Mes modpacks). Sans Client ID Azure, la connexion affiche « pas encore disponible »
et propose d'ouvrir CurseForge. Un échec du lancement natif (Java, réseau, installateur) propose aussi d'ouvrir CurseForge.

#### Tests
Essais réels (sur une **copie** de l'instance, jamais l'instance du joueur) :

```bash
CW_REAL_LAUNCH="C:\...\copie-instance" CW_OFFLINE_TEST=Essai CW_GAME_ROOT="C:\...\gameroot" \
dotnet test launcher/tests/CreateWarfare.Launcher.Tests --filter Lance_le_jeu --logger "console;verbosity=detailed"
CW_REAL_INSTALL_FRESH="C:\...\dossier-vide" dotnet test launcher/tests/CreateWarfare.Launcher.Tests --filter Installation_complete
```

`RealNativeLaunchTrial.Lance_le_jeu_jusqu_a_l_ecran_titre` installe, lance le jeu, attend « Sound engine started » et l'atlas
`gui.png` dans `logs\latest.log` (8 min au plus), compte les mods trouvés, puis ferme le jeu. Sans `CW_REAL_LAUNCH` et
`CW_OFFLINE_TEST`, il ne fait rien (le test passe). Essai du 12/11 sur une copie de l'instance : écran titre atteint, 275 mods comme
avec CurseForge, mêmes erreurs de chargement.
`Installation_complete_sans_CurseForge` (variable `CW_REAL_INSTALL_FRESH` = dossier vide, Java 21 déjà présent sur le PC) télécharge tout
sans miroir, vérifie les trois fichiers produits par l'installateur NeoForge contre leurs empreintes figées, puis refait un
second passage qui ne doit rien télécharger ni relancer l'installateur (installation depuis zéro mesurée à 39 s, voir `docs/launcher/PLAN.md`). La connexion Microsoft réelle n'a **pas** été essayée (pas de Client ID) ; elle est
testée contre un faux serveur (`MicrosoftAuthTests`).

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

### Une version du launcher (installeur Velopack, à partir de la 2.0.0)

1. Incrémenter `<Version>` dans `src/CreateWarfare.Launcher/CreateWarfare.Launcher.csproj`.
2. Depuis `launcher/` (`dotnet tool restore` une fois : `vpk` est dans `launcher/dotnet-tools.json`) :
   ```
   dotnet publish src/CreateWarfare.Launcher -c Release -o out/app
   dotnet vpk download github --repoUrl https://github.com/Robocnop/create-warfare -o ../dist/launcher/velopack   # versions précédentes, pour les deltas (échoue tant qu'il n'y a pas de release Velopack : ignorer)
   dotnet vpk pack --packId CreateWarfare --packVersion <version> --packDir out/app --mainExe CreateWarfareLauncher.exe `
       --packTitle "Create Warfare" --packAuthors Robocnop --icon src/CreateWarfare.Launcher/Assets/icon.ico --outputDir ../dist/launcher/velopack
   ```
   `<version>` = celle du csproj. Le dossier contient `CreateWarfare-win-Setup.exe`, `CreateWarfare-<v>-full.nupkg`, `-delta.nupkg`,
   `releases.win.json`, `RELEASES`, `assets.win.json`, `CreateWarfare-win-Portable.zip`.
3. `python tools/publish_launcher.py --upload` : crée la release `launcher-v<version>` avec le Setup, le zip portable, les fichiers du
   flux Velopack et les nupkg de cette version, et met le SHA-256 du **Setup** dans `version.json` (champ `launcher`). Sans
   `dist/launcher/velopack/`, l'ancien comportement reste : `dist/launcher/CreateWarfareLauncher.exe` (build 1.x).
   - Launcher installé : `version.json` sert seulement à afficher « vX disponible » ; la mise à jour passe par `InstalledUpdater`
     (`GithubSource` sur le dépôt : lit `releases.win.json` de chaque release `launcher-v*`, télécharge les deltas, `Update.exe` applique et relance).
   - Launcher 1.x portable : `SelfUpdater` télécharge l'URL du champ `launcher` (le Setup), vérifie le SHA-256, remplace l'exe et le lance :
     le Setup installe le launcher 2.x (migration). Garder ce canal tant qu'il reste des 1.x.
   - Essai local sans GitHub : `CW_VELOPACK_SOURCE=<dossier de vpk pack>` et `CW_LAUNCHER_MANIFEST=<version.json local>`.
   - La CI (`launcher.yml`, job « installeur ») refait `vpk pack` à chaque changement pour vérifier la chaîne, sans rien publier.
   Les launchers installés se mettent à jour au clic. Le launcher ne lit `version.json` qu'à l'ouverture, et `raw.githubusercontent.com` le garde en cache jusqu'à 5 min :
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
