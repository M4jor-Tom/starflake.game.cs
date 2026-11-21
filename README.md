# Starflake

Open-world 3D space explorer (Godot 4.7, C#), played solo. Each star system
holds asteroid fields and raider ships; breaking them up is the only source of
the fuel that each jump on to a neighbouring system burns.

## Run

```shell
nix develop                      # Godot .NET, .NET 8 SDK, uv, just
dotnet build                     # compile C# (after checkout and every code change)
godot-mono --path .              # play; or `godot-mono -e --path .` for the editor (F5 runs)
just test                        # every check: analyzers, acceptance scenarios, boot check, content, design
```

`nix develop` enables the pre-push hook in `.githooks/`, which runs `just test`.
CI runs the same command on every pull request. The build fails on a method
over 80 lines, a file over 1000 lines or a cognitive complexity over 15
(SonarAnalyzer's defaults, see `.editorconfig`).

Controls: see `spec/controls.json` (the HUD also lists them while you play).

## Layout

| Path | What | Edited by |
|---|---|---|
| `Domain/Generated/` | Domain classes generated from the ontology | `tools/sync-ontology.sh` only |
| `Domain/Bindings/` | Partial-class extensions and the galaxy loader | hand |
| `Design/Generated/` | Spec classes generated from the design schema | `tools/sync-design.sh` only |
| `Design/` (rest) | The spec and its loader | hand |
| `spec/*.json` | Gameplay specification: tuning, HUD, menus, controls, camera, session | `tools/sync-design.sh` only |
| `Sim/` | The simulation (engine-free): flight, combat, raiders, fuel, HUD, camera, menus | hand |
| `Game/` | Godot nodes that feed input to the simulation and draw it | hand |
| `Tests/Features/` | Acceptance scenarios of the design release | `tools/sync-design.sh` only |
| `Tests/` (rest) | Step bindings, loader and edge tests, the spec-literal scan | hand |
| `content/core.json` | The galaxy: ships, weapons, raiders, asteroids, star systems | designers, validated against the ontology |
| `ontology.lock` | Pinned ontology release and generator | bump to upgrade |
| `design.lock` | Pinned design release | bump to upgrade |

## Ontology

The domain model lives in the separate
[starflake.ontology.linkml](https://github.com/M4jor-Tom/starflake.ontology.linkml) repository.
`ontology.lock` pins a release tag. To upgrade:

1. bump `ONTOLOGY_REF` in `ontology.lock`;
2. run `tools/sync-ontology.sh` (inside `nix develop`): it regenerates
   `Domain/Generated/` and validates `content/*.json` against the same schema;
3. `git status Domain/Generated` shows the drift; fix `Domain/Bindings/` and
   `Design/`, `Sim/` and `Game/` until `just test` passes, then commit everything together.

`just content` (part of `just test`) runs `tools/sync-ontology.sh --check`: it
fails when `Domain/Generated/` differs from what the pinned release generates,
and validates `content/*.json`, without writing anything. `Domain/Generated/.gdignore` keeps Godot from
importing generated classes; MSBuild still compiles them.

## Design

The gameplay specification (tuning, HUD texts, menus, controls, camera, session)
and its acceptance scenarios live in the separate
[starflake.design](https://github.com/M4jor-Tom/starflake.design) repository.
`design.lock` pins a release tag. Code and content never restate a number or a
display text of the spec: they read it from `spec/*.json`, and a test fails when
they do not. To upgrade:

1. bump `DESIGN_REF` in `design.lock`;
2. run `tools/sync-design.sh` (inside `nix develop`): it rewrites `spec/*.json`
   and `Tests/Features/`, and regenerates `Design/Generated/`;
3. `git status spec Tests/Features Design/Generated` shows the drift; fix
   `Design/`, `Sim/`, `Game/` and the step bindings in `Tests/Steps/` until
   `just test` passes, then commit everything together.

`just design` (part of `just test`) runs `tools/sync-design.sh --check`: it
fails when `spec/`, `Tests/Features/` or `Design/Generated/` differ from the
pinned release, without writing anything.

## Local generator checkout

`ontology.lock` also pins the release of
[linkml-csharp](https://github.com/M4jor-Tom/linkml-csharp) that generates both
`Generated/` directories. To try an unreleased generator, point both sync
scripts at a local checkout. `UV_NO_CACHE=1` makes `uvx` build it afresh instead
of serving a stale cached build:

```shell
UV_NO_CACHE=1 LINKML_CSHARP_SOURCE=../../linkml-csharp tools/sync-ontology.sh
UV_NO_CACHE=1 LINKML_CSHARP_SOURCE=../../linkml-csharp tools/sync-design.sh
```
