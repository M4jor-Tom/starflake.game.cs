using System.Text.Json.Nodes;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Tests;

/// <summary>Loads the synced spec/*.json (copied beside the test binary) against the shipped galaxy.</summary>
internal static class Specs
{
    static string Read(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "spec", file + ".json"));

    public static Spec Load() => LoadWith("", "");

    /// <summary>The real spec with one file's text replaced.</summary>
    public static Spec LoadWith(string file, string text) =>
        SpecLoader.Parse(SpecLoader.Files.ToDictionary(f => f, f => f == file ? text : Read(f)),
            GalaxyLoader.Parse(File.ReadAllText(Paths.Content)));

    /// <summary>The real session.json starting in the given star system.</summary>
    public static string SessionStartingIn(string system)
    {
        var session = JsonNode.Parse(Read("session"))!;
        session["starting_system"] = system;
        return session.ToJsonString();
    }
}
