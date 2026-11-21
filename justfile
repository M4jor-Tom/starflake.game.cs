# Run every check: analyzers, the acceptance scenarios and the content.
test: build acceptance content

# Compile the C# and the tests. Analyzer findings and warnings fail the build.
build:
    dotnet build
    dotnet build Tests

# Run the unit tests and acceptance scenarios.
acceptance: build
    dotnet test Tests --no-build

# Validate content/*.json against the pinned ontology.
content:
    tools/sync-ontology.sh --content-only
