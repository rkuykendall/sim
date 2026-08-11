#!/bin/bash
set -u

# Static-check script for Paint Town
# Runs Godot's --check-only over every GDScript file in src/ and tests/ — catches parse errors,
# undefined members/functions, and type mismatches without actually running the game or tests.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Paint Town Static Check${NC}"
echo "========================"

GODOT=""
if command -v godot &> /dev/null; then
    GODOT="godot"
elif [ -f "/Applications/Godot.app/Contents/MacOS/Godot" ]; then
    GODOT="/Applications/Godot.app/Contents/MacOS/Godot"
elif [ -f "/Applications/Godot_mono.app/Contents/MacOS/Godot" ]; then
    GODOT="/Applications/Godot_mono.app/Contents/MacOS/Godot"
else
    echo -e "${RED}Error: Godot not found in PATH or /Applications${NC}"
    exit 1
fi

failed=0
checked=0

while IFS= read -r file; do
    checked=$((checked + 1))
    output=$("$GODOT" --headless --check-only --script "$file" 2>&1)
    if echo "$output" | grep -qE "SCRIPT ERROR|Parse Error|ERROR:"; then
        echo -e "${RED}FAIL${NC}: $file"
        echo "$output" | grep -E "SCRIPT ERROR|Parse Error|ERROR:" | sed 's/^/    /'
        failed=$((failed + 1))
    fi
done < <(find src tests -name "*.gd" | sort)

echo "========================"
if [ "$failed" -eq 0 ]; then
    echo -e "${GREEN}All $checked files passed.${NC}"
    exit 0
else
    echo -e "${RED}$failed of $checked files failed.${NC}"
    exit 1
fi
