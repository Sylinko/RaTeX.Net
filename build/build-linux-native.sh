#!/bin/bash
set -euo pipefail

if [[ $# -gt 1 ]]; then
    echo "Usage: bash build/build-linux-native.sh [linux-x64|linux-arm64]" >&2
    exit 1
fi

if [[ "$(uname -s)" != "Linux" ]]; then
    echo "Build Linux native assets on Linux." >&2
    exit 1
fi

case "$(uname -m)" in
    x86_64)
        host_rid="linux-x64"
        ;;
    aarch64)
        host_rid="linux-arm64"
        ;;
    *)
        echo "Unsupported Linux host architecture." >&2
        exit 1
        ;;
esac

rid="${1:-$host_rid}"
case "$rid" in
    linux-x64|linux-arm64)
        ;;
    *)
        echo "Supported RIDs: linux-x64, linux-arm64." >&2
        exit 1
        ;;
esac

if [[ "$rid" != "$host_rid" ]]; then
    echo "Build $rid on a matching Linux host; cross-compilation is not configured." >&2
    exit 1
fi

if ! getconf GNU_LIBC_VERSION >/dev/null 2>&1; then
    echo "Linux native builds require glibc; musl/Alpine is not supported." >&2
    exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$script_dir")"
settings="$(python3 - "$script_dir/native.json" "$rid" <<'PY'
import json
from pathlib import Path
import sys

config = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
native = config["targets"][sys.argv[2]]
print(config["commit"], config["archiveSha256"], config["toolchain"], native["target"], native["library"])
PY
)"
read -r commit archive_hash toolchain target library <<< "$settings"
cache="$root/artifacts/upstream"
upstream="$cache/RaTeX-$commit"

mkdir -p "$cache"
if [[ ! -d "$upstream" ]]; then
    archive="$cache/ratex.tar.gz"
    curl --fail --location "https://codeload.github.com/erweixin/RaTeX/tar.gz/$commit" --output "$archive"
    printf '%s  %s\n' "$archive_hash" "$archive" | sha256sum -c -
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

bash "$script_dir/update-linux-notices.sh" "$rid"

destination="$root/src/RaTeX.Net/runtimes/$rid/native"
mkdir -p "$destination"
cp "$upstream/target/$target/release/$library" "$destination/$library"
