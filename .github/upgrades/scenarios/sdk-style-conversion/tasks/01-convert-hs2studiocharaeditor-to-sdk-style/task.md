# 01-convert-hs2studiocharaeditor-to-sdk-style: Convert HS2StudioCharaEditor to SDK-style

**Project**: HS2StudioCharaEditor  
**Path**: E:\work\HS2StudioCharaEditor\HS2StudioCharaEditor.csproj  
**Target Framework**: net472 (no change)  
**Risk**: Medium

**What will change**:
- Project element: `<Project ToolsVersion="15.0" ...>` → `<Project Sdk="Microsoft.NET.Sdk">`
- Package management: `packages.config` → `PackageReference` items in `.csproj`
- Explicit file includes: Removed (replaced with SDK-style globbing patterns)
- Configuration-specific properties: Simplified to SDK patterns

**What must be preserved**:
- All NuGet package references (35+ packages)
- Custom MSBuild `.targets` imports from packages (15+)
- Local assembly references (HS2ABMX.dll, HS2_BoobSettings.dll, etc.)
- PostBuildEvent (currently empty, but structure is preserved)
- AllowUnsafeBlocks and other custom properties

**Validation**:
- ✓ Solution builds successfully
- ✓ No build errors or warnings
- ✓ packages.config file is removed
- ✓ All custom targets are still imported
- ✓ NuGet restore completes without errors
