#!/usr/bin/env bash

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Uncommment set -x to trace execution of the script
# set -x

usage()
{
    echo ""
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo ""
    echo "  -c, -Configuration <config>    Build configuration (Debug, Release, Checked). Default: Debug."
    echo ""
    echo "  -p, -Platform <platform>       Build platform (x64, x86, arm, armel, arm64, anycpu). Default: x64."
    echo ""
    echo "  -t, -Target <target>           MSBuild target (Build, Rebuild, Clean, Pack). Default: Build."
    echo ""
    echo "  -f, -Framework <tfm>           Target framework (net10.0, netstandard2.1, net472). Default: net10.0."
    echo ""
    echo "  -DjvuNet, -BuildDjvuNet        Build the core DjvuNet managed projects. Default: True."
    echo ""
    echo "  -bt, -BuildTests               Build the test projects. Default: False."
    echo ""
    echo "  -rt, -RunTests                 Build and run the test projects. Default: False."
    echo ""
    echo "  -Test                          Alias for -RunTests. Build and run the test projects."
    echo ""
    echo "  -sn, -SkipNative               Skip cloning, building, and testing of native components"
    echo "                                 (libdjvulibre) and its managed wrapper (DjvuNet.DjvuLibre)."
    echo "                                 When omitted, native dependencies are processed (SkipNative=False)."
    echo ""
    echo "  -v, -Verbosity <level>         Verbosity (q[uiet], m[inimal], n[ormal], d[etailed], diag[nostic]). Default: normal."
    echo ""
    echo "  -proc, -Processors <count>     Number of build processes. Default: number of logical processors."
    echo ""
    echo "  -OS <os>                       Target OS (Windows_NT, Linux, OSX). Default: Linux."
    echo ""
    echo "  -h, --help                     Show this usage message."
    echo ""
}

git_clone_retry()
{
    local extra_args=$1
    local url=$2
    local dest=$3
    local max_attempts=3
    local timeout_sec=120
    local attempt=1
    local delay=5

    while [ $attempt -le $max_attempts ]; do
        echo "BUILD: git clone attempt $attempt of $max_attempts for $url..."
        if command -v timeout >/dev/null 2>&1; then
            timeout ${timeout_sec}s git clone $extra_args "$url" "$dest"
        else
            git clone $extra_args "$url" "$dest"
        fi
        
        if [ $? -eq 0 ]; then
            return 0
        fi
        
        echo "BUILD: git clone failed or timed out. Retrying in $delay seconds..."
        sleep $delay
        delay=$((delay * 2))
        attempt=$((attempt + 1))
    done
    
    echo "BUILD: Error: git clone failed after $max_attempts attempts."
    return 1
}

initHostDistroRid()
{
    __HostDistroRid=""
    if [ "$__HostOS" == "Linux" ]; then
        if [ -e /etc/os-release ]; then
            source /etc/os-release
            if [[ $ID == "alpine" ]]; then
                # remove the last version digit
                VERSION_ID=${VERSION_ID%.*}
            fi
            __HostDistroRid="$ID.$VERSION_ID-$__HostArch"
        elif [ -e /etc/redhat-release ]; then
            local redhatRelease=$(</etc/redhat-release)
            if [[ $redhatRelease == "CentOS release 6."* || $redhatRelease == "Red Hat Enterprise Linux Server release 6."* ]]; then
               __HostDistroRid="rhel.6-$__HostArch"
            fi
        fi
    fi
    if [ "$__HostOS" == "FreeBSD" ]; then
        __freebsd_version=`sysctl -n kern.osrelease | cut -f1 -d'.'`
        __HostDistroRid="freebsd.$__freebsd_version-$__HostArch"
    fi

    if [ "$__HostDistroRid" == "" ]; then
        echo "WARNING: Can not determine runtime id for current distro."
    fi
}

initTargetDistroRid()
{
    if [ "$__CrossBuild" == "1" ]; then
        if [ "$__BuildOS" == "Linux" ]; then
            if [ ! -e $ROOTFS_DIR/etc/os-release ]; then
                if [ -e $ROOTFS_DIR/android_platform ]; then
                    source $ROOTFS_DIR/android_platform
                    export __DistroRid="$RID"
                else
                    echo "WARNING: Can not determine runtime id for current distro."
                    export __DistroRid=""
                fi
            else
                source $ROOTFS_DIR/etc/os-release
                export __DistroRid="$ID.$VERSION_ID-$__BuildArch"
            fi
        fi
    else
        export __DistroRid="$__HostDistroRid"
    fi

    # Portable builds target the base RID
    if [ $__PortableBuild == 1 ]; then
        if [ "$__BuildOS" == "Linux" ]; then
            export __DistroRid="linux-$__BuildArch"
        elif [ "$__BuildOS" == "OSX" ]; then
            export __DistroRid="osx-$__BuildArch"
        elif [ "$__BuildOS" == "FreeBSD" ]; then
            export __DistroRid="freebsd-$__BuildArch"
        fi
    fi
}

setup_dirs()
{
    echo "Setting up directories for build"

    mkdir -p "$__RootBinDir"
    mkdir -p "$__BinDir"
    mkdir -p "$__LogsDir"
    mkdir -p "$__IntermediatesDir"

    if [ "$__CrossBuild" == "1" ]; then
        mkdir -p "$__CrossComponentBinDir"
        mkdir -p "$__CrossCompIntermediatesDir"
    fi
}

# Check the system to ensure the right prereqs are in place

check_prereqs()
{
    echo "Checking prerequisites..."

    success=1

    # Check if system dotnet exists AND satisfies global.json
    __LocalDotNet=1
    if ! hash dotnet 2>/dev/null; then
        __LocalDotNet=0
    else
        # dotnet --version respects global.json. It returns 0 if satisfied, 1 if not.
        if ! __DotnetVer=$(dotnet --version 2>/dev/null); then
            __LocalDotNet=0
        fi
    fi
if [ "$__LocalDotNet" == "0" ]; then
    echo "System dotnet is missing or does not satisfy global.json. Falling back to local tools..."
    # Let init-tools.sh determine the highly specific directory name or calculate it here
    __LocalDotNetDir="$__ProjectRoot/Tools/coreclr/dotnetcli/$__OSName/$__Libc/$__ArchName"
    bash "$__ProjectRoot/init-tools.sh" "$__LocalDotNetDir"
    if [ $? -ne 0 ]; then
        echo "Error initializing tools."
        exit 1
    fi
    export DOTNET_ROOT="$__LocalDotNetDir"
    export PATH="${__LocalDotNetDir}:$PATH"
    __DotnetVer=$(dotnet --version)
fi
export __DotnetCmd="dotnet"

echo "dotnet $__DotnetVer installed"
    __MSBuildVer=$(dotnet msbuild /nologo /version)
    if [[ -z "$__MSBuildVer" ]]; then
        echo "dotnet sdk not installed $__MSBuildVer"
        success=0
    else
        echo "msbuild $__MSBuildVer installed"
    fi

    # Check presence of git on the path
    hash git 2>/dev/null
    if ! [[ $? -eq 0 ]]; then
        echo "git not installed";
        success=0;
    else
        echo "git installed"
    fi

    # Check presence of unzip on the path
     hash unzip 2>/dev/null
     if ! [[ $? -eq 0 ]]; then
        echo >&2 "unzip not installed";
        success=0;
    else
        echo "unzip installed"
    fi

    # Check presence of libgdiplus
    ldconfig -p | grep libgdiplus >/dev/null
    if [[ $? -ne 0 ]]; then
        echo "libgdiplus not installed";
        success=0;
    else
        echo "libgdiplus installed"
    fi

    # Minimum required version of clang is version 3.9 for arm/armel cross build
    if [[ $__CrossBuild == 1 && ("$__BuildArch" == "arm" || "$__BuildArch" == "armel") ]]; then
        if ! [[ "$__ClangMajorVersion" -gt "3" || ( $__ClangMajorVersion == 3 && $__ClangMinorVersion == 9 ) ]]; then
            echo "Please install clang3.9 or newer for arm/armel cross build";
            success=0;
        fi
    fi

    # Check for clang
    hash clang-$__ClangMajorVersion.$__ClangMinorVersion 2>/dev/null
    if ! [[ $? -eq 0 ]]; then
        hash clang$__ClangMajorVersion$__ClangMinorVersion 2>/dev/null
        if ! [[ $? -eq 0 ]]; then
            hash clang 2>/dev/null
            if ! [[ $? -eq 0 ]]; then
                echo >&2 "Please install clang-$__ClangMajorVersion.$__ClangMinorVersion before running this script";
                success=0;
            fi
        fi
    fi

    if [ $success -eq 0 ]; then exit 1; fi
}

build_native()
{
    skipCondition=$1
    platformArch="$2"
    intermediatesForBuild="$3"
    extraCmakeArguments="$4"
    message="$5"

    if [ $skipCondition == 1 ]; then
        echo "Skipping $message build."
        return
    fi

    # All set to commence the build
    echo "Commencing build of $message for $__BuildOS.$__BuildArch.$__BuildType in $intermediatesForBuild"

    generator=""
    buildFile="Makefile"
    buildTool="make"
    if [ $__UseNinja == 1 ]; then
        generator="ninja"
        buildFile="build.ninja"
        if ! buildTool=$(command -v ninja || command -v ninja-build); then
           echo "Unable to locate ninja!" 1>&2
           exit 1
        fi
    fi

    if [ "$__SkipConfigure" == "0" ]; then
        # if msbuild is not supported, then set __SkipGenerateVersion to 1
        if [ $__isMSBuildOnNETCoreSupported == 0 ]; then __SkipGenerateVersion=1; fi
        # Drop version.cpp file
        __versionSourceFile="$intermediatesForBuild/version.cpp"
        if [ $__SkipGenerateVersion == 0 ]; then
            pwd
            "$__ProjectRoot/run.sh" build -Project=$__ProjectDir/build.proj -generateHeaderUnix -NativeVersionSourceFile=$__versionSourceFile $__RunArgs $__UnprocessedBuildArgs
        else
            # Generate the dummy version.cpp, but only if it didn't exist to make sure we don't trigger unnecessary rebuild
            __versionSourceLine="static char sccsid[] __attribute__((used)) = \"@(#)No version information produced\";"
            if [ -e $__versionSourceFile ]; then
                read existingVersionSourceLine < $__versionSourceFile
            fi
            if [ "$__versionSourceLine" != "$existingVersionSourceLine" ]; then
                echo $__versionSourceLine > $__versionSourceFile
            fi
        fi


        pushd "$intermediatesForBuild"
        # Regenerate the CMake solution
        echo "Invoking \"$__ProjectRoot/src/pal/tools/gen-buildsys-clang.sh\" \"$__ProjectRoot\" $__ClangMajorVersion $__ClangMinorVersion $platformArch $__BuildType $__CodeCoverage $__IncludeTests $generator $extraCmakeArguments $__cmakeargs"
        "$__ProjectRoot/src/pal/tools/gen-buildsys-clang.sh" "$__ProjectRoot" $__ClangMajorVersion $__ClangMinorVersion $platformArch $__BuildType $__CodeCoverage $__IncludeTests $generator "$extraCmakeArguments" "$__cmakeargs"
        popd
    fi

    if [ ! -f "$intermediatesForBuild/$buildFile" ]; then
        echo "Failed to generate $message build project!"
        exit 1
    fi

    # Build
    if [ $__ConfigureOnly == 1 ]; then
        echo "Finish configuration & skipping $message build."
        return
    fi

    # Check that the makefiles were created.
    pushd "$intermediatesForBuild"

    echo "Executing $buildTool install -j $__NumProc"

    $buildTool install -j $__NumProc
    if [ $? != 0 ]; then
        echo "Failed to build $message."
        exit 1
    fi

    popd
}

isMSBuildOnNETCoreSupported()
{
    __isMSBuildOnNETCoreSupported=$__msbuildonunsupportedplatform

    if [ $__isMSBuildOnNETCoreSupported == 1 ]; then
        return
    fi

    if [ "$__HostArch" == "x64" ]; then
        if [ "$__HostOS" == "Linux" ]; then
            __isMSBuildOnNETCoreSupported=1
            # note: the RIDs below can use globbing patterns
            UNSUPPORTED_RIDS=("debian.9-x64" "ubuntu.17.04-x64")
            for UNSUPPORTED_RID in "${UNSUPPORTED_RIDS[@]}"
            do
                if [[ $__HostDistroRid == $UNSUPPORTED_RID ]]; then
                    __isMSBuildOnNETCoreSupported=0
                    break
                fi
            done
        elif [ "$__HostOS" == "OSX" ]; then
            __isMSBuildOnNETCoreSupported=1
        fi
    fi
}


build_CoreLib_ni()
{
    if [ $__SkipCrossgen == 1 ]; then
        echo "Skipping generating native image"
        return
    fi

    if [ $__SkipCoreCLR == 0 -a -e $__BinDir/crossgen ]; then
        echo "Generating native image for System.Private.CoreLib."
        echo "$__BinDir/crossgen /Platform_Assemblies_Paths $__BinDir/IL $__IbcTuning /out $__BinDir/System.Private.CoreLib.dll $__BinDir/IL/System.Private.CoreLib.dll"
        $__BinDir/crossgen /Platform_Assemblies_Paths $__BinDir/IL $__IbcTuning /out $__BinDir/System.Private.CoreLib.dll $__BinDir/IL/System.Private.CoreLib.dll
        if [ $? -ne 0 ]; then
            echo "Failed to generate native image for System.Private.CoreLib."
            exit 1
        fi

        if [ "$__BuildOS" == "Linux" ]; then
            echo "Generating symbol file for System.Private.CoreLib."
            $__BinDir/crossgen /CreatePerfMap $__BinDir $__BinDir/System.Private.CoreLib.dll
            if [ $? -ne 0 ]; then
                echo "Failed to generate symbol file for System.Private.CoreLib."
                exit 1
            fi
        fi
    fi
}

build_CoreLib()
{

    if [ $__isMSBuildOnNETCoreSupported == 0 ]; then
        echo "System.Private.CoreLib.dll build unsupported."
        return
    fi

    if [ $__SkipMSCorLib == 1 ]; then
       echo "Skipping building System.Private.CoreLib."
       return
    fi

    echo "Commencing build of managed components for $__BuildOS.$__BuildArch.$__BuildType"

    # Invoke MSBuild
    __ExtraBuildArgs=""
    if [[ "$__IbcTuning" -eq "" ]]; then
        __ExtraBuildArgs="$__ExtraBuildArgs -OptimizationDataDir=\"$__PackagesDir/optimization.$__BuildOS-$__BuildArch.IBC.CoreCLR/$__IbcOptDataVersion/data/\""
        __ExtraBuildArgs="$__ExtraBuildArgs -EnableProfileGuidedOptimization=true"
    fi
    $__ProjectRoot/run.sh build -Project=$__ProjectDir/build.proj -MsBuildLog="/flp:Verbosity=normal;LogFile=$__LogsDir/System.Private.CoreLib_$__BuildOS__$__BuildArch__$__BuildType.log" -BuildTarget -__IntermediatesDir=$__IntermediatesDir -__RootBinDir=$__RootBinDir -BuildNugetPackage=false -UseSharedCompilation=false $__RunArgs $__ExtraBuildArgs $__UnprocessedBuildArgs

    if [ $? -ne 0 ]; then
        echo "Failed to build managed components."
        exit 1
    fi

    # The cross build generates a crossgen with the target architecture.
    if [ $__CrossBuild != 1 ]; then
       # The architecture of host pc must be same architecture with target.
       if [[ ( "$__HostArch" == "$__BuildArch" ) ]]; then
           build_CoreLib_ni
       elif [[ ( "$__HostArch" == "x64" ) && ( "$__BuildArch" == "x86" ) ]]; then
           build_CoreLib_ni
       elif [[ ( "$__HostArch" == "arm64" ) && ( "$__BuildArch" == "arm" ) ]]; then
           build_CoreLib_ni
       else
           exit 1
       fi
    fi
}

generate_NugetPackages()
{
    # We can only generate nuget package if we also support building mscorlib as part of this build.
    if [ $__isMSBuildOnNETCoreSupported == 0 ]; then
        echo "Nuget package generation unsupported."
        return
    fi

    # Since we can build mscorlib for this OS, did we build the native components as well?
    if [ $__SkipCoreCLR == 1 ]; then
        echo "Unable to generate nuget packages since native components were not built."
        return
    fi

    echo "Generating nuget packages for "$__BuildOS
    echo "DistroRid is "$__DistroRid
    echo "ROOTFS_DIR is "$ROOTFS_DIR
    # Build the packages
    $__ProjectRoot/run.sh build -Project=$__SourceDir/.nuget/packages.builds -MsBuildLog="/flp:Verbosity=normal;LogFile=$__LogsDir/Nuget_$__BuildOS__$__BuildArch__$__BuildType.log" -BuildTarget -__IntermediatesDir=$__IntermediatesDir -__RootBinDir=$__RootBinDir -BuildNugetPackage=false -UseSharedCompilation=false $__RunArgs $__UnprocessedBuildArgs

    if [ $? -ne 0 ]; then
        echo "Failed to generate Nuget packages."
        exit 1
    fi
}

echo ; echo "BUILD: Starting Build of DjvuNet at $(date +"%Y-%m-%d %H:%M:%S.%2N")"; echo "";

# Argument types supported by this script:
#
# Build platform        - valid values are: x64, x86, arm, armel, arm64.
# Build configuration   - valid values are: Debug, Release
#
# Set the default arguments for build

# Obtain the location of the bash script to figure out where the root of the repo is.
__ProjectRoot="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

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

__BuildOS=$__OSName
__HostOS=$__OSName
__BuildArch=$__ArchName
__HostArch=$__ArchName

# Get the number of processors available to the scheduler
if [ `uname` = "FreeBSD" ]; then
  __NumProc=`sysctl hw.ncpu | awk '{ print $2+1 }'`
elif [ `uname` = "NetBSD" ]; then
  __NumProc=$(($(getconf NPROCESSORS_ONLN)+1))
elif [ `uname` = "Darwin" ]; then
  __NumProc=$(($(getconf _NPROCESSORS_ONLN)+1))
else
  __NumProc=$(nproc --all)
fi

# Set default values
_MSB_Target="Build"
_MSB_Configuration="Debug"
_MSB_Platform="x64"
_Verbosity="normal"
_Processors=$__NumProc
_OS="Linux"
_SkipNative=""
_BuildDjvuNet="1"
_BuildTests=""
_RunTests=""
_Test=""
_Pack=""
_DefaultNetCoreApp="net10.0"
_NetCoreAppId=".NETCoreApp"
_NetCoreAppTFM=".NETCoreApp,Version=v10.0"
_DefaultNetStandard="netstandard2.1"
_NetStandardId=".NETStandard"
_NetStandardTFM=".NETStandard,Version=v2.1"
_Framework="$_DefaultNetCoreApp"

# Parse command line
while [[ $# -gt 0 ]]; do
    key="$1"
    case $key in
        -Configuration|-c)
        _MSB_Configuration="$2"; shift 2 ;;
        -Platform|-p)
        _MSB_Platform="$2"; shift 2 ;;
        -Target|-t)
        _MSB_Target="$2"; shift 2 ;;
        -BuildDjvuNet|-DjvuNet)
        _BuildDjvuNet=1; shift 1 ;;
        -BuildTests|-bt)
        _BuildTests=1; shift 1 ;;
        -RunTests|-rt)
        _RunTests=1; shift 1 ;;
        -Test)
        _Test=1; shift 1 ;;
        -Framework|-f)
        _Framework="$2"; shift 2 ;;
        -SkipNative|-sn)
        _SkipNative=1; shift 1 ;;
        -Verbosity|-v)
        _Verbosity="$2"; shift 2 ;;
        -Processors|-proc)
        _Processors="$2"; shift 2 ;;
        -OS)
        _OS="$2"; shift 2 ;;
        -h|--help)
        usage
        exit 0 ;;
        *)
        echo "Unknown command line parameter: $1"; usage; exit 1 ;;
    esac
done

# check_params
_MSB_ConfigurationLower=$(echo "$_MSB_Configuration" | tr '[:upper:]' '[:lower:]')
if [[ "$_MSB_ConfigurationLower" == "debug" ]]; then
    _MSB_Configuration="Debug"
elif [[ "$_MSB_ConfigurationLower" == "release" ]]; then
    _MSB_Configuration="Release"
else
    echo "Invalid command line parameter -c/-Configuration: $_MSB_Configuration"; usage; exit 1
fi

_MSB_Platform=$(echo "$_MSB_Platform" | tr '[:upper:]' '[:lower:]')

if [[ "$_MSB_Platform" == "arm" || "$_MSB_Platform" == "arm64" || "$_MSB_Platform" == "armel" ]]; then
    __ManagedPlatform="AnyCPU"
    if [[ "$__HostArch" == "x64" ]]; then __SkipNativeTests=1; fi
elif [[ "$_MSB_Platform" == "anycpu" ]]; then
    _MSB_Platform="x64"
    __ManagedPlatform="AnyCPU"
elif [[ "$_MSB_Platform" == "x64" ]]; then
    _MSB_Platform="x64"
    __ManagedPlatform="x64"
elif [[ "$_MSB_Platform" == "x86" ]]; then
    _MSB_Platform="x86"
    __ManagedPlatform="x86"
else
    echo "Invalid command line parameter -p/-Platform: $_MSB_Platform"; usage; exit 1
fi

if [[ -z "$__ManagedPlatform" ]]; then __ManagedPlatform="$_MSB_Platform"; fi
_MSB_TargetLower=$(echo "$_MSB_Target" | tr '[:upper:]' '[:lower:]')
if [[ "$_MSB_TargetLower" == "clean" ]]; then __SkipPublish=1; fi

# Accepted Framework values
_FrameworkLower=$(echo "$_Framework" | tr '[:upper:]' '[:lower:]')
_DefaultNetCoreAppLower=$(echo "$_DefaultNetCoreApp" | tr '[:upper:]' '[:lower:]')
_DefaultNetStandardLower=$(echo "$_DefaultNetStandard" | tr '[:upper:]' '[:lower:]')
if [[ "$_FrameworkLower" == "netcoreapp" || "$_FrameworkLower" == "$_DefaultNetCoreAppLower" ]]; then
    _Framework="$_DefaultNetCoreApp"
    __TargetFrameworkMoniker="$_NetCoreAppTFM"
elif [[ "$_FrameworkLower" == "netstandard" || "$_FrameworkLower" == "$_DefaultNetStandardLower" ]]; then
    _Framework="$_DefaultNetStandard"
    TargetFrameworkIdentifier=".NETStandard"
    __TargetFrameworkMoniker="$_NetStandardTFM"
else
    echo "Invalid command line parameter -f/-Framework: $_Framework"; usage; exit 1
fi

if [[ -n "$_Test" ]]; then _BuildDjvuNet=1; _BuildTests=1; _RunTests=1; fi

if [[ -z "$_BuildDjvuNet" ]]; then
    if [[ -n "$_BuildTests" ]]; then _BuildDjvuNet=1; fi
fi

# Set default clang version
if [[ $__ClangMajorVersion == 0 && $__ClangMinorVersion == 0 ]]; then
    __ClangMajorVersion=3
    __ClangMinorVersion=9
fi


# init the host distro name
initHostDistroRid

# Set the remaining variables based upon the determined build configuration
__isMSBuildOnNETCoreSupported=0

# Init if MSBuild for .NET Core is supported for this platform
isMSBuildOnNETCoreSupported

# CI_SPECIFIC - On CI machines, $HOME may not be set. In such a case, create a subfolder and set the variable to set.
# This is needed by CLI to function.
if [ -z "$HOME" ]; then
    if [ ! -d "$__ProjectDir/temp_home" ]; then
        mkdir temp_home
    fi
    export HOME=$__ProjectDir/temp_home
    echo "HOME not defined; setting it to $HOME"
fi

# Configure environment if we are doing a cross compile.
if [ "$__CrossBuild" == "1" ]; then
    export CROSSCOMPILE=1
    if ! [[ -n "$ROOTFS_DIR" ]]; then
        export ROOTFS_DIR="$__ProjectRoot/cross/rootfs/$__BuildArch"
    fi
fi

# Check prerequisites
check_prereqs

__RootBuildDir="${__ProjectRoot}/build/bin/"
__RuntimeIdentifier="linux-${_MSB_Platform}"

__SystemAttrProj="System.Attributes/System.Attributes.csproj"
__DjvuNetGitTasksProj="build/tools/DjvuNet.Git.Tasks/DjvuNet.Git.Tasks.csproj"
__DjvuNetProj="DjvuNet/DjvuNet.csproj"
__DjvuNetDjvuLibreProj="DjvuNet.DjvuLibre/DjvuNet.DjvuLibre.csproj"

__OutputDir="${__RootBuildDir}${_OS}.${__ManagedPlatform}.${_MSB_Configuration}/binaries/${_Framework}/"
__PublishDir="${__OutputDir}${__RuntimeIdentifier}/publish/"
__LogsDir="${__RootBuildDir}${_OS}.${_MSB_Platform}.${_MSB_Configuration}/logs/"

echo "BUILD: __OutputDir [${__OutputDir}]"
echo "BUILD: __PublishDir [${__PublishDir}]"

__BuildCommandArgs=("-p:Configuration=${_MSB_Configuration}" "-p:Platform=${__ManagedPlatform}" "-p:TargetFramework=${_Framework}" "-p:RuntimeIdentifier=${__RuntimeIdentifier}" "-p:PublishDir=${__PublishDir}" "-v:${_Verbosity}" "-m:${_Processors}" "-nologo" "-nr:false")
__RestoreCmdArgs=("${__BuildCommandArgs[@]}")

mkdir -p "$__LogsDir"

restore_dotnet_proj() {
    local __DjvuTargetProject=$1
    echo "BUILD: Restoring $__DjvuTargetProject"
    echo "BUILD: Calling: $__DotnetCmd msbuild /t:Restore ${__RestoreCmdArgs[@]} $__DjvuTargetProject"
    "$__DotnetCmd" msbuild /t:Restore "${__RestoreCmdArgs[@]}" "$__DjvuTargetProject"
    if [[ $? -ne 0 ]]; then
        echo "BUILD: Error: nuget restore of $__DjvuTargetProject returned error"
        exit 1
    else
        echo "BUILD: Success: nuget restore of $__DjvuTargetProject finished"
    fi
}

build_dotnet_proj() {
    local __BuildProj=$1
    local __BuildProjName=$2

    local __BuildLogRootName="${__BuildProjName}.${_MSB_Target}"
    local __BuildLog="${__LogsDir}${__BuildLogRootName}.log"
    local __BuildWrn="${__LogsDir}${__BuildLogRootName}.wrn"
    local __BuildErr="${__LogsDir}${__BuildLogRootName}.err"
    local __MsbuildLogging=("/flp:Verbosity=diag;LogFile=${__BuildLog}" "/flp1:WarningsOnly;LogFile=${__BuildWrn}" "/flp2:ErrorsOnly;LogFile=${__BuildErr}")

    echo ""
    echo "BUILD: Building $__BuildProj"
    echo "BUILD: calling $__DotnetCmd msbuild ${__BuildCommandArgs[@]} -t:${_MSB_Target} ${__MsbuildLogging[@]} ${__ProjectRoot}/${__BuildProj}"
    "$__DotnetCmd" msbuild "${__BuildCommandArgs[@]}" -t:${_MSB_Target} "${__MsbuildLogging[@]}" "${__ProjectRoot}/${__BuildProj}"
    if [[ $? -ne 0 ]]; then
        echo "BUILD: Error: $__BuildProj build failed. Refer to the build log files for details:"
        echo "    $__BuildLog"
        echo "    $__BuildWrn"
        echo "    $__BuildErr"
        exit 1
    fi

    if [ -z "$__SkipPublish" ]; then
        local __PublishLogRootName="${__BuildProjName}.Publish"
        local __PublishLog="${__LogsDir}${__PublishLogRootName}.log"
        local __PublishWrn="${__LogsDir}${__PublishLogRootName}.wrn"
        local __PublishErr="${__LogsDir}${__PublishLogRootName}.err"
        local __MsbuildPubLogging=("/flp:Verbosity=diag;LogFile=${__PublishLog}" "/flp1:WarningsOnly;LogFile=${__PublishWrn}" "/flp2:ErrorsOnly;LogFile=${__PublishErr}")

        echo ""
        echo "BUILD: Publishing $__BuildProj"
        echo "BUILD: calling $__DotnetCmd msbuild ${__BuildCommandArgs[@]} -t:Publish ${__MsbuildPubLogging[@]} ${__ProjectRoot}/${__BuildProj}"
        "$__DotnetCmd" msbuild "${__BuildCommandArgs[@]}" -t:Publish "${__MsbuildPubLogging[@]}" "${__ProjectRoot}/${__BuildProj}"
        if [[ $? -ne 0 ]]; then
            echo "BUILD: Error: $__BuildProj publish failed. Refer to the publish log files for details:"
            echo "    $__PublishLog"
            echo "    $__PublishWrn"
            echo "    $__PublishErr"
            exit 1
        fi
    fi
}

if [ -n "$_BuildDjvuNet" ]; then
    # Build core projects
    restore_dotnet_proj "$__DjvuNetGitTasksProj"
    restore_dotnet_proj "$__SystemAttrProj"
    restore_dotnet_proj "$__DjvuNetProj"
    if [ -z "$_SkipNative" ]; then restore_dotnet_proj "$__DjvuNetDjvuLibreProj"; fi

    build_dotnet_proj "$__DjvuNetGitTasksProj" "DjvuNet.Git.Tasks.csproj"
    build_dotnet_proj "$__SystemAttrProj" "System.Attributes.csproj"
    build_dotnet_proj "$__DjvuNetProj" "DjvuNet.csproj"
    if [ -z "$_SkipNative" ]; then build_dotnet_proj "$__DjvuNetDjvuLibreProj" "DjvuNet.DjvuLibre.csproj"; fi
fi

if [ -n "$_BuildTests" ]; then
    # Clone test data
    if [ ! -f "./artifacts/test001C.djvu" ]; then
        echo ""
        echo "BUILD: Cloning test data from https://github.com/DjvuNet/artifacts.git"
        git_clone_retry "--depth 1 -c core.autocrlf=false" "https://github.com/DjvuNet/artifacts.git" ""
        if [ $? -ne 0 ]; then echo "BUILD: Error: git clone returned error"; exit 1; fi
    fi

    # Build test projects
    __DjvuNetTestsProj="DjvuNet.Tests/DjvuNet.Tests.csproj"
    __DjvuNetWaveletTestsProj="DjvuNet.Wavelet.Tests/DjvuNet.Wavelet.Tests.csproj"
    __DjvuNetTestExeProj="DjvuNetTest/DjvuNetTest.csproj"
    __DjvuNetDjvuLibreTestsProj="DjvuNet.DjvuLibre.Tests/DjvuNet.DjvuLibre.Tests.csproj"

    restore_dotnet_proj "$__DjvuNetTestsProj"
    restore_dotnet_proj "$__DjvuNetWaveletTestsProj"
    restore_dotnet_proj "$__DjvuNetTestExeProj"
    if [ -z "$_SkipNative" ]; then restore_dotnet_proj "$__DjvuNetDjvuLibreTestsProj"; fi

    build_dotnet_proj "$__DjvuNetTestsProj" "DjvuNet.Tests.csproj"
    build_dotnet_proj "$__DjvuNetWaveletTestsProj" "DjvuNet.Wavelet.Tests.csproj"
    build_dotnet_proj "$__DjvuNetTestExeProj" "DjvuNetTest.csproj"
    if [ -z "$_SkipNative" ]; then build_dotnet_proj "$__DjvuNetDjvuLibreTestsProj" "DjvuNet.DjvuLibre.Tests.csproj"; fi
fi

if [ -n "$_RunTests" ]; then
    # Run tests
    __TestOutputDir="$__PublishDir"
    _DjvuNet_Tests="${__TestOutputDir}DjvuNet.Tests.dll"
    _DjvuNet_DjvuLibre_Tests="${__TestOutputDir}DjvuNet.DjvuLibre.Tests.dll"
    _DjvuNet_Wavelet_Tests="${__TestOutputDir}DjvuNet.Wavelet.Tests.dll"

    __TestResOutputDir="TestResults/${_Framework}/"
    mkdir -p "$__TestResOutputDir"

    if [[ "$_Framework" == "$_DefaultNetCoreApp" ]]; then
        _Test_Options="-trait- Category=skip-netcoreapp"
        __TestOutputFormat="xml"
    elif [[ "$_Framework" == "$_DefaultNetStandard" ]]; then
        _Test_Options="-trait- Category=skip-netcoreapp"
        __TestOutputFormat="xml"
    fi

    _VerbosityLower=$(echo "$_Verbosity" | tr '[:upper:]' '[:lower:]')
    if [[ "$_VerbosityLower" == "d" || "$_VerbosityLower" == "detailed" ]]; then 
        _Test_Options="$_Test_Options -verbose"
    fi
    if [[ "$_VerbosityLower" == "diag" || "$_VerbosityLower" == "diagnostic" ]]; then 
        _Test_Options="$_Test_Options -verbose -internaldiagnostics"
    fi
    _Test_Options="$_Test_Options -trait- Category=Skip -nologo -nocolor"

    _DjvuNet_Tests_Error="false"

    echo ""
    echo "BUILD: Running tests from DjvuNet.Tests assembly"
    echo "BUILD: calling: \"$__DotnetCmd\" \"$_DjvuNet_Tests\" $_Test_Options -${__TestOutputFormat} \"${__TestResOutputDir}DjvuNet.Tests.${__TestOutputFormat}\""
    "$__DotnetCmd" "$_DjvuNet_Tests" $_Test_Options -${__TestOutputFormat} "${__TestResOutputDir}DjvuNet.Tests.${__TestOutputFormat}" || _DjvuNet_Tests_Error="true"

    if [ -z "$_SkipNative" ] && [ -z "$__SkipNativeTests" ]; then
        echo ""
        echo "BUILD: Running tests from DjvuNet.DjvuLibre.Tests assembly"
        echo "BUILD: calling: \"$__DotnetCmd\" \"$_DjvuNet_DjvuLibre_Tests\" $_Test_Options -${__TestOutputFormat} \"${__TestResOutputDir}DjvuNet.DjvuLibre.Tests.${__TestOutputFormat}\""
        "$__DotnetCmd" "$_DjvuNet_DjvuLibre_Tests" $_Test_Options -${__TestOutputFormat} "${__TestResOutputDir}DjvuNet.DjvuLibre.Tests.${__TestOutputFormat}" || _DjvuNet_Tests_Error="true"
    fi

    echo ""
    echo "BUILD: Running tests from DjvuNet.Wavelet.Tests assembly"
    echo "BUILD: calling: \"$__DotnetCmd\" \"$_DjvuNet_Wavelet_Tests\" $_Test_Options -${__TestOutputFormat} \"${__TestResOutputDir}DjvuNet.Wavelet.Tests.${__TestOutputFormat}\""
    "$__DotnetCmd" "$_DjvuNet_Wavelet_Tests" $_Test_Options -${__TestOutputFormat} "${__TestResOutputDir}DjvuNet.Wavelet.Tests.${__TestOutputFormat}" || _DjvuNet_Tests_Error="true"

    if [ "$_DjvuNet_Tests_Error" == "true" ]; then
        echo ""
        echo "BUILD: Error: tests failed"
        echo ""
        echo "BUILD: Build Failed at $(date +"%Y-%m-%d %H:%M:%S.%2N")"
        exit 1
    else
        echo ""
        echo "BUILD: Success: tests passed"
        echo ""
        echo "BUILD: Finished Build at $(date +"%Y-%m-%d %H:%M:%S.%2N")"
        exit 0
    fi
fi

echo ""
echo "BUILD: Success: successfully built."
echo ""
echo "BUILD: Finished Build at $(date +"%Y-%m-%d %H:%M:%S.%2N")"
exit 0
