#!/bin/sh
# Create Warfare - installation du serveur (une fois, puis a chaque mise a jour du pack)
# Prerequis : Java 21 et Python 3.11+.
set -e
cd "$(dirname "$0")"
if [ ! -d libraries/net/neoforged/neoforge/21.1.264 ]; then
  curl -L -o neoforge-installer.jar https://maven.neoforged.net/releases/net/neoforged/neoforge/21.1.264/neoforge-21.1.264-installer.jar
  java -jar neoforge-installer.jar --installServer
  rm neoforge-installer.jar
fi
python3 -I pack/tools/fetch_mods.py server mods
cp -r pack/config/. config/
echo "Installation terminee. Accepte l'EULA Minecraft dans eula.txt puis lance ./start.sh"
