# DjvuNet Build Output Architecture

This document defines the formal, strict output routing architecture for the DjvuNet repository across all stages of the build lifecycle.

## Design Background (The "Why")
The output routing is structured to solve several complex cross-platform build problems:
1. **Side-by-Side Framework Targeting:** Managed code is compiled against multiple Target Frameworks (TFMs) simultaneously (e.g., `net10.0`, `netstandard2.1`).
2. **Native Compilation Cost:** Native C++ compilation (`libdjvulibre`) is extremely slow and does not depend on the .NET framework. By placing the raw native binaries at a higher level or in a shared drop, we compile them exactly once per architecture (`x64`, `arm64`) and share them across all TFM folders, preventing redundant C++ compilations.
3. **Two-Step Publish & Dependency Resolution:** Native dependencies for `libgit2sharp` and `libdjvulibre` are resolved via Runtime Identifiers (RIDs). During the `Build` phase, they sit alongside the managed DLLs for quick compilation checks. During the `Publish` phase, they are strictly isolated into `win-x64`, `linux-x64` folders to prevent architecture collisions.
4. **Tool Isolation:** Custom build tools (`DjvuNet.Git.Tasks`) execute *during* the MSBuild pipeline. They must be physically isolated in a `tools/` directory so their dependency locks don't pollute the main product output, preventing file-in-use crashes during concurrent compilation.

---

## Global Path Variables
*   `$(TargetOS)` = Target OS (e.g., `Windows_NT`, `Linux`, `OSX`)
*   `$(TargetPlatform)` = Native Platform (e.g., `x64`, `arm64`)
*   `$(Configuration)` = Build Configuration (e.g., `Debug`, `Release`)
*   `$(TargetFramework)` = Managed Target Framework (e.g., `net10.0`)
*   `$(RuntimeIdentifier)` = Specific Runtime ID (e.g., `win-x64`, `linux-x64`)
*   `$(VCToolsVersion)` = Active C++ Compiler Toolchain Version (e.g., `14.51.36231`)

---

## 1. Restore & 2. Intermediate (obj)
**Goal:** Complete isolation of generated assets (`project.assets.json`, `.g.props`) and intermediate `.obj` files from the source tree.

*   **Path:** `build/bin/obj/$(MSBuildProjectName)/`
*   **Mechanism:** Controlled centrally via `<BaseIntermediateOutputPath>`, `<MSBuildProjectExtensionsPath>` and `<RestoreOutputPath>` in `Directory.Build.props`.

## 3. Build (Current Local Dev Layout)
**Goal:** Compile managed assemblies for a specific framework.

*   **Standard Managed Binaries:**
    `build/bin/$(TargetOS).$(TargetPlatform).$(Configuration)/binaries/$(TargetFramework)/`
*   **Tools Managed Binaries:**
    `build/bin/tools/$(TargetOS).$(TargetPlatform).$(Configuration)/binaries/$(TargetFramework)/`
*   **Native Library Build Drop:**
    The C++ toolchain builds locally. MSBuild targets (`DjvuNet.DjvuLibre.targets`) seamlessly copy the resulting `libdjvulibre` binaries directly into **both** the Standard Managed Binaries folder and the Tools Managed Binaries folder (as needed) to ensure basic compilation checks succeed without requiring a full publish step.

## 4. Publish (Two-Step Process)
**Goal:** Assemble the final, fully resolved artifact layouts with all transitive native dependencies mapped correctly according to their Runtime Identifiers (RID).

*   **Standard Publish (Self-Contained / Execution Ready):**
    `build/bin/$(TargetOS).$(TargetPlatform).$(Configuration)/binaries/$(TargetFramework)/$(RuntimeIdentifier)/publish/`
*   **Tools Publish (Execution Ready):**
    `Tools/DjvuNet/$(TargetFramework)/`
    *(Pushed dynamically by the `DeployTasksAssemblyToTools` target via a nested MSBuild publish task that correctly resolves the `LibGit2Sharp` native dependency graph).*

## 5. Testing
**Goal:** Execute the tests against the `Publish` outputs exclusively. Testing against the raw build directory is forbidden, as only the publish directory accurately represents the final resolved dependency tree and native RID layouts.

*   **Test Working Directory:**
    `build/bin/$(TargetOS).$(TargetPlatform).$(Configuration)/binaries/$(TargetFramework)/$(RuntimeIdentifier)/publish/`
*   **Test Results (XML/HTML reports):**
    `TestResults/$(TargetFramework)/`

## 6. Final Native / Managed Layout (Distribution & Packaging)
**Goal:** Generate distributable `.nupkg` files representing the logical isolation of managed code from native assets, adhering to the .NET ecosystem RID fallback standard.

*   **Unified Package Output Directory:**
    `build/bin/$(TargetOS).$(TargetPlatform).$(Configuration)/packages/`

*   **Final Distribution Architecture:**
    1.  **`DjvuNet`:** Pure managed library (MSIL). Zero native dependencies.
        *   `lib/net10.0/DjvuNet.dll`
    2.  **`DjvuNet.DjvuLibre.NativeBinaries`:** Pure native package mimicking the LG2S layout. Contains C++ assets mapped strictly by RID.
        *   `runtimes/win-x64/native/libdjvulibre.dll`
        *   `runtimes/linux-x64/native/libdjvulibre.so`
    3.  **`DjvuNet.DjvuLibre`:** Managed wrapper that strictly depends on the NativeBinaries package.
        *   `lib/net10.0/DjvuNet.DjvuLibre.dll`
        *(At runtime, a custom `DllImportResolver` uses OS/Arch detection to construct the exact path pointing into the `runtimes/` folder of the cached NuGet package).*

## 7. Byproducts & Telemetry (Logs)
**Goal:** Isolate MSBuild logs to prevent concurrent build steps from overwriting files, causing file lock crashes.

*   **Managed Logs (Isolated by Framework):**
    `build/bin/$(TargetOS).$(TargetPlatform).$(Configuration)/logs/$(TargetFramework)/`
*   **Native Logs (Isolated by Compiler Toolchain):**
    `build/bin/$(TargetOS).$(TargetPlatform).$(Configuration)/logs/native/msvc-$(VCToolsVersion)/` (or Clang/Intel equivalently).