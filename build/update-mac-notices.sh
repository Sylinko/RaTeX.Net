#!/bin/bash
set -euo pipefail

rid=''
check=false
for argument in "$@"; do
    case "$argument" in
        osx-arm64|osx-x64)
            rid="$argument"
            ;;
        --check)
            check=true
            ;;
        *)
            echo "Usage: bash build/update-mac-notices.sh [osx-arm64|osx-x64] [--check]" >&2
            exit 1
            ;;
    esac
done

if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "Generate macOS notices on a Mac." >&2
    exit 1
fi

if [[ -z "$rid" ]]; then
    case "$(uname -m)" in
        arm64)
            rid="osx-arm64"
            ;;
        x86_64)
            rid="osx-x64"
            ;;
        *)
            echo "Unsupported macOS host architecture." >&2
            exit 1
            ;;
    esac
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ "$check" == true ]]; then
    exec python3 "$script_dir/generate-mac-notices.py" "$rid" --check
fi
exec python3 "$script_dir/generate-mac-notices.py" "$rid"
