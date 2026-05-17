#!/usr/bin/env bash
set -eu
cd "$(dirname "$0")"
venv=".venv"
if [[ ! -d "$venv" ]]; then
    python3 -m venv "$venv" >&2
    "$venv/bin/pip" install --quiet -r requirements.txt >&2
fi
exec "$venv/bin/python" run.py "$1"
