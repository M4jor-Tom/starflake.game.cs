using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Starflake.Design;

/// <summary>The gameplay specification: the typed roots of spec/*.json plus lookups by id and slot path.</summary>
public sealed record Spec(ControlScheme Controls, Tuning Tuning, CameraRig Camera, Hud Hud, MenuSet Menus, SessionRules Session)
{
    /// <summary>The parsed JSON of each spec file, by file name without extension; what <see cref="Number"/> reads.</summary>
    public IReadOnlyDictionary<string, JsonNode> Raw { get; init; } = new Dictionary<string, JsonNode>();

    /// <summary>The number at a slot path such as "flight.drag_fraction"; flight and raider_ai are in tuning.json.</summary>
    public float Number(string slotPath)
    {
        var keys = slotPath.Split('.');
        var inTuning = keys[0] is "flight" or "raider_ai";
        var node = Raw.GetValueOrDefault(inTuning ? "tuning" : keys[0]);
        foreach (var key in inTuning ? keys : keys[1..])
            node = (node as JsonObject)?[key];
        return node is JsonValue value && value.TryGetValue<float>(out var number)
            ? number
            : throw new InvalidDataException($"unknown slot path '{slotPath}'");
    }

    public ControlAction Action(string id) => Find(Controls.Actions, a => a.Id, id, "action");

    public HudReadout Readout(string id) => Find(Hud.Readouts, r => r.Id, id, "readout");

    public HudMessage Message(string id) => Find(Hud.Messages, m => m.Id, id, "message");

    public Menu Menu(string id) => Find(Menus.Menus, m => m.Id, id, "menu");

    static T Find<T>(IEnumerable<T> items, Func<T, string> idOf, string id, string what) =>
        items.FirstOrDefault(i => idOf(i) == id) ?? throw new InvalidDataException($"unknown {what} '{id}'");
}
