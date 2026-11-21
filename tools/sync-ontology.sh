#!/usr/bin/env bash
# Regenerate Domain/Generated from the pinned ontology release, then validate
# content/*.json against the same schema. Run inside `nix develop`.
#
#   tools/sync-ontology.sh          # regenerate + validate content
#   tools/sync-ontology.sh --check  # compare Domain/Generated with the release, write nothing; validate content
#
# Overrides for local checkouts:
#   STARFLAKE_ONTOLOGY_REPO=file://$PWD/../ontology UV_NO_CACHE=1 LINKML_CSHARP_SOURCE=../../linkml-csharp tools/sync-ontology.sh
set -euo pipefail
cd "$(dirname "$0")/.."
# shellcheck source=../ontology.lock
source ontology.lock

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
# init + fetch instead of clone --branch: a shallow clone of an annotated tag warns that it "is not a commit".
git init --quiet "$tmp/ontology"
git -C "$tmp/ontology" fetch --quiet --depth 1 "${STARFLAKE_ONTOLOGY_REPO:-$ONTOLOGY_REPO}" "$ONTOLOGY_REF"
git -C "$tmp/ontology" -c advice.detachedHead=false checkout --quiet FETCH_HEAD
schema="$tmp/ontology/src/starflake_ontology/schema/starflake_ontology.yaml"

# The check generates into the temp dir and compares; the sync writes here.
out=Domain/Generated
[ "${1:-}" = --check ] && out="$tmp/generated"
rm -f "$out"/*.cs # the generator never deletes classes removed from the schema
uvx --from "${LINKML_CSHARP_SOURCE:-$LINKML_CSHARP}" \
  gen-csharp "$schema" -d "$out" --namespace Starflake.Domain
status=0
diff -r -x .gdignore "$out" Domain/Generated || status=1

for content in content/*.json; do
  uvx --from "linkml==$LINKML_VERSION" linkml-validate -s "$schema" -C Galaxy "$content"
done
exit "$status"
