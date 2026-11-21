#!/usr/bin/env bash
# Regenerate Domain/Generated from the pinned ontology release, then validate
# content/*.json against the same schema. Run inside `nix develop`.
# Schema drift shows up as changes under Domain/Generated in `git status`.
#
#   tools/sync-ontology.sh                 # regenerate + validate content
#   tools/sync-ontology.sh --content-only  # validate content after editing it
#
# Overrides for unpublished local checkouts:
#   STARFLAKE_ONTOLOGY_REPO=file://$PWD/../ontology UV_NO_CACHE=1 LINKML_CSHARP_SOURCE=../../linkml-csharp tools/sync-ontology.sh
set -euo pipefail
cd "$(dirname "$0")/.."
# shellcheck source=../ontology.lock
source ontology.lock

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
git -c advice.detachedHead=false clone --quiet --depth 1 --branch "$ONTOLOGY_REF" \
  "${STARFLAKE_ONTOLOGY_REPO:-$ONTOLOGY_REPO}" "$tmp/ontology"
schema="$tmp/ontology/src/starflake_ontology/schema/starflake_ontology.yaml"

out=Domain/Generated
if [ "${1:-}" != --content-only ]; then
  rm -f "$out"/*.cs # the generator never deletes classes removed from the schema
  uvx --from "${LINKML_CSHARP_SOURCE:-$LINKML_CSHARP}" \
    gen-csharp "$schema" -d "$out" --namespace Starflake.Domain
  git status --short -- "$out"
fi

for content in content/*.json; do
  uvx --from "linkml==$LINKML_VERSION" linkml-validate -s "$schema" -C Galaxy "$content"
done
