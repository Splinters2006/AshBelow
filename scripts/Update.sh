#!/bin/sh
# Runs the Ash Below updater on Linux. It needs Python 3.10 or newer, which most distributions already include.
dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
for candidate in python3 python; do
    if command -v "$candidate" >/dev/null 2>&1 &&
        "$candidate" -c 'import sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)' 2>/dev/null; then
        exec "$candidate" "$dir/update-game.py" --mode release --platform linux --directory "$dir" "$@"
    fi
done
echo "Python 3.10 or newer was not found. Install it with your package manager (for example"
echo "'sudo apt install python3', 'sudo dnf install python3' or 'sudo pacman -S python'), then run this again."
exit 1
