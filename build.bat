@echo off
cd /d "%~dp0"
dotnet build KMux.sln -c Debug > build_log.txt 2>&1
echo Exit code: %ERRORLEVEL% >> build_log.txt
