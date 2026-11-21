#!/usr/bin/env bash
# Sync the pinned design release: spec/*.json (from its YAML), Tests/Features
# and Design/Generated. Run inside `nix develop`.
#
#   tools/sync-design.sh          # sync everything
#   tools/sync-design.sh --check  # compare spec and features with the release; write nothing, fail on drift
#
# Overrides for unpublished local checkouts:
#   STARFLAKE_DESIGN_REPO=file://$PWD/../design STARFLAKE_ONTOLOGY_REPO=file://$PWD/../ontology \
#     UV_NO_CACHE=1 LINKML_CSHARP_SOURCE=../../linkml-csharp tools/sync-design.sh
set -euo pipefail
cd "$(dirname "$0")/.."
# shellcheck source=../design.lock
source design.lock
# shellcheck source=../ontology.lock
source ontology.lock

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
# init + fetch instead of clone --branch: a shallow clone of an annotated tag warns that it "is not a commit".
git init --quiet "$tmp/design"
git -C "$tmp/design" fetch --quiet --depth 1 "${STARFLAKE_DESIGN_REPO:-$DESIGN_REPO}" "$DESIGN_REF"
git -C "$tmp/design" -c advice.detachedHead=false checkout --quiet FETCH_HEAD

# The check writes the release into the temp dir and compares; the sync writes it here.
out=.
[ "${1:-}" = --check ] && out="$tmp/out"
rm -f "$out"/spec/*.json "$out"/Tests/Features/*.feature
mkdir -p "$out/spec" "$out/Tests/Features"
for yaml in "$tmp"/design/spec/*.yaml; do
  uv run --no-project --with pyyaml python -c \
    'import json, sys, yaml; open(sys.argv[2], "w", encoding="utf-8").write(json.dumps(yaml.safe_load(open(sys.argv[1], encoding="utf-8")), indent=2, ensure_ascii=False) + "\n")' \
    "$yaml" "$out/spec/$(basename "$yaml" .yaml).json"
done
cp "$tmp"/design/features/*.feature "$out/Tests/Features/"

if [ "$out" != . ]; then
  # Against the working tree, so an unstaged hand edit is drift too, and stays where it is.
  status=0
  diff -r "$out/spec" spec || status=1
  diff -r "$out/Tests/Features" Tests/Features || status=1
  exit "$status"
fi

"$tmp/design/tools/fetch-ontology.sh"
rm -f Design/Generated/*.cs # the generator never deletes classes removed from the schema
uvx --from "${LINKML_CSHARP_SOURCE:-$LINKML_CSHARP}" \
  gen-csharp "$tmp/design/schema/starflake_design.yaml" -d Design/Generated \
  --namespace Starflake.Design --import-namespace starflake-ontology=Starflake.Domain
git status --short -- Design/Generated
