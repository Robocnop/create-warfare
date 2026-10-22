#!/bin/bash
# Sync server mods, boot the test server until "Done" or a failure, then stop it.
cd "$(dirname "$0")/.." || exit 1
python -I tools/fetch_mods.py server server-test/mods || exit 1
cp -r config/. server-test/config/
mkdir -p server-test/kubejs && cp -r kubejs/. server-test/kubejs/
cd server-test || exit 1
export PATH="/c/Program Files/Eclipse Adoptium/jdk-21.0.12.101-hotspot/bin:$PATH"
rm -f console.log
java @user_jvm_args.txt @libraries/net/neoforged/neoforge/21.1.264/win_args.txt nogui > console.log 2>&1 &
pid=$!
for i in $(seq 1 900); do
  grep -qE 'Done \(|Crash report saved|Missing or unsupported mandatory|Encountered an unexpected exception' console.log && break
  kill -0 $pid 2>/dev/null || break
  sleep 1
done
sleep 3
grep -nE 'Done \(|Crash report saved|Missing or unsupported mandatory|Mod ID:|Encountered an unexpected exception' console.log | head -30
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='java.exe'\" | ? { \$_.CommandLine -like '*win_args*' } | % { Stop-Process -Id \$_.ProcessId -Force }"
