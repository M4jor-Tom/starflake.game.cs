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

    /// <summary>The real session.json with one string value replaced.</summary>
    public static string SessionWith(string key, string value)
    {
        var session = JsonNode.Parse(Read("session"))!;
        session[key] = value;
        return session.ToJsonString();
    }

    /// <summary>The real session.json starting in the given star system.</summary>
    public static string SessionStartingIn(string system) => SessionWith("starting_system", system);

    /// <summary>The real JSON text of a spec file after an edit.</summary>
    public static string Edited(string file, Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(Read(file))!;
        edit(node);
        return node.ToJsonString();
    }

    /// <summary>The real controls.json without the given action.</summary>
    public static string ControlsWithout(string actionId)
    {
        var controls = JsonNode.Parse(Read("controls"))!;
        var actions = controls["actions"]!.AsArray();
        actions.Remove(actions.First(a => a!["id"]!.GetValue<string>() == actionId));
        return controls.ToJsonString();
    }
}
