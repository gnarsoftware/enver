#!/usr/bin/env bash
# Compat runner: write a throwaway compose.yml that references the fixture
# via `env_file`, then ask `docker compose convert` to resolve it and emit
# the .services.test.environment object as JSON.
#
# Notes:
# - The fixture is copied next to compose.yml so env_file's relative-path
#   semantics match what a real consumer would write.
# - `--env-file /dev/null` prevents docker compose from picking up the
#   workspace's .env file as a substitution source for the compose.yml
#   itself (separate from env_file expansion inside the service).
# - We use `image: alpine` as a placeholder since Compose requires either
#   image or build, and we never actually run anything.
set -eu
fixture="$1"
tmpdir=$(mktemp -d)
trap 'rm -rf "$tmpdir"' EXIT

cp "$fixture" "$tmpdir/env_file"
cat > "$tmpdir/compose.yml" <<'YAML'
services:
  test:
    image: alpine
    env_file: env_file
YAML

# `docker compose convert` doubles every `$` in the rendered environment so
# the resulting compose YAML can be re-parsed without re-substituting. The
# container's actual env block sees a single `$`, so we un-escape here to
# report what the consumer would observe at runtime.
docker compose -f "$tmpdir/compose.yml" --env-file /dev/null convert --format json 2>/dev/null \
    | jq '.services.test.environment // {} | with_entries(.value |= gsub("\\$\\$"; "$"))'
