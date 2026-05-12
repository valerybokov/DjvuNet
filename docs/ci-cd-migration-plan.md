# CI/CD Stabilization and Migration Plan

## Background & Motivation
The recent migration to .NET 10 and xUnit v3 has broken the CI/CD pipelines (AppVeyor and a planned secondary CI provider) due to a combination of outdated pre-compiled build tools (`Tools.zip`), missing custom Unix dependencies (`System.Drawing.Common`), and legacy native build scripts for `libdjvulibre`. To restore and modernize the pipelines, we will retain AppVeyor and replace Travis CI with **GitHub Actions**. This move is forced by Travis CI's decision to drop free support for OSS software, whereas GitHub Actions offers unlimited free minutes for OSS public repositories and natively supports the required Visual Studio 2026 / .NET 10 environments.

## Scope & Impact
This plan covers:
1. Fixing the MSBuild custom tasks circular dependency and updating the `Tools` artifacts.
2. Integrating custom `System.Drawing.Common` (6.0.21) Unix dependencies.
3. Replacing the legacy manual native compilation with `vcpkg.json` for managing native binaries.
4. Packaging all new artifacts, tools, and test data into a finalized `v0.9.26130.3` release on the `DjvuNet/artifacts` repository.
5. Updating the build scripts and authoring new GitHub Actions workflows to consume the new release.

## Proposed Solution & Implementation Steps

### Phase 1: Tooling and Circular Dependency Fix
*   **Step 1: [COMPLETED]** Build the `DjvuNet.Git.Tasks` project locally from a fresh state to generate the correct `net10.0` DLLs.
*   **Step 2: [COMPLETED]** Zip the newly generated DjvuNet custom tools (specifically the `Tools/DjvuNet` directory) into a clean `Tools.zip` archive containing the necessary cross-platform native runtimes.

### Phase 2: Dependencies Integration (`System.Drawing.Common` and `vcpkg`)
We must stabilize all dependencies before cutting the artifacts release.
*   **Step 1:** Properly integrate the custom-built `System.Drawing.Common` (6.0.21) binaries for Unix support. Determine if they will be packaged into a new `deps.zip` or managed locally.
*   **Step 2:** Create a `vcpkg.json` file in the root of the repository to explicitly define the native dependencies required by DjVuLibre (e.g., `zlib`, `libjpeg`, `tiff`, etc.).

### Phase 3: Finalize Artifacts Release
The test data in the local `DjvuNet/artifacts` repo has been updated. We must now bundle the tools and dependencies to create the release.
*   **Step 1:** Ensure all test data, the new `Tools.zip`, and any required dependency packages (e.g., a new `deps.zip`) are committed/ready in the artifacts repository.
*   **Step 2:** Create a point release tag `v0.9.26130.3` on the `DjvuNet/artifacts` repository and push it. This allows GitHub to automatically generate the immutable Source Code Zip for the tests.

### Phase 4: CI Configuration and Build Script Updates
Now that the release tag exists, we can point the CI environments to it.
*   **Step 1:** Modify `build.cmd` and `build.sh` to download and extract the automatically generated GitHub Source Code Zip for the test artifacts (`https://github.com/DjvuNet/artifacts/archive/refs/tags/v0.9.26130.3.zip`) instead of performing a `git clone`.
*   **Step 2:** Update `build.cmd` and `build.sh` to point to the new `Tools.zip` release URI (`v0.9.26130.3`).
*   **Step 3:** Retain the `deps.zip` download logic in `build.cmd` and `build.sh` as it is required to distribute the custom `System.Drawing.Common` binaries for Unix until we migrate to a different graphics backend. Update it to the new URI for the upcoming dynamically versioned release. Note: `CMAKE_TOOLCHAIN_FILE` integration for `vcpkg` is not applicable, as DjvuLibre builds via Autotools; `vcpkg` integration is managed manually via `CPPFLAGS`, `LDFLAGS`, and `PKG_CONFIG_PATH`.
*   **Step 4:** Modify `.appveyor.yml` to bootstrap `vcpkg` and author a new `.github/workflows/build.yml` file to implement GitHub Actions.

## Verification
*   Execute the modified `build.cmd` locally to verify that tools build cleanly without the circular dependency.
*   Verify that `vcpkg` successfully installs dependencies and that `libdjvulibre` links against them correctly.