DjvuNet Library
===============

## CI status of master branch

| Windows | Linux | macOS |
| :---: | :---: | :---: |
| ![Image](https://ci.appveyor.com/api/projects/status/github/djvunet/djvunet?svg=true) | [![Build Status](https://travis-ci.org/DjvuNet/DjvuNet.svg?branch=dev)](https://travis-ci.org/DjvuNet/DjvuNet) | [![Build Status](https://travis-ci.org/DjvuNet/DjvuNet.svg?branch=dev)](https://travis-ci.org/DjvuNet/DjvuNet) |


## Introduction

DjvuNet is an open source library designed to process and create documents encoded with DjVu format. Library is written in C# for .NET platform with
no external dependencies. Library supports Djvu format specification version 3 up to the minor version 26 (v3.26).
The so called "Secure DjVu" format is not supported as this specification was never published. Project was started several years ago
by [Telavian](https://github.com/Telavian) and after remaining inactive for some time currently is continued at new
[GitHub DjvuNet](https://github.com/DjvuNet) repo location. *Code is not production ready and still early in development. You should expect bugs, incomplete features, and API breakage as we work to improve it.* There are known bugs but anyway it should work on large number of djvu files (obviously it's still only a subset of all DjVu files which can be found out in the wild). Therefore, use it at your own risk and do not blame us for any of your problems.

## Current Status

*DjvuNet library is not ready for production use and is still in early development.* There are several known bugs which need to be fixed and missing features which need to be implemented first
before library could be treated as production ready or fully functional. Furthermore, there are some bugs in image decoder that leave some of images distorted.

Library supports full .NET Framework 4.7.2 or newer on Windows and .NET Core 10.0.0 or newer on Windows, Linux, macOS.

Project undergoes several architectural and implementations changes, which are done in "dev" branch.

- DjVu file format parser was optimized and refactored what so far resulted in more than 10x speedup.

- Image data decoding and **encoding** with Interpolated Dubuc-Deslauriers-Lemire (DDL) (4, 4) Discrete Wavelet Transform is close
to be finished but still has couple bugs which need to be fixed.

- There was very limited optimization work done in this area with some 30 - 40% improvements in performance and identification of several next optimization targets.

- ZP arithmetic coder and BZZ encoding/decoding is fully implemented and reached binary compatibility with DjvuLibre. It still awaits final optimizations.

- JB decoding is implemented but not optimized, encoding is not implemented.

- Image segmentation for Mixed Raster Content done in DjvuLibre with ColorPalette histogram calculation will be entirely rewritten as there was significant progress in image segmentation algorithms in the last two decades.

- Support for some DjvuLibre masked image formats is not implemented yet.

- Test framework is systematically developed and is composed of unit and functional tests. It covers project in top down way and provides
around 85% code coverage using 2 586 test cases with implementation target being more than 90% code coverage.

- Performance tests are based on DjvuNetTest project with some additional benchmarks planned for implementation soon.


## DjVu Format Support Validation

Full library format handling validation is realized by using [DjVuLibre](http://djvu.sourceforge.net/) reference library implementation of DjVu format and supporting tools. Our github mirror of DjVuLibre is available here: [DjVuLibre for DjvuNet](https://github.com/DjvuNet/DjVuLibre).
.NET Bindings for majority of C API are available in DjvuNet.DjvuLibre project. It builds for
x86 and x64 targets only. Perhaps AnyCPU target will be available via NuGet packaging or alternatively via embedding of native binaries
in managed assembly - the issue is still open.

DjVuLibre was modified by creating libdjvulibre build integration with DjvuNet projects and modifying library by expanding some C APIs through
addition of memory management functions exports, implementation of Json formatted output from some dump functions and tools (djvudump),
and addition of functions bypassing s-expressions formatting used in text retrieval.

Modified library used for testing DjvuNet implementation of DjVu format is available here: [DjVuLibre for DjvuNet](https://github.com/DjvuNet/DjVuLibre).

Due to more restrictive licensing conditions of DjVuLibre .NET bindings project DjvuNet.DjvuLibre is double licensed under MIT and GPL v2 licenses.

## Building

### Windows for .NET Core 10.0 LTS

#### Prerequisites

- Visual Studio 2026 v18 with at least following workloads: .NET desktop development, desktop development with C++, .NET Core cross-platform development

- or alternatively to Visual Studio 2026 v18 one can use .NET Core SDK command line tools with any code editor and Visual Studio build tools (tested with Visual Studio Build Tools 2026 v18.4)

- Git

- Internet access for restoring dependencies

#### Building

* Building and testing is easily done with help of build.sh / build.cmd scripts which are located in root directory of DjvuNet repository. It accepts multiple configuration parameters and can be used to build and test all targets with one command line. It automatically clones and downloads all required repos and dependencies.*

Building from command line on Windows (tested on Windows 11 with Visual Studio 2026 v18.4 installed).

Open Visual Studio 2026 developer command prompt and clone repository
`````
git clone https://github.com/DjvuNet/DjvuNet.git
`````
Change directory to your repo
`````
cd djvunet
`````
Here one can run build.cmd script from command line (command accepts multiple configuration parameters)
`````
build -p x64 -c Release -t Rebuild -Test -f netcoreapp10.0 (execute build -h to see all available options)
`````
Available configurations:
`````
Debug, Release  (example: -c Debug),  default value Debug
`````
Available platforms:
DjvuNet.DjvuLibre and libdjvulibre are built only for x64 platforms - x86 is no longer tested and is not supported by CI builds, however, it should be possible to build locally.
`````
x64, x86, arm64, arm (example -p x64, default dotnet value AnyCPU is not supported for CI builds)
`````
Available targets:
`````
Clean, Build, Rebuild, Restore   (example -t Clean), default value Rebuild
`````

To build with Visual Studio open DjvuNet.sln file located in root directory of DjvuNet cloned
repository and build DjvuNet.csproj or entire solution.

#### Testing

Test data are stored in separate repository [artifacts](https://github.com/DjvuNet/artifacts).
Clone repository with git command (run it from DjvuNet repo root directory):
`````
git clone --depth 1 https://github.com/DjvuNet/artifacts.git
`````

Tests can be run by building and running tests from DjvuNet.Tests.dll and DjvuNet.Wavelet.Tests.dll
assemblies under Visual Studio from Test Explorer or using xUnit test runner from command line.

All tests should pass except for skipped.

Performance tests can be run with help of DjvuNetTest project.

### Windows for netcoreapp10.0 target

#### Prerequisites

Visual Studio 2026 v18 with the following workloads: .NET desktop development, desktop development with C++, .NET Core cross-platform development
VS 2026 versions can be installed side by side and preview version can be safely used side by side with RTM versions.

- .NET Core 10.0.203 SDK

- Git

- Internet access for restoring dependencies

#### Building

Building from command line on Windows (tested on Windows 11 with Visual Studio 2026 v18.4 installed).

Open Visual Studio 2026 developer command prompt and clone repository
`````
git clone https://github.com/DjvuNet/DjvuNet.git
`````
Change directory to your repo
`````
cd djvunet
`````

Clone DjVuLibre from DjvuNet GutHub (this library was modified to integrate it into DjvuNet project)
`````
git clone https://github.com/DjvuNet/DjVuLibre.git
`````
DjVuLibre repo is now located in DjVuLibre directory of your DjvuNet repo.

From command prompt in DjvuNet root directory run:
`````
 build -c {Configuration} -p {Platform} -f {Framework}
`````

#### Testing

Test setup for all targets is not streamlined and is a bit involved. To avoid any problems use build script as follows:
`````
build -c {Configuration} -p {Platform} -f {Framework} -Test
`````
It is possible to skip building and testing of libdjvulibre and DjvuNet.DjvuLibre libraries by passing -sn or -SkipNative
command line switches to build script:
`````
build -c {Configuration} -p {Platform} -f {Framework} -Test -sn
`````

### Linux for netcoreapp10.0 target

#### Prerequisites

Tested on Ubuntu 24.04 and 26.04, however, it should work on other distributions as well.

Install required tools and dependencies:

`````
sudo apt-get update
sudo apt-get install git zip unzip curl libgdiplus
`````

#### Building

Clone repository:
`````
git clone https://github.com/DjvuNet/DjvuNet.git
`````

Change directory to cloned DjvuNet repo:
`````
cd DjvuNet
`````

Use build script * build.sh * which will automatically install required .NET Core SDK version in repo for local use or install .NET Core 10.0.203 SDK manually. Provided .build.sh script will default to installed .NET Core SDK if version is compatible with required version for building defined global.json file and SDK tools are in PATH, otherwise, it will install required version locally in repo in Tools/dotnetcli directoryand use it for building. Build script accepts multiple configuration parameters and can be used to build and test all targets with one command line - run *./build.sh -h* to see all available options and defaults.

Building with script:
`````
./build.sh -t Rebuild -c Release -p x64 -f netcoreapp10.0
`````

#### Testing

The easiest way to run tests is to use build.sh script with -Test command line switch. Script will automatically download required test artifacts if not present, build the repository, and run tests:
`````
./build.sh -t Build -c Release -p x64 -f netcoreapp10.0 -Test
`````

If you want to build tests only and run them manually use -BuildTests switch:
`````
./build.sh -t Build -c Release -p x64 -f netcoreapp10.0 -BuildTests
`````

Required for tests data and files can be downloaded manually - use latest available release.
`````
curl -L -o artifacts.zip -s https://github.com/DjvuNet/artifacts/releases/download/{latest}/artifacts.zip
unzip -q artifacts.zip -d artifacts
`````

Build and run DjvuNet tests (commands are starting from repo root):
`````
cd DjvuNet.Tests
dotnet build -c Release
dotnet publish -c Release
dotnet publish/path/DjvuNet.Tests.dll

# Return to repo root
cd ..

cd DjvuNet.Wavelet.Tests
dotnet build -c Release
dotnet publish -c Release
dotnet publish/path/DjvuNet.Wavelet.Tests.dll
`````


### macOS for netcoreapp10.0 target

### Temporarily not supported by scripts and not tested after dependency updates, however, one can try to build manually following Linux instructions as there should be no significant differences in build and test process on macOS. We will try to fix scripts and test on macOS as soon as possible.

#### Prerequisites

Download and install the .NET Core SDK from [.NET Downloads](https://www.microsoft.com/net/download/core).

#### Building and Testing

Follow Linux instructions for Building and Testing

## Usage

`````c#
using DjvuNet;

using(DjvuDocument doc = new DjvuDocument())
{
    doc.Load("Document.djvu");
    if (doc.Pages.Length > 0)
    {
        var firstPage = doc.Pages[0];
        var lastPage = doc.Pages[doc.Pages.Length - 1];

        using(System.Drawing.Bitmap pageImage = firstPage.BuildPageImage())
            firstPage.Save("DocumentTestImage1.png", ImageFormat.Png);

        string firstPageText = firstPage.Text;
        string lastPageText = lastPage.Text;
    }
}
`````

`````c#
using DjvuNet;

using(DjvuDocument doc = new DjvuDocument("Mcguffey's_Primer.djvu"))
{
    var page = doc.Pages[0];
    using(System.Drawing.Bitmap pageImage = page.BuildPageImage())
    {
        pageImage.Save("TestImage1.png", ImageFormat.Png);
        string pageText = page.Text;
    }
}
`````

## Reporting Issues

In case of build, test or DjvuNet library usage problems open new issue in [GitHub DjvuNet repo](https://github.com/DjvuNet/DjvuNet/issues) providing
detailed information on error (logs, command line output, stack trace, minidump) and used system.

We will try to adress all problems quickly unless they depend on missing features or known bugs which will be implemented or fixed according to our roadmap.

## License

DjvuNet is licensed under [MIT license](https://opensource.org/licenses/mit-license.php).

DjvuNet.DjvuLibre is double licensed under [MIT license](https://opensource.org/licenses/mit-license.php) and [GPL v2](https://opensource.org/licenses/GPL-2.0) or later.

DjVuLibre used for format support validation is licensed under [GPL v2](https://opensource.org/licenses/GPL-2.0) or later.
