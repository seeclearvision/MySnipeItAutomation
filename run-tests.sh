#!/usr/bin/env bash

# © 2026 Devanand Bhowan. Licensed under the MIT License (see LICENSE).

# Runs the Snipe-IT Playwright tests on macOS / Linux.
# (Same options as run-tests.ps1 and run-tests.cmd)
#
#   ./run-tests.sh -Username USER -Password PASS                            run all tests
#   ./run-tests.sh -Username USER -Password PASS -Filter "Name~ModifyAsset" run tests matching a filter
#   ./run-tests.sh -Username USER -Password PASS -Headed                    visible browser
#
# Options (case-insensitive):
#   -Username USER   Snipe-IT username (else $SNIPEIT_USERNAME)
#   -Password PASS   Snipe-IT password (else $SNIPEIT_PASSWORD)
#   -Filter FILTER   dotnet test filter, e.g. "Name~ModifyAsset"
#   -Headed          show the browser
#   -Help            show this help
#
# The account must be allowed to create, edit, check out and delete assets.
# Credentials are never stored in this repo: pass them or set the environment variables.

set -euo pipefail

usage() { sed -n '5,20p' "$0" | sed 's/^# \{0,1\}//'; }

filter=""
while [[ $# -gt 0 ]]; do
    opt="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
    case "$opt" in
        -username|-password|-filter)
            [[ $# -ge 2 ]] || { echo "Missing value for $1" >&2; exit 2; } ;;
    esac
    case "$opt" in
        -username) SNIPEIT_USERNAME="$2"; shift 2 ;;
        -password) SNIPEIT_PASSWORD="$2"; shift 2 ;;
        -filter)   filter="$2"; shift 2 ;;
        -headed)   export HEADED=1; shift ;;
        -help|-h)  usage; exit 0 ;;
        *)         echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
done

export SNIPEIT_BASE_URL="${SNIPEIT_BASE_URL:-https://demo.snipeitapp.com}"
if [[ -z "${SNIPEIT_USERNAME:-}" || -z "${SNIPEIT_PASSWORD:-}" ]]; then
    echo "Username and password are required: use -Username USER -Password PASS, or set SNIPEIT_USERNAME and SNIPEIT_PASSWORD." >&2
    exit 2
fi
export SNIPEIT_USERNAME SNIPEIT_PASSWORD

# Homebrew's .NET needs DOTNET_ROOT.
if [[ -z "${DOTNET_ROOT:-}" ]] && command -v brew >/dev/null && [[ -d "$(brew --prefix)/opt/dotnet/libexec" ]]; then
    export DOTNET_ROOT="$(brew --prefix)/opt/dotnet/libexec"
fi

cd "$(dirname "$0")"

echo "Snipe-IT: $SNIPEIT_BASE_URL  (account: $SNIPEIT_USERNAME)"

if [[ -n "$filter" ]]; then
    dotnet test --tl:off --filter "$filter" --logger "console;verbosity=normal"
else
    dotnet test --tl:off --logger "console;verbosity=normal"
fi
