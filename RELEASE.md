# Release Guide

## Quick Release

```bash
# Bump patch version (0.8.15 → 0.8.16) and release
./release.sh patch

# Bump minor version (0.8.15 → 0.9.0) and release
./release.sh minor

# Bump major version (0.8.15 → 1.0.0) and release
./release.sh major

# Release current version without bumping
./release.sh
```

## What It Does

The script automatically:
- ✅ Bumps version in `MatchZy.cs` (if specified)
- ✅ Cleans and builds the project
- ✅ Creates proper directory structure (`addons/` and `cfg/`)
- ✅ Creates `MatchZy-X.Y.Z.zip` ready for extraction
- ✅ Commits changes
- ✅ Creates and pushes Git tag
- ✅ Creates GitHub release
- ✅ Uploads the zip file

## Directory Structure

The release zip contains:
```
MatchZy-X.Y.Z/
├── addons/
│   └── counterstrikesharp/
│       └── plugins/
│           └── MatchZy/
│               ├── MatchZy.dll
│               ├── lang/
│               ├── spawns/
│               └── runtimes/
└── cfg/
    └── MatchZy/
        ├── config.cfg
        ├── admins.json
        ├── database.json
        └── [other configs]
```

Users simply extract to their `csgo/` directory!

## Version Bumping

- **patch** - Bug fixes: `0.8.15` → `0.8.16`
- **minor** - New features: `0.8.15` → `0.9.0`
- **major** - Breaking changes: `0.8.15` → `1.0.0`

## Manual Build (Testing)

```bash
# Clean and build
rm -rf bin/ obj/
dotnet restore
dotnet publish -c Release

# Output: bin/Release/net8.0/publish/
```

## Troubleshooting

### Tag already exists

```bash
# Delete and retry
git tag -d v0.8.16
git push origin :refs/tags/v0.8.16
./release.sh patch
```

### Build errors

```bash
rm -rf bin/ obj/
dotnet clean
./release.sh patch
```

