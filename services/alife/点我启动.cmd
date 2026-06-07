@echo off
setlocal
set PYTHONIOENCODING=utf-8
set PYTHONUTF8=1

:: Auto Request Administrator Privileges (Silent fallback)
net session >nul 2>&1 || (powershell -Command "Start-Process -FilePath '%~dpnx0' -Verb RunAs" & exit)

cd /d "%~dp0"
title Alife Launcher [Private Runtime Mode - Silent]

:: Keep large model/package caches inside the project runtime instead of C:\Users\...
set "ALIFE_CACHE_DIR=%~dp0Runtime\Cache"
set "PRIVATE_DOTNET_ROOT=%~dp0.dotnet"
set "MODELSCOPE_CACHE=%ALIFE_CACHE_DIR%\modelscope\hub"
set "HF_HOME=%ALIFE_CACHE_DIR%\huggingface"
set "HUGGINGFACE_HUB_CACHE=%HF_HOME%\hub"
set "TRANSFORMERS_CACHE=%HF_HOME%\transformers"
set "PIP_CACHE_DIR=%ALIFE_CACHE_DIR%\pip"
set "NUGET_PACKAGES=%ALIFE_CACHE_DIR%\nuget\packages"
set "NUGET_HTTP_CACHE_PATH=%ALIFE_CACHE_DIR%\nuget\local\v3-cache"
set "DOTNET_CLI_HOME=%ALIFE_CACHE_DIR%\dotnet"
if exist "%PRIVATE_DOTNET_ROOT%\dotnet.exe" (
    set "DOTNET_ROOT=%PRIVATE_DOTNET_ROOT%"
    set "PATH=%PRIVATE_DOTNET_ROOT%;%PATH%"
)
if not exist "%ALIFE_CACHE_DIR%" mkdir "%ALIFE_CACHE_DIR%"

echo ===================================================
echo [Alife] System Initialization and Silent Setup
echo ===================================================
echo.

:: 1. Check Visual C++ & .NET Desktop Runtime
echo [Alife] [Step 1/4] Checking System Dependencies...

:: === [Check VC++] ===
powershell -Command "if(Test-Path \"$env:SystemRoot\System32\vcruntime140.dll\"){exit 0}else{exit 1}" >nul 2>&1
if not errorlevel 1 goto :VC_READY

echo [Alife] Missing Dependency: Visual C++. Starting auto-installation...
echo [Alife] Downloading Visual C++...
powershell -Command "Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile '%TEMP%\vc_setup.exe'"
echo [Alife] Installing Visual C++ (Silent Mode)...
start /wait "" "%TEMP%\vc_setup.exe" /install /quiet /norestart
if exist "%TEMP%\vc_setup.exe" del "%TEMP%\vc_setup.exe"
echo [Alife] Visual C++ installed successfully.

:VC_READY
echo [Alife] Visual C++ Runtime is ready.

:: === [Check .NET] ===
:: 使用纯 PowerShell 检测，彻底抛弃容易导致崩溃的 cmd 管道符 (|)
powershell -Command "if (Get-Command dotnet -ErrorAction SilentlyContinue) { $r = dotnet --list-runtimes; if ($r -match 'Microsoft.WindowsDesktop.App 9\.') { exit 0 } } exit 1" >nul 2>&1
if not errorlevel 1 goto :DOTNET_READY

echo [Alife] Missing Dependency: .NET 9 Desktop. Starting auto-installation...
echo [Alife] Downloading .NET 9 Desktop...
powershell -Command "Invoke-WebRequest -Uri 'https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe' -OutFile '%TEMP%\dotnet_setup.exe'"
echo [Alife] Installing .NET 9 Desktop (Silent Mode)...
start /wait "" "%TEMP%\dotnet_setup.exe" /quiet /norestart
if exist "%TEMP%\dotnet_setup.exe" del "%TEMP%\dotnet_setup.exe"
echo [Alife] .NET 9 Desktop installed successfully.

:DOTNET_READY
echo [Alife] .NET Desktop Runtime is ready.
echo.

:: 2. Setup Forced Private Python Environment
echo [Alife] [Step 2/4] Verifying Private Python Environment...
set "PY_DIR=%~dp0Runtime\Python312"

if exist "%PY_DIR%\python.exe" goto :PYTHON_READY

echo [Alife] Python environment not found. Initiating silent download...
if not exist "%PY_DIR%" mkdir "%PY_DIR%"

echo [Alife] Downloading Python 3.12.10...
powershell -Command "Invoke-WebRequest -Uri 'https://repo.huaweicloud.com/python/3.12.10/python-3.12.10-embed-amd64.zip' -OutFile '%TEMP%\py.zip'"

echo [Alife] Extracting Python to Private Runtime...
powershell -Command "Expand-Archive -Path '%TEMP%\py.zip' -DestinationPath '%PY_DIR%' -Force"
if exist "%TEMP%\py.zip" del "%TEMP%\py.zip"

:: Fix .pth file (enable site-packages)
echo [Alife] Configuring site-packages mapping (.pth)...
powershell -Command "$p = Join-Path '%PY_DIR%' 'python312._pth'; (Get-Content $p) | ForEach-Object { $_ -replace '#import site', 'import site' } | Set-Content $p"

:: Install Pip
echo [Alife] Downloading and installing Pip silently...
powershell -Command "Invoke-WebRequest -Uri 'https://bootstrap.pypa.io/get-pip.py' -OutFile '%TEMP%\get-pip.py'"
"%PY_DIR%\python.exe" "%TEMP%\get-pip.py" --no-warn-script-location --index-url https://mirrors.aliyun.com/pypi/simple/ >nul 2>&1
if exist "%TEMP%\get-pip.py" del "%TEMP%\get-pip.py"

echo [Alife] Python environment setup complete.

:PYTHON_READY
echo [Alife] Private Python environment is ready.
echo.

:: 3. PATH Injection & Setup
echo [Alife] [Step 3/4] Injecting Variables and Updating Packages...
set "PATH=%PY_DIR%;%PY_DIR%\Scripts;%PATH%"
set "PIP_INDEX_URL=https://mirrors.aliyun.com/pypi/simple/"

echo [Alife] Updating basic Python tools (pip, setuptools, wheel)...
python -m pip install --upgrade pip setuptools wheel >nul 2>&1
echo [Alife] Isolated environment injected successfully.
echo.

:: 4. Launch Application
echo [Alife] [Step 4/4] Launching Application...
if not exist "Outputs\Alife\Alife.dll" goto :NO_EXE

echo [Alife] Starting Alife.dll with private .NET runtime...
echo ===================================================
dotnet "Outputs\Alife\Alife.dll"
goto :END_APP

:NO_EXE
echo [Error] Outputs\Alife\Alife.dll not found!

:END_APP
echo.
echo [Alife] Application process ended.
pause
exit /b
