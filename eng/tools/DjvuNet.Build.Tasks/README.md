# DjvuNet Build Tasks

This project contains custom MSBuild tasks used by the DjvuNet build infrastructure.

## The Bootstrapping Paradox & "Buildability"

Because this project provides MSBuild tasks (such as version injection) that are used to compile the entire solution—*including this project itself*—it suffers from a classic bootstrapping paradox. 

This paradox is critical to consider for the "buildability" of a freshly cloned repository.

**The Loop:**
1. A developer freshly clones the repository. 
2. `DjvuNet.Build.Tasks.csproj` imports `DjvuNetBuild.targets`.
3. `DjvuNetBuild.targets` hooks into `BeforeTargets="CoreCompile"` to run the `InjectVersion` target for every project.
4. `<InjectVersion>` requires the compiled DLL of this project to exist in the `Tools/` folder.

To make a fresh clone buildable, the repository provides pre-compiled tools (via `Tools.zip` download or committed artifacts). 

**The Crash Scenario:**
If we abruptly rename the project and update the MSBuild targets to look for a new DLL name (e.g., `DjvuNet.Build.Tasks.dll`), a freshly cloned repository will fail to build. The downloaded/existing toolset only provides the old `DjvuNet.Git.Tasks.dll`. MSBuild will fail to load the required task assembly, and the build will crash before it even has a chance to compile the newly renamed project and generate the new DLL.

---

## The 3-Stage Migration Procedure

To safely cut over the identity of this build tool while guaranteeing that a fresh clone remains 100% buildable, we must use a controlled, three-stage sequence across multiple commits.

### Stage 1: Move Source, Maintain Old Assembly Identity
**Goal:** Physically relocate the source code but trick the build system into thinking nothing changed.
1. Rename the physical directories from `DjvuNet.Git.Tasks` to `DjvuNet.Build.Tasks` and update solution/script path references.
2. Inside `DjvuNet.Build.Tasks.csproj`, explicitly define `<AssemblyName>DjvuNet.Git.Tasks</AssemblyName>` and `<RootNamespace>DjvuNet.Git.Tasks</RootNamespace>`.
   * **Why exactly?** The existing `DjvuNetBuild.targets` file is still hardcoded to look for the old DLL name (`DjvuNet.Git.Tasks.dll`). By moving the source code but keeping the old assembly output name, the build system can use the previously compiled (cached or downloaded) version of the old DLL to bootstrap the compilation of the relocated project. It will then overwrite the old DLL with the newly compiled code, proving the new directory structure works.
3. Do not modify the `$(GitTasksAssembly)` property in `DjvuNetBuild.targets`.
**Result:** A fresh clone downloads the old toolset. MSBuild uses the old DLL to successfully compile the relocated project. It outputs the newly compiled DLL under the old name. The repo remains completely buildable.

### Stage 2: Dual-Targeting and New Output
**Goal:** Output the new assembly name, but allow MSBuild to fall back to the old toolset DLL to bootstrap the process.
1. Remove the `<AssemblyName>` override in the `.csproj`, allowing it to naturally compile into `DjvuNet.Build.Tasks.dll`.
2. Update `DjvuNetBuild.targets` to use a dynamic fallback:
   ```xml
   <!-- Prefer the new DLL if it exists -->
   <GitTasksAssembly Condition="Exists('$(MSBuildThisFileDirectory)Tools/DjvuNet/$(DotNetCoreFrameworkVersion)/DjvuNet.Build.Tasks.dll')">$(MSBuildThisFileDirectory)Tools/DjvuNet/$(DotNetCoreFrameworkVersion)/DjvuNet.Build.Tasks.dll</GitTasksAssembly>
   
   <!-- Fall back to the old DLL (provided by existing toolset) to bootstrap the build -->
   <GitTasksAssembly Condition="'$(GitTasksAssembly)' == ''">$(MSBuildThisFileDirectory)Tools/DjvuNet/$(DotNetCoreFrameworkVersion)/DjvuNet.Git.Tasks.dll</GitTasksAssembly>
   ```
**Result:** A fresh clone downloads the old toolset. MSBuild evaluates the condition, realizes the new DLL doesn't exist yet, and falls back to using the old DLL. It successfully compiles the project and publishes the *new* `DjvuNet.Build.Tasks.dll` to the `Tools/` folder. 
**Post-Action:** We must:
   1. Create a new `artifacts` repository release with a new tag and a new `Tools.zip` file containing the newly compiled, renamed assemblies.
   2. Modify all links in the build files (`build.cmd`, `build.sh`) that download tools to point to this new release tag, commit the changes, and publish them.

### Stage 3: Cleanup
**Goal:** Remove the bootstrapping crutches once the new `Tools.zip` is live.
1. Remove the fallback condition from `DjvuNetBuild.targets`, hardcoding it to strictly require `DjvuNet.Build.Tasks.dll`.
**Result:** A fresh clone downloads the *new* toolset (containing the new DLL). MSBuild loads it directly and compiles the project. Migration complete, and buildability is maintained throughout.