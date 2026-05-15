@if not defined _echo @echo off
setlocal EnableDelayedExpansion EnableExtensions

set "__MsgPrefix=BUILD: "

if /i [%PLATFORM%] == [] set "PLATFORM=%1"

set "INIT_TOOLS_LOG_DIR=%~dp0build"
if NOT exist "%INIT_TOOLS_LOG_DIR%" mkdir "%INIT_TOOLS_LOG_DIR%"
set "INIT_TOOLS_LOG=%INIT_TOOLS_LOG_DIR%\init-tools.log"
if [%PACKAGES_DIR%]==[] set "PACKAGES_DIR=%~dp0packages\"
if [%TOOLRUNTIME_DIR%]==[] set "TOOLRUNTIME_DIR=%~dp0Tools\coreclr\"
set "__GlobalJson=%~dp0global.json"

:: ARCHITECTURAL NOTE: The official .NET SDK payloads for Windows are strictly compiled
:: targeting the MSVC Application Binary Interface (ABI) and Universal C Runtime (UCRT).
:: As discussed in early coreclr/runtime GitHub issues, it is structurally impossible to 
:: build the core Windows .NET runtime with alternative compilers unless they perfectly 
:: emulate MSVC (e.g., clang-cl.exe masquerading as cl.exe).
:: Because the payload binary contract is immutable, we statically isolate it under 'msvc'.
if [%2]==[] (
    set "DOTNET_PATH=%TOOLRUNTIME_DIR%dotnetcli\"
) else (
    set "DOTNET_PATH=%~2\"
)

if [%3]==[] (
    set "__SdkChannel=10.0"
) else (
    set "__SdkChannel=%~3"
)

if [%DOTNET_CMD%]==[] set "DOTNET_CMD=%DOTNET_PATH%dotnet.exe"

:: if force option is specified then clean the tool runtime and build tools package directory to force it to get recreated
if /i [%1]==[force] (
  if exist "%TOOLRUNTIME_DIR%" rmdir /S /Q "%TOOLRUNTIME_DIR%"
)

echo %__MsgPrefix%Running %0 > "%INIT_TOOLS_LOG%"

if exist "%DOTNET_CMD%" goto :afterdotnetrestore

echo %__MsgPrefix%Installing dotnet cli via dotnet-install.ps1...
if NOT exist "%DOTNET_PATH%" mkdir "%DOTNET_PATH%"

set "__TmpInitScript=%~dp0init-tools_%RANDOM%.ps1"

(
    echo $ErrorActionPreference = 'Stop'
    echo $retryCount = 0
    echo $success = $false
    echo $maxAttempts = 5
    echo do {
    echo     try {
    echo         Write-Output "Downloading dotnet-install.ps1..."
    echo         Invoke-RestMethod -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile '%DOTNET_PATH%dotnet-install.ps1'
    echo         $success = $true
    echo     } catch {
    echo         if ^($retryCount -ge ^($maxAttempts - 1^)^) { throw }
    echo         $retryCount++
    echo         Write-Output "Download failed. Retrying... (Attempt $retryCount)"
    echo         Start-Sleep -Seconds ^(5 * $retryCount^)
    echo     }
    echo } while ^($success -eq $false^)
    echo Write-Output "Executing dotnet-install.ps1 with Channel '%__SdkChannel%' and InstallDir '%DOTNET_PATH%'"
    echo ^& '%DOTNET_PATH%dotnet-install.ps1' -Channel '%__SdkChannel%' -InstallDir '%DOTNET_PATH%' -NoPath
    echo Write-Output "dotnet-install.ps1 completed. Removing script."
    echo Remove-Item '%DOTNET_PATH%dotnet-install.ps1' -Force
) > "%__TmpInitScript%"

powershell -NoProfile -ExecutionPolicy ByPass -Command ". '%__TmpInitScript%' | Tee-Object -FilePath '%INIT_TOOLS_LOG%' -Append"
if exist "%__TmpInitScript%" del "%__TmpInitScript%"

if NOT exist "%DOTNET_CMD%" (
  echo %__MsgPrefix%ERROR: Could not install dotnet cli correctly. 1>&2
  goto :error
)

:afterdotnetrestore
exit /b 0

:error
echo %__MsgPrefix%Please check the detailed log that follows. 1>&2
type "%INIT_TOOLS_LOG%" 1>&2
exit /b 1
