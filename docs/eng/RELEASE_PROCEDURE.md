# DjvuNet Release Process

This document outlines the standard operating procedures for building and cutting a new release of the DjvuNet library and its associated components.

## 1. Versioning Mechanics
DjvuNet employs a hybrid versioning system. Because the public APIs have not yet stabilized via an official 1.0 release, the project does not strictly follow semantic versioning.

*   **Major and Minor Versions:** These are set **manually** via the `library.version` file located in the root directory (e.g., `0.9.0` or `0.10.0`).
*   **Patch and Build Versions:** These are calculated **automatically** via the custom MSBuild tasks during compilation (yielding versions like `v0.9.26139.1`).

### 1.1 Recommended Tagging Workflow
To ensure a green CI pipeline and correct build bootstrapping, releases **must** follow this strict sequence:

1.  **Tag Sub-repositories First:** Tag the `eng/tools/libgit2sharp` (4creators fork) and `djvulibre` repositories. 
    *   *Critical:* Tagging `libgit2sharp` first is mandatory to maintain synchronization so the build tools compilation can succeed later.
2.  **Release Artifacts:** Create the release for the `artifacts` repository and publish the required binary payloads (see Section 3).
3.  **Update Build Scripts:** In the main `DjvuNet` repository, update the build scripts (`build.cmd`, `build.sh`) to point to the newly created artifacts release tag. Commit this change.
4.  **Tag Root Repository:** Finally, release tag the root `DjvuNet` repository.

*Note on Version Alignment:* Because the root repository requires an additional commit (Step 3) to update the build scripts with the new tag, its automatically calculated patch/build value will be incremented by one compared to the other repositories (e.g., root is `v0.10.X.1` while sub-repositories are `v0.10.X.0`). This discrepancy is expected and valid.

## 2. Bootstrapping and the `-Tools` Build Flag
The repository contains custom MSBuild tasks (such as `DjvuNet.Build.Tasks`) that are required to compile the solution. This creates a bootstrapping paradox: the solution requires a compiled DLL of the build tasks to inject version numbers, but the build tasks themselves are part of the solution being compiled.

To resolve this and ensure a fresh repository clone is buildable:
1.  **Pre-compiled Artifacts:** The build scripts download a `Tools.zip`/`Tools.tar.gz` archive containing the pre-compiled MSBuild task DLLs from an existing artifacts release.
2.  **The `-Tools` Flag:** By default, the build scripts (`build.cmd`/`build.sh`) do **not** recompile the MSBuild tools project. The build tools are *only* compiled when the `-Tools` option is explicitly passed to the build scripts.
3.  **Tool Updates:** If modifications are made to the `DjvuNet.Build.Tasks` project, developers must follow the multi-stage bootstrapping procedure defined in `eng/tools/DjvuNet.Build.Tasks/README.md` to safely update the cached `Tools.zip` artifact without breaking the build pipeline for new clones. Even if no changes were made which required the bootstrapping procedure implementation, it is recommended to rebuild the tools for the next tagged release to ensure correct assembly version numbers are synchronized across release tags for binary released tools.

## 3. Release Artifacts
To ensure a fresh clone remains buildable and testable, specific binary payloads must be generated and attached to the GitHub Release on the `DjvuNet/artifacts` repository for every synchronized tag. The build scripts (`build.cmd`/`build.sh`) will attempt to download these archives directly from `https://github.com/DjvuNet/artifacts/releases/download/<Tag>/`.

The following artifacts must be present on the release tag:
1.  **`Tools.zip` / `Tools.tar.gz`:** Contains the compiled `DjvuNet.Build.Tasks.dll` with its dependencies needed to bootstrap the build (see Section 2).
2.  **`deps.zip` / `deps.tar.gz`:** Contains the custom `System.Drawing.Common` dependencies required for cross-platform image processing.

Additionally, the build scripts rely on downloading the source code tarballs from the synchronized tags of the sub-repositories (e.g., `djvulibre.tar.gz`, `libgit2sharp.tar.gz`, and `artifacts.tar.gz` for test files). Ensure these tags are cut and published to GitHub (Step 1 & 2) *before* running CI pipelines on the root repository.

## 4. Compilation Directives: `PROD_RELEASE`
The `PROD_RELEASE` preprocessor macro is a critical gatekeeper used to differentiate between testing builds and final production binaries.

### Usage in Code
When `PROD_RELEASE` is **defined**, the compiler generates the safest, most robust version of the library designed for the end-user.
When `PROD_RELEASE` is **not defined**, the library may engage in aggressive, fail-fast behaviors to validate architectural assumptions.

**Example: Hardware Acceleration (AVX2)**
In `DjvuNet.Wavelet.InterWaveTransform`, the static constructor contains:
```csharp
#if !PROD_RELEASE
    if (Avx2.IsSupported) return;
#endif
    EnsureLutsInitialized();
```
**Why this exists:** If AVX2 is supported in non-prod builds, we intentionally skip initializing the scalar Lookup Tables (LUTs). This acts as a strict, fail-fast mechanism. If an AVX2-accelerated method accidentally falls back to scalar logic due to a bug or edge-case boundary, the missing LUT will immediately trigger a `NullReferenceException`, alerting the developer to the unoptimized path. In `PROD_RELEASE` builds, the LUTs are always initialized defensively to guarantee execution stability, even if an unexpected fallback occurs.

## 5. Pre-Commit Review
All releases must pass the Tri-Scope Code Review (Code Quality, Performance, Security) prior to commit.
