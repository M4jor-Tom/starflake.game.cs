# Run every check: analyzers, the self-test and the content.
test: build self-test content

# Compile the C#. Analyzer findings and warnings fail the build.
build:
    dotnet build

# Run the rules and scene checks in a headless Godot.
self-test: build
    godot-mono --headless --path . -- --self-test

# Validate content/*.json against the pinned ontology.
content:
    tools/sync-ontology.sh --content-only
