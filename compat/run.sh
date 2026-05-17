#!/usr/bin/env bash
# Driver for the cross-parser compatibility harness.
#
# For each fixture under fixtures/*.env, runs every available runner under
# runners/<name>/run.sh and diffs the runner's JSON output against the
# fixture's .expected.json. Runners whose toolchain isn't installed are
# skipped, not failed.
set -u

cd "$(dirname "$0")"

if ! command -v jq >/dev/null; then
    echo "error: jq is required (used for JSON normalization and diffing)" >&2
    exit 2
fi

# Discover runners by scanning the runners directory. Each runner must expose:
#   runners/<name>/runnable.sh  — exits 0 if the toolchain is available, non-zero otherwise
#   runners/<name>/run.sh       — parses a .env file (path given as $1) and emits JSON to stdout
runners=()
available=""
for dir in runners/*/; do
    r=$(basename "$dir")
    runners+=("$r")
    if [[ -x "$dir/runnable.sh" ]] && bash "$dir/runnable.sh" 2>/dev/null; then
        available="$available $r"
    fi
done

is_available() {
    case " $available " in *" $1 "*) return 0 ;; *) return 1 ;; esac
}

# Output formatting.
green=$'\e[32m'; red=$'\e[31m'; dim=$'\e[2m'; reset=$'\e[0m'
# Disable color if stdout isn't a terminal.
[[ -t 1 ]] || { green=""; red=""; dim=""; reset=""; }

declare -i pass=0 fail=0 skip=0

for fixture in fixtures/*.env; do
    name=$(basename "$fixture" .env)
    expected="fixtures/$name.expected.json"
    if [[ ! -f "$expected" ]]; then
        echo "error: missing $expected for fixture $fixture" >&2
        exit 2
    fi

    echo "=== $name ==="
    expected_norm=$(jq -S . "$expected")

    for r in "${runners[@]}"; do
        if ! is_available "$r"; then
            printf "  %-8s %s%s%s\n" "$r:" "$dim" "⊘ (not installed)" "$reset"
            ((skip++)) || true
            continue
        fi

        # Capture stdout only; runner setup chatter goes to stderr.
        if ! actual=$(bash "runners/$r/run.sh" "$(pwd)/$fixture" 2>/dev/null); then
            printf "  %-8s %s%s%s\n" "$r:" "$red" "✗ (runner failed to parse)" "$reset"
            ((fail++)) || true
            continue
        fi

        # Normalize via jq -S so key order can't cause spurious diffs.
        if ! actual_norm=$(echo "$actual" | jq -S . 2>/dev/null); then
            printf "  %-8s %s%s%s\n" "$r:" "$red" "✗ (runner emitted invalid JSON)" "$reset"
            ((fail++)) || true
            continue
        fi

        if [[ "$actual_norm" == "$expected_norm" ]]; then
            printf "  %-8s %s%s%s\n" "$r:" "$green" "✓" "$reset"
            ((pass++)) || true
        else
            printf "  %-8s %s%s%s\n" "$r:" "$red" "✗" "$reset"
            diff <(echo "$expected_norm") <(echo "$actual_norm") | sed 's/^/      /'
            ((fail++)) || true
        fi
    done
    echo
done

echo "Pass: $pass  Fail: $fail  Skipped: $skip"
[[ $fail -eq 0 ]]
