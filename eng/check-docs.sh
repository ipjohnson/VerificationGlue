#!/usr/bin/env bash
# Checks the docs the way CI does:
#   1. snippets: MarkdownSnippets expands every snippet reference, and the result must match the committed files;
#   2. links: lychee finds no broken link or missing anchor;
#   3. style: Vale finds no break of the rules in docs/STYLE.md.
# The diagnostic page check is a spec in test/Docs.Specs and runs with the other specs.
# Needs lychee and vale on PATH. Pass --offline to skip links to other sites.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

lychee_args=()
for arg in "$@"; do
  case "$arg" in
    --offline) lychee_args+=(--offline) ;;
    *) echo "check-docs: unknown argument $arg" >&2; exit 2 ;;
  esac
done

markdown=()
while IFS= read -r file; do markdown+=("$file"); done \
  < <(git ls-files --cached --others --exclude-standard '*.md' ':!:spikes/**')

echo "== Snippets"
before="$(mktemp -d)"
trap 'rm -rf "$before"' EXIT
for file in "${markdown[@]}"; do
  mkdir -p "$before/$(dirname "$file")"
  cp "$file" "$before/$file"
done
dotnet tool restore
dotnet mdsnippets
changed=0
for file in "${markdown[@]}"; do
  diff -u --label "$file" --label "$file (expanded)" "$before/$file" "$file" || changed=1
done
if [ $changed -ne 0 ]; then
  echo "check-docs: dotnet mdsnippets changed the docs above. Commit the expanded files." >&2
  exit 1
fi

echo "== Links"
lychee --no-progress --root-dir "$root" --include-fragments ${lychee_args[@]+"${lychee_args[@]}"} "${markdown[@]}"

echo "== Style"
vale "${markdown[@]}"

echo "check-docs: passed"
