@echo off
cd /d "%~dp0"
java @user_jvm_args.txt @libraries/net/neoforged/neoforge/21.1.264/win_args.txt nogui
pause
