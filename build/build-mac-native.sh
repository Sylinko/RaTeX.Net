#!/bin/bash
set -euo pipefail

if [[ $# -gt 1 ]]; then
    echo "Usage: bash build/build-mac-native.sh [osx-arm64|osx-x64]" >&2
    exit 1
fi

if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "Build macOS native assets on a Mac." >&2
    exit 1
fi

rid="${1:-}"
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

case "$rid" in
    osx-arm64|osx-x64)
        ;;
    *)
        echo "Supported RIDs: osx-arm64, osx-x64." >&2
        exit 1
        ;;
esac

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$script_dir")"
config="$script_dir/native.json"
commit="$(/usr/bin/plutil -extract commit raw -o - "$config")"
repo_url="$(/usr/bin/plutil -extract repoUrl raw -o - "$config")"
toolchain="$(/usr/bin/plutil -extract toolchain raw -o - "$config")"
target="$(/usr/bin/plutil -extract "targets.$rid.target" raw -o - "$config")"
library="$(/usr/bin/plutil -extract "targets.$rid.library" raw -o - "$config")"
cache="$root/artifacts/upstream"
upstream="$cache/RaTeX-$commit"

mkdir -p "$cache"
if [[ ! -d "$upstream" ]]; then
    archive="$cache/ratex.tar.gz"
    curl --fail --location "$repo_url/archive/$commit.tar.gz" --output "$archive"
    tar -xzf "$archive" -C "$cache"
fi

cp "$script_dir/Cargo.lock" "$upstream/Cargo.lock"
rustup target add "$target" --toolchain "$toolchain"
(
    cd "$upstream"
    export RUSTFLAGS=''
    export CARGO_ENCODED_RUSTFLAGS=''
    export RUSTC_WRAPPER=''
    cargo "+$toolchain" build --locked --release -p ratex-ffi --target "$target" --target-dir "$upstream/target"
)

bash "$script_dir/update-mac-notices.sh" "$rid"

destination="$root/src/RaTeX/runtimes/$rid/native"
mkdir -p "$destination"
cp "$upstream/target/$target/release/$library" "$destination/$library"
echo "Built $rid from $upstream -> $destination"
