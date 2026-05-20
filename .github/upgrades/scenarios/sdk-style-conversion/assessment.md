# Assessment: SDK-style Conversion

## Projects to Convert

| Project | Path | packages.config | Custom Imports | Special Type | Risk |
|---------|------|----------------|----------------|-------------|------|
| HS2StudioCharaEditor | E:\work\HS2StudioCharaEditor\HS2StudioCharaEditor.csproj | Yes | Multiple custom targets | Class library | Medium |

## Already SDK-style (no action needed)
- None

## Project Analysis: HS2StudioCharaEditor

### Format Indicators
- **No Sdk attribute**: ✗ Legacy format (`<Project ToolsVersion="15.0">`)
- **Explicit file includes**: ✓ Yes — `<Compile Include="...">` for every source file
- **packages.config**: ✓ Present — 35+ NuGet packages
- **Custom imports**: ✓ Multiple — 15+ `<Import>` statements for package-specific targets
- **Standard imports**: ✓ Present — `Microsoft.CSharp.targets`
- **Custom build events**: ✓ Yes — PostBuildEvent defined

### Complexity Patterns Found

**1. NuGet Package Management**
- Using `packages.config` (legacy; needs migration to `PackageReference`)
- 35+ package references with specific version pinning
- Multiple custom `.targets` file imports from NuGet packages (required for proper build behavior)

**2. Explicit File Includes**
- All source files (`<Compile>` items) explicitly listed
- Embedded resources explicitly listed (`<EmbeddedResource>`)
- SDK-style would use glob patterns (implicit)

**3. Custom MSBuild Targets**
- 15+ conditional `<Import>` statements for package-specific `.targets` files
- `EnsureNuGetPackageBuildImports` target for validation
- Package target paths are all relative to the `packages` directory
- All must be preserved in the SDK-style conversion

**4. Assembly Configuration**
- Configuration-specific `<PropertyGroup>` blocks for Debug/Release
- `AllowUnsafeBlocks` set to true (C# feature requirement)
- Standard assembly metadata in `Properties\AssemblyInfo.cs`

**5. References**
- GAC references: System, System.Core, System.Data, Microsoft.CSharp, etc.
- Path-based references to local DLLs: `HS2ABMX.dll`, `HS2_BoobSettings.dll`, etc.
- NuGet package references via `packages\...` HintPath

### Risks & Considerations

**Risk Level**: **Medium**

1. **Package-specific targets must be preserved**
   - The project relies on 15+ custom `.targets` imports from NuGet packages
   - SDK-style doesn't automatically import these from `packages.config`
   - Solution: After converting to `PackageReference`, ensure all imported `.targets` files are still accessible

2. **Custom build event**
   - Empty `<PostBuildEvent>` is currently defined
   - Should be preserved during conversion (though empty, it may be used later)

3. **Local assembly references**
   - Local DLLs (`libdll\HS2ABMX.dll`, etc.) are referenced by path
   - These will need to be resolved post-conversion (may need to be added as project references or copied appropriately)

4. **Multiple explicit build configurations**
   - Debug and Release are explicitly configured
   - SDK-style uses a different approach (most settings can be removed)

## Baseline

- Solution builds: **Yes** (with current legacy format)
- Warning count: Unknown (needs build)

## Key Findings

1. **Straightforward single-project conversion** — Only one project, no complex inter-project dependencies
2. **High file count** — Explicit file listing will be replaced with SDK-style globbing, significantly reducing the .csproj file size
3. **NuGet package counts** — Converting from `packages.config` to `PackageReference` is well-supported by modern tooling
4. **Preservation required** — All custom `.targets` imports and local reference paths must be carefully preserved
5. **Build verification critical** — After conversion, test that:
   - All NuGet packages resolve correctly
   - Package-specific targets still import successfully
   - Local assembly references are still resolvable
   - The assembly builds without errors or warnings

## Conversion Strategy

1. **Use the built-in conversion tool** (`convert_project_to_sdk_style`) to perform the conversion
2. **Verify the .csproj output** to ensure all custom targets and references are preserved
3. **Restore NuGet packages** and ensure all `PackageReference` entries are created
4. **Build and validate** to confirm no regressions
