using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;

namespace Starflake.Domain;

/// <summary>JSON reading helpers shared by the loaders. Every failure names the key.</summary>
internal static class Json
{
    public static List<T> Items<T>(JsonNode? list, Func<JsonNode, T> build) =>
        list?.AsArray().Select(n => build(n!)).ToList() ?? [];

    public static Dictionary<string, T> Index<T>(JsonNode? list, Func<JsonNode, T> build) where T : NamedThing =>
        Items(list, build).ToDictionary(t => t.Id);

    public static T Find<T>(Dictionary<string, T> index, string id, string key) =>
        index.TryGetValue(id, out var found) ? found : throw new InvalidDataException($"{key}: unknown id '{id}'");

    public static JsonNode Obj(JsonNode node, string key) =>
        node[key] ?? throw new InvalidDataException($"missing {key}");

    public static string Str(JsonNode node, string key) =>
        (string?)node[key] ?? throw new InvalidDataException($"missing {key}");

    public static float Num(JsonNode node, string key) =>
        (float?)node[key] ?? throw new InvalidDataException($"missing {key}");

    // Enum members carry their schema text in [EnumMember], e.g. SpectralClass.G <- "g".
    public static T Enum<T>(JsonNode node, string key) where T : struct, System.Enum
    {
        var text = Str(node, key);
        return TryEnum<T>(text, out var value) ? value : throw new InvalidDataException($"{key}: unknown value '{text}'");
    }

    public static bool TryEnum<T>(string text, out T value) where T : struct, System.Enum
    {
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.GetCustomAttribute<EnumMemberAttribute>()?.Value == text)
            {
                value = (T)field.GetValue(null)!;
                return true;
            }
        value = default;
        return false;
    }

    /// <summary>The schema text of an enum member, e.g. "g" for SpectralClass.G.</summary>
    public static string SchemaText<T>(T value) where T : struct, System.Enum =>
        typeof(T).GetField(value.ToString())!.GetCustomAttribute<EnumMemberAttribute>()!.Value!;
}
