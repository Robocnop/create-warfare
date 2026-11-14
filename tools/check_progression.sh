#!/bin/bash
# Lance le serveur de test avec l'export des recettes, puis le vérificateur de progression.
cd "$(dirname "$0")/.." || exit 1
dump=server-test/kubejs/server_scripts/zz_cw_dump.js
# Repartir de scripts propres : test_server.sh recopie kubejs/ sans rien supprimer.
rm -rf server-test/kubejs/server_scripts server-test/kubejs/export
mkdir -p server-test/kubejs/server_scripts
cp tools/progression/zz_cw_dump.js "$dump"
bash tools/test_server.sh
rm -f "$dump"
if ! grep -q 'CW-DUMP terminé' server-test/logs/kubejs/server.log; then
  echo "Export absent : le serveur n'a pas atteint ServerEvents.loaded" >&2
  exit 1
fi
python -I tools/check_progression.py
