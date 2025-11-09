#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 MatchZy Automated Release Script${NC}\n"

# Get current version from MatchZy.cs
CURRENT_VERSION=$(grep 'ModuleVersion =>' MatchZy.cs | sed -E 's/.*"(.*)".*/\1/')
if [ -z "$CURRENT_VERSION" ]; then
    echo -e "${RED}❌ Could not detect version from MatchZy.cs${NC}"
    exit 1
fi

# Parse version components
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"

# Check for version bump argument
BUMP_TYPE="${1:-none}"
VERSION="$CURRENT_VERSION"

if [ "$BUMP_TYPE" = "major" ]; then
    MAJOR=$((MAJOR + 1))
    MINOR=0
    PATCH=0
    VERSION="${MAJOR}.${MINOR}.${PATCH}"
    echo -e "${YELLOW}🔼 Bumping MAJOR version: ${CURRENT_VERSION} → ${VERSION}${NC}"
elif [ "$BUMP_TYPE" = "minor" ]; then
    MINOR=$((MINOR + 1))
    PATCH=0
    VERSION="${MAJOR}.${MINOR}.${PATCH}"
    echo -e "${YELLOW}🔼 Bumping MINOR version: ${CURRENT_VERSION} → ${VERSION}${NC}"
elif [ "$BUMP_TYPE" = "patch" ]; then
    PATCH=$((PATCH + 1))
    VERSION="${MAJOR}.${MINOR}.${PATCH}"
    echo -e "${YELLOW}🔼 Bumping PATCH version: ${CURRENT_VERSION} → ${VERSION}${NC}"
elif [ "$BUMP_TYPE" != "none" ]; then
    echo -e "${RED}❌ Invalid bump type: ${BUMP_TYPE}${NC}"
    echo "Usage: ./release.sh [major|minor|patch]"
    echo "  major: 0.8.15 → 1.0.0"
    echo "  minor: 0.8.15 → 0.9.0"
    echo "  patch: 0.8.15 → 0.8.16"
    echo "  (no arg): Use current version"
    exit 1
else
    echo -e "${GREEN}📦 Using current version: ${VERSION}${NC}"
fi

# Update version in MatchZy.cs if bumped
if [ "$BUMP_TYPE" != "none" ]; then
    sed -i '' "s/ModuleVersion => \".*\"/ModuleVersion => \"${VERSION}\"/" MatchZy.cs
    echo -e "${GREEN}✓ Updated MatchZy.cs to version ${VERSION}${NC}"
fi

# Check if tag already exists
if git rev-parse "v${VERSION}" >/dev/null 2>&1; then
    echo -e "${RED}❌ Tag v${VERSION} already exists!${NC}"
    echo "Please bump to a new version or delete the existing tag:"
    echo "  git tag -d v${VERSION}"
    echo "  git push origin :refs/tags/v${VERSION}"
    exit 1
fi

# Clean previous builds
echo -e "\n${BLUE}🧹 Cleaning previous builds...${NC}"
rm -rf bin/ obj/

# Restore dependencies
echo -e "\n${BLUE}📥 Restoring dependencies...${NC}"
dotnet restore

# Build and publish
echo -e "\n${BLUE}🔨 Building project (Release mode)...${NC}"
dotnet publish -c Release

# Create release directory structure
RELEASE_DIR="MatchZy-${VERSION}"
rm -rf "$RELEASE_DIR" "${RELEASE_DIR}.zip"
mkdir -p "$RELEASE_DIR/addons/counterstrikesharp/plugins/MatchZy"
mkdir -p "$RELEASE_DIR/cfg/MatchZy"

# Copy plugin files to proper directory structure
echo -e "\n${BLUE}📂 Creating directory structure...${NC}"
cp -r bin/Release/net8.0/publish/* "$RELEASE_DIR/addons/counterstrikesharp/plugins/MatchZy/"

# Copy config files
echo -e "${BLUE}📂 Copying config files...${NC}"
cp -r cfg/MatchZy/* "$RELEASE_DIR/cfg/MatchZy/"

# Create zip file
echo -e "\n${BLUE}🗜️  Creating release archive...${NC}"
zip -r -q "${RELEASE_DIR}.zip" "$RELEASE_DIR"

# Get file size for display
SIZE=$(du -h "${RELEASE_DIR}.zip" | cut -f1)
echo -e "${GREEN}✓ Created ${RELEASE_DIR}.zip (${SIZE})${NC}"

# Commit changes
echo -e "\n${BLUE}💾 Committing changes...${NC}"
git add .
git commit -m "Release v${VERSION}"

# Create and push tag
echo -e "\n${BLUE}🏷️  Creating Git tag v${VERSION}...${NC}"
CURRENT_BRANCH=$(git branch --show-current)
git tag -a "v${VERSION}" -m "Release version ${VERSION}"
git push origin "$CURRENT_BRANCH"
git push origin "v${VERSION}"

# Set default repo for gh CLI (if not already set)
REPO_URL=$(git remote get-url origin | sed -E 's|.*github.com[:/](.*).git|\1|')
gh repo set-default "$REPO_URL" 2>/dev/null || true

# Create GitHub release
echo -e "\n${BLUE}🌟 Creating GitHub release...${NC}"
gh release create "v${VERSION}" \
    "${RELEASE_DIR}.zip" \
    --title "MatchZy v${VERSION}" \
    --notes "## Installation

1. Download \`${RELEASE_DIR}.zip\`
2. Extract the contents to your CS2 server's \`game/csgo/\` directory
   - The zip contains the proper folder structure (\`addons/\` and \`cfg/\`)
3. Restart your server

## Requirements

- CounterStrikeSharp (latest version)
- CS2 Dedicated Server

## Configuration

Config files are located in \`csgo/cfg/MatchZy/\`:
- \`config.cfg\` - Main plugin configuration
- \`admins.json\` - Admin permissions
- \`database.json\` - Database settings
- \`live.cfg\`, \`warmup.cfg\`, \`knife.cfg\` - Match configs" \
    --draft=false \
    --latest

# Cleanup
echo -e "\n${BLUE}🧹 Cleaning up temporary files...${NC}"
rm -rf "$RELEASE_DIR"

echo -e "\n${GREEN}✅ Release v${VERSION} published successfully!${NC}"
echo -e "${GREEN}🔗 View release: https://github.com/${REPO_URL}/releases/tag/v${VERSION}${NC}"
