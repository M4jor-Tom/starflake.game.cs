using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Starflake.Domain;
using static Starflake.Domain.Json;

namespace Starflake.Design;

/// <summary>
/// Builds a <see cref="Spec"/> from the JSON of spec/*.json, failing on a key missing or unknown. Every failure names its file;
/// the starting ids of session.json are resolved in the galaxy the game loads.
/// Hand-written binding: update it when the design schema changes.
/// </summary>
public static class SpecLoader
{
    public static readonly string[] Files = ["camera", "controls", "hud", "menus", "session", "tuning"];

    public static Spec Parse(IReadOnlyDictionary<string, string> jsonByFile, Galaxy galaxy)
    {
        var raw = Files.ToDictionary(f => f, f => ParseFile(jsonByFile, f));
        return new Spec(
            In("controls", () => Controls(raw["controls"])),
            In("tuning", () => Tuning(raw["tuning"])),
            In("camera", () => Camera(raw["camera"])),
            In("hud", () => Hud(raw["hud"])),
            In("menus", () => Menus(raw["menus"])),
            In("session", () => Session(raw["session"], galaxy)))
        { Raw = raw };
    }

    static JsonNode ParseFile(IReadOnlyDictionary<string, string> jsonByFile, string file) =>
        In(file, () => jsonByFile.TryGetValue(file, out var text)
            ? JsonNode.Parse(text) ?? throw new InvalidDataException("empty")
            : throw new InvalidDataException("missing file"));

    // Prefixes the failure of a file's parse with the file name.
    static T In<T>(string file, Func<T> parse)
    {
        try { return parse(); }
        catch (Exception e) when (e is InvalidDataException or JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{file}.json: {e.Message}", e);
        }
    }

    static ControlScheme Controls(JsonNode root) => new()
    {
        Actions = Items(Only(root, "actions")["actions"], a => new ControlAction
        {
            Id = Str(Only(a, "id", "kind", "bindings"), "id"),
            Kind = Enum<ActionKind>(a, "kind"),
            Bindings = Items(a["bindings"], b => new Binding
            {
                Device = Enum<InputDevice>(Only(b, "device", "key", "button"), "device"),
                Key = b["key"] is null ? null : Enum<PhysicalKey>(b, "key"),
                Button = b["button"] is null ? null : Enum<MouseButton>(b, "button"),
            }),
        }),
    };

    static Tuning Tuning(JsonNode root)
    {
        var flight = Only(Obj(Only(root, "flight", "raider_ai"), "flight"),
            "drag_fraction", "arrival_distance_fraction", "ship_hit_radius", "muzzle_clearance");
        var ai = Only(Obj(root, "raider_ai"), "aim_cone", "steering_gain");
        return new Tuning
        {
            Flight = new FlightTuning
            {
                DragFraction = Num(flight, "drag_fraction"),
                ArrivalDistanceFraction = Num(flight, "arrival_distance_fraction"),
                ShipHitRadius = Num(flight, "ship_hit_radius"),
                MuzzleClearance = Num(flight, "muzzle_clearance"),
            },
            RaiderAi = new RaiderAiTuning { AimCone = Num(ai, "aim_cone"), SteeringGain = Num(ai, "steering_gain") },
        };
    }

    static CameraRig Camera(JsonNode root)
    {
        var offset = Only(Obj(Only(root, "offset", "fov_degrees", "far_plane", "follow_rate", "aim"), "offset"), "x", "y", "z");
        return new CameraRig
        {
            Offset = new Vector3D { X = Num(offset, "x"), Y = Num(offset, "y"), Z = Num(offset, "z") },
            FovDegrees = Num(root, "fov_degrees"),
            FarPlane = Num(root, "far_plane"),
            FollowRate = Num(root, "follow_rate"),
            Aim = Enum<CameraAim>(root, "aim"),
        };
    }

    static Hud Hud(JsonNode root) => new()
    {
        MessageDurationSeconds = Num(Only(root, "message_duration_seconds", "number_decimals", "readouts", "messages"),
            "message_duration_seconds"),
        NumberDecimals = (int)Num(root, "number_decimals"),
        Readouts = Items(root["readouts"], r => new HudReadout
        {
            Id = Str(Only(r, "id", "anchor", "text"), "id"), Anchor = Enum<HudAnchor>(r, "anchor"), Text = Str(r, "text"),
        }),
        Messages = Items(root["messages"], m => new HudMessage { Id = Str(Only(m, "id", "text"), "id"), Text = Str(m, "text") }),
    };

    static MenuSet Menus(JsonNode root) => new()
    {
        Menus = Items(Only(root, "menus")["menus"], m => new Menu
        {
            Id = Str(Only(m, "id", "title", "buttons"), "id"), Title = Str(m, "title"),
            Buttons = Items(m["buttons"], b => new MenuButton { Id = Str(Only(b, "id", "label"), "id"), Label = Str(b, "label") }),
        }),
    };

    static SessionRules Session(JsonNode root, Galaxy galaxy) => new()
    {
        HostingMode = Enum<HostingMode>(Only(root, "hosting_mode", "max_players", "starting_ship_class", "starting_system"),
            "hosting_mode"),
        MaxPlayers = (int)Num(root, "max_players"),
        StartingShipClass = Find(Index(galaxy.ShipClasses), Str(root, "starting_ship_class"), "starting_ship_class"),
        StartingSystem = Find(Index(galaxy.StarSystems), Str(root, "starting_system"), "starting_system"),
    };

    // The design defines every key: one it does not, such as a typo or a renamed slot, fails instead of being ignored.
    static JsonNode Only(JsonNode node, params string[] keys) =>
        node.AsObject().Select(p => p.Key).FirstOrDefault(k => !keys.Contains(k)) is { } unknown
            ? throw new InvalidDataException($"unknown key '{unknown}'")
            : node;

    static Dictionary<string, T> Index<T>(IEnumerable<T>? things) where T : NamedThing =>
        (things ?? []).ToDictionary(t => t.Id);
}
