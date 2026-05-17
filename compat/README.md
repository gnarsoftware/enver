# Cross-parser compatibility

Checks compatibility of various .env constructs that Enver supports across various
other parsers.

## What's covered

| Runner | Library | Pinned in |
|---|---|---|
| `nodejs` | `dotenv` + `dotenv-expand` (Node) | `runners/nodejs/package.json` |
| `python` | `python-dotenv` | `runners/python/requirements.txt` |
| `go` | `joho/godotenv` | `runners/go/go.mod` |
| `compose` | `docker compose convert` env_file resolution | host's installed `docker` |

## How to run

```sh
./run.sh
```

Output is a per-fixture matrix:

```
=== 01-bare-values ===
  enver:   ✓
  nodejs:  ✓
  python:  ✓
  go:      ✓
  compose: ✓
```

Skipped runners are marked `⊘ (not installed)`.

## Prerequisites

- `bash`, `jq`, `diff`
- `node` + `npm` for the `nodejs` runner
- `python3` + `pip` for the `python` runner
- `go` for the `go` runner
- `docker` (with Compose v2) for the `compose` runner
