#!/usr/bin/env bash
set -eu
cd "$(dirname "$0")"
if [[ ! -d node_modules ]]; then
    npm install --silent --no-audit --no-fund >&2
fi
exec node run.js "$1"
