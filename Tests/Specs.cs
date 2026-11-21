using System.Text.Json.Nodes;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Tests;

/// <summary>Loads the synced spec/*.json (copied beside the test binary), by default against the shipped galaxy.</summary>
internal static class Specs
{
    static string Read(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "spec", file + ".json"));

    public static Spec Load(Galaxy? galaxy = null) => LoadWith("", "", galaxy);

    /// <summary>The real spec with one file's text replaced.</summary>
    public static Spec LoadWith(string file, string text, Galaxy? galaxy = null) =>
        SpecLoader.Parse(SpecLoader.Files.ToDictionary(f => f, f => f == file ? text : Read(f)),
            galaxy ?? GalaxyLoader.Parse(File.ReadAllText(Paths.Content)));

    /// <summary>The string value of a key of the real session.json, such as its starting system id.</summary>
    public static string SessionValue(string key) => JsonNode.Parse(Read("session"))![key]!.GetValue<string>();

    /// <summary>The real JSON text of a spec file after an edit.</summary>
    public static string Edited(string file, Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(Read(file))!;
        edit(node);
        return node.ToJsonString();
    }
}
