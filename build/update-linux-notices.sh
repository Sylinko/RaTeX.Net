#!/bin/bash
set -euo pipefail

rid=''
check=false
for argument in "$@"; do
    case "$argument" in
        linux-x64|linux-arm64)
            rid="$argument"
            ;;
        --check)
            check=true
            ;;
        *)
            echo "Usage: bash build/update-linux-notices.sh [linux-x64|linux-arm64] [--check]" >&2
            exit 1
            ;;
    esac
done

if [[ "$(uname -s)" != "Linux" ]]; then
    echo "Generate Linux notices on Linux." >&2
    exit 1
fi

if [[ -z "$rid" ]]; then
    case "$(uname -m)" in
        x86_64)
            rid="linux-x64"
            ;;
        aarch64)
            rid="linux-arm64"
            ;;
        *)
            echo "Unsupported Linux host architecture." >&2
            exit 1
            ;;
    esac
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ "$check" == true ]]; then
    exec python3 "$script_dir/generate-native-notices.py" "$rid" --check
fi
exec python3 "$script_dir/generate-native-notices.py" "$rid"
