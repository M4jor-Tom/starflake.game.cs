namespace Starflake.Tests;

internal static class Paths
{
    // The test binary lives in Tests/bin/<configuration>/<framework>.
    public static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static readonly string Content = Path.Combine(Root, "content", "core.json");
}
