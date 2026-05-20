# Conventional Commits Guide

This project uses **Conventional Commits** for automated versioning. Your commit messages determine version bumps!

## Commit Message Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

## Types & Version Impact

| Type | Description | Version Bump | Example |
|------|-------------|--------------|---------|
| **feat** | New feature | **MINOR** ↑ | `feat: add character editor` |
| **fix** | Bug fix | **PATCH** ↑ | `fix: correct alignment issue` |
| **BREAKING CHANGE** | Breaking API change | **MAJOR** ↑ | `feat!: remove legacy format support` |
| **perf** | Performance improvement | PATCH ↑ | `perf: optimize rendering` |
| **docs** | Documentation only | No release | `docs: update README` |
| **style** | Code style changes | No release | `style: format code` |
| **refactor** | Code refactor | No release | `refactor: restructure module` |
| **test** | Adding tests | No release | `test: add unit tests` |
| **chore** | Build/tooling changes | No release | `chore: update dependencies` |

## Examples

### Bug Fix (PATCH: 1.0.0 → 1.0.1)
```
fix: correct dropdown selection logic

The dropdown was not properly handling null values.
This fix ensures null values are handled gracefully.
```

### New Feature (MINOR: 1.0.0 → 1.1.0)
```
feat: add character import from file

Allows users to import character data from CSV files.
Supports both HS2 and KK formats.
```

### Breaking Change (MAJOR: 1.0.0 → 2.0.0)
```
feat!: redesign configuration format

BREAKING CHANGE: The old JSON config format is no longer supported.
Migrate to the new YAML format using the migration tool.
```

### With Scope
```
feat(ui): add dark mode support

Implements dark mode with automatic system theme detection.
Closes #42
```

## Local Testing

To see what version would be generated from your commits:

```powershell
# Check last tag
git describe --tags --abbrev=0

# See commits since last tag
git log <last-tag>..HEAD --oneline
```

## Tips

✅ **Use imperative mood**: "add feature" not "added feature"  
✅ **Lowercase first letter**: `fix: bug` not `fix: Bug`  
✅ **No period at end**: `fix: bug` not `fix: bug.`  
✅ **Be specific**: `fix: null reference in Editor` not `fix: bugs`  
✅ **Reference issues**: `Closes #42` or `Fixes #123`  

## Workflow

1. Make your changes
2. Commit with conventional format:
   ```powershell
   git commit -m "feat: add new UI component"
   ```
3. Push to master:
   ```powershell
   git push origin master
   ```
4. GitHub Actions automatically:
   - ✅ Detects version bump needed
   - ✅ Updates AssemblyVersion
   - ✅ Builds the project
   - ✅ Creates a tag (v1.2.3)
   - ✅ Creates a GitHub Release with files

## Disabling Auto-Release

If you want to build without auto-releasing, push to a branch other than `master` or `main`.

```powershell
git checkout -b feature/my-feature
git commit -m "feat: my feature"
git push origin feature/my-feature
# This will build but NOT create a release
```
