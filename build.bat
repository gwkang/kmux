@echo off
cd /d D:\github\kmux
dotnet build KMux.sln -c Debug > build_log.txt 2>&1
echo Exit code: %ERRORLEVEL% >> build_log.txt
