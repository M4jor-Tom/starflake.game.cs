# Run every check: analyzers, the acceptance scenarios, the boot check, the content and the design pin.
test: build acceptance boot content design

# Compile the C# and the tests. Analyzer findings and warnings fail the build.
build:
    dotnet build
    dotnet build Tests

# Run the unit tests and acceptance scenarios.
acceptance: build
    dotnet test Tests --no-build

# Boot the game headless on the real content and spec, open and close the pause menu; fail on any error.
boot: build
    tools/boot-check.sh

# Validate content/*.json against the pinned ontology.
content:
    tools/sync-ontology.sh --content-only

# Check that spec/*.json and Tests/Features match the pinned design release.
design:
    tools/sync-design.sh --check
