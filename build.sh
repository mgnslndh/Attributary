#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

TARGET="${1:-Default}"
if [ "$#" -gt 0 ]; then
    shift
fi

dotnet run --project build/Attributary.Build -- "--target=$TARGET" "$@"
