namespace Starflake.Tests;

internal static class Paths
{
    // The test binary lives in Tests/bin/<configuration>/<framework>.
    public static readonly string Content =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "content", "core.json");
}
