@echo off
setlocal enabledelayedexpansion

:: Target directory set to ../Outputs (on your Desktop) to keep it separate from project folder
set "DIST_DIR=..\¹²ÏíÎÄ¼þ\Alife\Outputs"
echo [System] Starting Unified Publish Workflow...

:: 1. Cleanup target directory (removes old pollution)
if exist "%DIST_DIR%" (
    echo [Info] Cleaning target %DIST_DIR% directory...
    rd /s /q "%DIST_DIR%"
)
mkdir "%DIST_DIR%"

:: 2. Publish Main Application
echo [1/2] Publishing Main Application (Alife)...
dotnet publish "Sources\Alife\Alife.csproj" -c Release -o "%DIST_DIR%\Alife" --self-contained false /p:PublishSelfContained=false

:: 3. Publish DeskPet module
echo [2/2] Publishing DeskPet Module...
dotnet publish "Sources\Alife.Function\Alife.Function.DeskPet\Alife.Function.DeskPet.csproj" -c Release -o "%DIST_DIR%\Alife.Function.DeskPet" --self-contained false /p:PublishSelfContained=false

:: 5. Final pollution check and cleanup (Safety Net for Alife)
echo [Clean] Running final runtime pollution check...
pushd "%DIST_DIR%\Alife"
del /f /q hostfxr.dll 2>nul
del /f /q hostpolicy.dll 2>nul
del /f /q coreclr.dll 2>nul
del /f /q clrjit.dll 2>nul
del /f /q createdump.exe 2>nul
popd

echo.
echo ======================================================
echo [Success] Unified Release is ready in: %CD%\%DIST_DIR%
echo [Success] Package is clean and supports .NET 9/10/11.
echo ======================================================
pause
