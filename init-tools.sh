#!/usr/bin/env bash

__ProjectRoot="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
__GlobalJson="$__ProjectRoot/global.json"

# Detect OS Family based on TestPlatforms matrix
__UnameOS=$(uname -s | tr '[:upper:]' '[:lower:]')
case $__UnameOS in
    linux)   
        # Differentiate Android from standard Linux
        if [ -n "$ANDROID_STORAGE" ] || [ -d "/system/app" ]; then __OSName="android"; else __OSName="linux"; fi 
        ;;
    darwin)  
        # Differentiate iOS/tvOS/MacCatalyst from OSX natively if possible, default to osx
        if [ "$TARGET_OS" == "ios" ]; then __OSName="ios"; 
        elif [ "$TARGET_OS" == "tvos" ]; then __OSName="tvos";
        elif [ "$TARGET_OS" == "maccatalyst" ]; then __OSName="maccatalyst";
        else __OSName="osx"; fi
        ;;
    freebsd) __OSName="freebsd" ;;
    netbsd)  __OSName="netbsd" ;;
    openbsd) __OSName="openbsd" ;;
    sunos)   
        # Differentiate illumos from legacy Solaris
        if uname -v | grep -qi "illumos"; then __OSName="illumos"; else __OSName="solaris"; fi
        ;;
    haiku)   __OSName="haiku" ;;
    wasi)    __OSName="wasi" ;;
    *)       __OSName="$__UnameOS" ;;
esac

# Detect Libc Variant (Crucial for Linux permutations)
__Libc="gnu"
if [ "$__OSName" == "linux" ]; then
    if [ -f /etc/alpine-release ] || (command -v ldd >/dev/null && ldd --version 2>&1 | grep -q "musl"); then
        __Libc="musl"
    elif [ -n "$ANDROID_STORAGE" ] || (command -v ldd >/dev/null && ldd --version 2>&1 | grep -q "bionic"); then
        __Libc="bionic"
    fi
elif [[ "$__OSName" == "osx" || "$__OSName" == "ios" || "$__OSName" == "tvos" || "$__OSName" == "maccatalyst" ]]; then
    __Libc="darwin"
elif [[ "$__OSName" == *"bsd" ]]; then
    __Libc="bsd"
elif [[ "$__OSName" == "solaris" || "$__OSName" == "illumos" ]]; then
    __Libc="sun"
elif [ "$__OSName" == "android" ]; then
    __Libc="bionic"
fi

# Detect Architecture
__UnameArch=$(uname -m | tr '[:upper:]' '[:lower:]')
case $__UnameArch in
    x86_64|amd64)    __ArchName="x64" ;;
    aarch64|arm64)   __ArchName="arm64" ;;
    armv7l|armv8l|armhf) __ArchName="arm" ;;
    i386|i486|i586|i686) __ArchName="x86" ;;
    s390x)           __ArchName="s390x" ;;
    ppc64le)         __ArchName="ppc64le" ;;
    riscv64)         __ArchName="riscv64" ;;
    wasm32)          __ArchName="wasm" ;;
    *)               __ArchName="$__UnameArch" ;;
esac

# Allow override via $1
__DotNetDir="${1:-$__ProjectRoot/Tools/coreclr/dotnetcli/$__OSName/$__Libc/$__ArchName}"

echo "Initializing local .NET SDK based on DjvuNet global.json..."

mkdir -p "$__DotNetDir"

max_attempts=5
attempt=1
delay=10
success=false
base_timeout=120

while [ $attempt -le $max_attempts ]; do
    current_timeout=$((base_timeout * attempt))
    echo "Downloading dotnet-install.sh (Attempt $attempt of $max_attempts, Timeout: ${current_timeout}s)..."
    
    if command -v wget >/dev/null 2>&1; then
        download_cmd="wget -q --timeout=$current_timeout https://dot.net/v1/dotnet-install.sh -O $__DotNetDir/dotnet-install.sh"
    elif command -v curl >/dev/null 2>&1; then
        download_cmd="curl -sSL --max-time $current_timeout https://dot.net/v1/dotnet-install.sh -o $__DotNetDir/dotnet-install.sh"
    else
        echo "Error: Neither wget nor curl is available. Cannot download dotnet-install.sh."
        exit 1
    fi
    
    if $download_cmd; then
        success=true
        break
    else
        echo "Download failed or timed out. Retrying in $delay seconds..."
        sleep $delay
        delay=$((delay * 2))
        attempt=$((attempt + 1))
    fi
done

if [ "$success" = false ]; then
    echo "Error: Failed to download dotnet-install.sh after $max_attempts attempts."
    exit 1
fi
chmod +x "$__DotNetDir/dotnet-install.sh"

"$__DotNetDir/dotnet-install.sh" --channel "$__SdkChannel" --install-dir "$__DotNetDir"
rm "$__DotNetDir/dotnet-install.sh"

__ResolvedVersion=$("$__DotNetDir/dotnet" --version)
echo "Local .NET SDK v${__ResolvedVersion} initialized at $__DotNetDir"