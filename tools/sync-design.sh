#!/usr/bin/env bash
# Sync the pinned design release: spec/*.json (from its YAML), Tests/Features
# and Design/Generated. Run inside `nix develop`.
#
#   tools/sync-design.sh          # sync everything
#   tools/sync-design.sh --check  # sync spec and features only; fail on drift
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
git -c advice.detachedHead=false clone --quiet --depth 1 --branch "$DESIGN_REF" \
  "${STARFLAKE_DESIGN_REPO:-$DESIGN_REPO}" "$tmp/design"

rm -f spec/*.json Tests/Features/*.feature
mkdir -p spec Tests/Features
for yaml in "$tmp"/design/spec/*.yaml; do
  uv run --no-project --with pyyaml python -c \
    'import json, sys, yaml; open(sys.argv[2], "w", encoding="utf-8").write(json.dumps(yaml.safe_load(open(sys.argv[1], encoding="utf-8")), indent=2, ensure_ascii=False) + "\n")' \
    "$yaml" "spec/$(basename "$yaml" .yaml).json"
done
cp "$tmp"/design/features/*.feature Tests/Features/

if [ "${1:-}" = --check ]; then
  # Against the index, so staged but uncommitted files are not drift; untracked ones are.
  untracked=$(git ls-files --others --exclude-standard -- spec Tests/Features)
  if ! git diff --quiet -- spec Tests/Features || [ -n "$untracked" ]; then
    git diff --stat -- spec Tests/Features
    echo "$untracked"
    exit 1
  fi
  exit 0
fi

"$tmp/design/tools/fetch-ontology.sh"
rm -f Design/Generated/*.cs # the generator never deletes classes removed from the schema
uvx --from "${LINKML_CSHARP_SOURCE:-$LINKML_CSHARP}" \
  gen-csharp "$tmp/design/schema/starflake_design.yaml" -d Design/Generated \
  --namespace Starflake.Design --import-namespace starflake-ontology=Starflake.Domain
git status --short -- Design/Generated
