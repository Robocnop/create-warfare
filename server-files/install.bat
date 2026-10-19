@echo off
rem Create Warfare - installation du serveur (a lancer une seule fois, puis a chaque mise a jour du pack)
rem Prerequis : Java 21 et Python 3.11+ dans le PATH.
cd /d "%~dp0"
if not exist libraries\net\neoforged\neoforge\21.1.264 (
  curl -L -o neoforge-installer.jar https://maven.neoforged.net/releases/net/neoforged/neoforge/21.1.264/neoforge-21.1.264-installer.jar || goto :err
  java -jar neoforge-installer.jar --installServer || goto :err
  del neoforge-installer.jar
)
python -I pack\tools\fetch_mods.py server mods || goto :err
xcopy /E /Y /I pack\config config >nul
echo.
echo Installation terminee. Accepte l'EULA Minecraft dans eula.txt puis lance start.bat
pause
exit /b 0
:err
echo ERREUR pendant l'installation.
pause
exit /b 1
