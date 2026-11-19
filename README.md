# Starflake

Open-world 3D space explorer (Godot 4.7, C#). Fly from star system to star
system: shoot asteroids and pirates, collect the fuel they drop, and jump to a
neighbouring system once the tank holds the system's jump cost.

## Run

```shell
nix develop                      # Godot .NET, .NET 8 SDK, uv
dotnet build                     # compile C# (after checkout and every code change)
godot-mono --path .              # play; or `godot-mono -e --path .` for the editor (F5 runs)
godot-mono --headless --path . -- --self-test   # rules + scene checks, exit 1 on failure
```

Controls: W/S thrust · A/D yaw · ↑/↓ pitch · Q/E roll · Space or click fire ·
Tab next jump target · J jump.

## Layout

| Path | What | Edited by |
|---|---|---|
| `Domain/Generated/` | Domain classes generated from the ontology | `tools/sync-ontology.sh` only |
| `Domain/Bindings/` | Partial-class extensions and the content loader | hand |
| `Game/` | Rules (engine-free) and Godot nodes | hand |
| `content/core.json` | Ships, weapons, enemies, asteroids, galaxy | designers, validated against the ontology |
| `ontology.lock` | Pinned ontology release and generator | bump to upgrade |

## Ontology

The domain model lives in the separate
[starflake-ontology](https://github.com/M4jor-Tom/starflake-ontology) repository.
`ontology.lock` pins a release tag. To upgrade:

1. bump `ONTOLOGY_REF` in `ontology.lock`;
2. run `tools/sync-ontology.sh` (inside `nix develop`): it regenerates
   `Domain/Generated/` and validates `content/*.json` against the same schema;
3. `git status Domain/Generated` shows the drift; fix `Domain/Bindings/` and
   `Game/` until the build and the self-test pass, then commit everything together.

After editing content only, `tools/sync-ontology.sh --content-only` validates it
without regenerating code. `Domain/Generated/.gdignore` keeps Godot from
importing generated classes; MSBuild still compiles them.

Until the ontology and [linkml-csharp](https://github.com/M4jor-Tom/linkml-csharp)
are published, point the script at local checkouts:

```shell
STARFLAKE_ONTOLOGY_REPO=file://$PWD/../ontology LINKML_CSHARP_SOURCE=../../linkml-csharp tools/sync-ontology.sh
```
