# Plan: SDK-style Conversion

## Overview

Convert the single **HS2StudioCharaEditor** project from legacy .NET Framework 4.7.2 format to modern SDK-style format.

**No target framework changes** — The project will remain at .NET Framework 4.7.2 after conversion.

## Task Breakdown

### Task 1: Convert HS2StudioCharaEditor to SDK-style

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

## Execution Order

This is a single-project scenario with no dependencies, so execution is straightforward:

1. Start Task 1: Convert HS2StudioCharaEditor
2. Execute conversion using `convert_project_to_sdk_style` tool
3. Verify build
4. Complete Task 1

## Summary

- **Total Tasks**: 1
- **Estimated Complexity**: Medium
- **Estimated Duration**: 30-45 minutes
- **Risk**: Medium (complex package and target preservation)
