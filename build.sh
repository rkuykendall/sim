#!/bin/bash
set -e

# Build script for SimGame
# Builds for Windows, macOS, and Linux

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Paint Down Build Script${NC}"
echo "========================"

# Check for Godot
GODOT=""
if command -v godot &> /dev/null; then
    GODOT="godot"
elif [ -f "/Applications/Godot.app/Contents/MacOS/Godot" ]; then
    GODOT="/Applications/Godot.app/Contents/MacOS/Godot"
else
    echo -e "${RED}Error: Godot not found in PATH or /Applications${NC}"
    echo "Please install Godot 4.5 or add it to your PATH"
    exit 1
fi

echo -e "Using Godot: ${GREEN}$GODOT${NC}"

# Check for export templates
echo ""
echo "Checking prerequisites..."
echo -e "${YELLOW}Note: Export templates must be installed in Godot Editor first${NC}"
echo "      (Editor -> Manage Export Templates -> Download)"
echo ""

# Create build directories
echo -e "${YELLOW}Creating build directories...${NC}"
mkdir -p builds/paint-town-windows
mkdir -p builds/paint-town-macos
mkdir -p builds/paint-town-linux
echo -e "${GREEN}Done!${NC}"
echo ""

# Export for each platform
echo -e "${YELLOW}Exporting for Windows...${NC}"
"$GODOT" --headless --export-release "Windows" builds/paint-town-windows/SimGame.exe || {
    echo -e "${RED}Windows export failed. Make sure Windows export templates are installed.${NC}"
}

echo ""
echo -e "${YELLOW}Exporting for macOS...${NC}"
"$GODOT" --headless --export-release "macOS" builds/paint-town-macos/SimGame.app || {
    echo -e "${RED}macOS export failed. Make sure macOS export templates are installed.${NC}"
}

echo ""
echo -e "${YELLOW}Exporting for Linux...${NC}"
"$GODOT" --headless --export-release "Linux" builds/paint-town-linux/SimGame.x86_64 || {
    echo -e "${RED}Linux export failed. Make sure Linux export templates are installed.${NC}"
}

# Copy content files (JSON files need to be accessible via filesystem, not packed in .pck)
echo ""
echo -e "${YELLOW}Copying content files...${NC}"

# Windows: content folder next to exe
cp -r content builds/paint-town-windows/
echo "  Copied to builds/paint-town-windows/content/"

# macOS: content folder inside app bundle Resources
cp -r content builds/paint-town-macos/SimGame.app/Contents/Resources/
echo "  Copied to builds/paint-town-macos/SimGame.app/Contents/Resources/content/"

# Linux: content folder next to executable
cp -r content builds/paint-town-linux/
echo "  Copied to builds/paint-town-linux/content/"

echo -e "${GREEN}Done!${NC}"

# Create zip files for each platform
echo ""
echo -e "${YELLOW}Creating zip archives...${NC}"
cd builds
zip -r paint-town-windows.zip paint-town-windows/
echo "  Created builds/paint-town-windows.zip"
zip -r paint-town-macos.zip paint-town-macos/
echo "  Created builds/paint-town-macos.zip"
zip -r paint-town-linux.zip paint-town-linux/
echo "  Created builds/paint-town-linux.zip"
cd ..
echo -e "${GREEN}Done!${NC}"

echo ""
echo "========================"
echo -e "${GREEN}Build complete!${NC}"
echo ""
echo "Output locations:"
echo "  Windows: builds/paint-town-windows/SimGame.exe"
echo "  macOS:   builds/paint-town-macos/SimGame.app"
echo "  Linux:   builds/paint-town-linux/SimGame.x86_64"
echo ""
echo "Zip archives:"
echo "  builds/paint-town-windows.zip"
echo "  builds/paint-town-macos.zip"
echo "  builds/paint-town-linux.zip"
