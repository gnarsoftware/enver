#!/usr/bin/env bash
set -eu
cd "$(dirname "$0")"
if [[ ! -f go.sum ]]; then
    go mod tidy >&2
fi
exec go run . "$1"
