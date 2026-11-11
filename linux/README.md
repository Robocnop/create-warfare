# Create Warfare sous Linux

Un seul fichier à télécharger. Il crée l'instance « Create Warfare » dans **Prism Launcher**, y installe le pack,
puis le met à jour tout seul avant chaque partie. Rien n'est installé sur le système : tout reste dans ton dossier personnel.

## Installer

1. Installe Prism Launcher et ouvre-le une fois pour connecter ton compte Microsoft.
   Recommandé, toutes distributions et Steam Deck : `flatpak install flathub org.prismlauncher.PrismLauncher`.
   Autres méthodes : <https://prismlauncher.org/download/>.
2. Télécharge le fichier de la [dernière release du launcher](https://github.com/Robocnop/create-warfare/releases) :
   `create-warfare-linux-x86_64` (PC, Steam Deck) ou `create-warfare-linux-aarch64` (ARM 64 bits).
3. Lance-le :

   ```sh
   chmod +x create-warfare-linux-x86_64
   ./create-warfare-linux-x86_64
   ```

4. Ouvre Prism Launcher et lance « Create Warfare ». Au premier lancement, accepte le téléchargement de Java 21 si Prism le propose.

Le premier passage télécharge environ 700 Mo. Tu peux ensuite supprimer le fichier téléchargé : une copie est rangée dans l'instance.

## Commandes

| Commande | Effet |
| --- | --- |
| `./create-warfare-linux-x86_64` | crée l'instance, ou la met à jour si elle existe |
| `… --launch` | pareil, puis lance le jeu dans Prism |
| `… repair` | remet tous les fichiers du pack comme à l'origine (mondes, touches et réglages gardés) |
| `… status` | montre ce qui est installé, sans rien modifier |
| `… --memory 8192` | mémoire donnée au jeu, en Mo (sinon réglée une fois d'après la RAM du PC) |
| `… --prism-dir DOSSIER` | dossier de données de Prism, pour une installation portable |

`--help` donne la liste complète.

## Ce qui est gardé

Les mises à jour ne touchent jamais à tes mondes, captures, touches (`options.txt`), liste de serveurs, ni aux mods et
packs de ressources que tu as ajoutés. Une config du pack que tu as modifiée est gardée tant que le pack ne la change pas.
Chaque mise à jour fait une sauvegarde et revient en arrière si elle échoue. Sans connexion, le jeu se lance avec la version déjà installée.

## Dossiers

- Instance : `<données de Prism>/instances/CreateWarfare/` (le jeu est dans `minecraft/`).
  Données de Prism : `~/.var/app/org.prismlauncher.PrismLauncher/data/PrismLauncher` (Flatpak) ou `~/.local/share/PrismLauncher`.
- Téléchargements en cache : `~/.cache/create-warfare/`.
- Mise à jour automatique : c'est la « commande de pré-lancement » de l'instance (Prism → Modifier → Paramètres → Commandes personnalisées).
  Si tu l'as changée, `repair` la remet.

## Désinstaller

Supprime l'instance dans Prism (clic droit → Supprimer), puis `rm -r ~/.cache/create-warfare`.

## Compiler

```sh
dotnet publish launcher/src/CreateWarfare.Linux -c Release -r linux-x64   -o dist/linux/x86_64
dotnet publish launcher/src/CreateWarfare.Linux -c Release -r linux-arm64 -o dist/linux/aarch64
```

Le code est dans `launcher/src/CreateWarfare.Linux` et partage le cœur du launcher Windows (`CreateWarfare.Launcher.Core`).
