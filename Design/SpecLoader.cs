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
/// Builds a <see cref="Spec"/> from the JSON of spec/*.json. Every failure names its file;
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
        Actions = Items(root["actions"], a => new ControlAction
        {
            Id = Str(a, "id"),
            Kind = Enum<ActionKind>(a, "kind"),
            Bindings = Items(a["bindings"], b => new Binding
            {
                Device = Enum<InputDevice>(b, "device"),
                Key = b["key"] is null ? null : Enum<PhysicalKey>(b, "key"),
                Button = b["button"] is null ? null : Enum<MouseButton>(b, "button"),
            }),
        }),
    };

    static Tuning Tuning(JsonNode root)
    {
        var flight = Obj(root, "flight");
        var ai = Obj(root, "raider_ai");
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
        var offset = Obj(root, "offset");
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
        MessageDurationSeconds = Num(root, "message_duration_seconds"),
        NumberDecimals = (int)Num(root, "number_decimals"),
        Readouts = Items(root["readouts"], r => new HudReadout
        {
            Id = Str(r, "id"), Anchor = Enum<HudAnchor>(r, "anchor"), Text = Str(r, "text"),
        }),
        Messages = Items(root["messages"], m => new HudMessage { Id = Str(m, "id"), Text = Str(m, "text") }),
    };

    static MenuSet Menus(JsonNode root) => new()
    {
        Menus = Items(root["menus"], m => new Menu
        {
            Id = Str(m, "id"), Title = Str(m, "title"),
            Buttons = Items(m["buttons"], b => new MenuButton { Id = Str(b, "id"), Label = Str(b, "label") }),
        }),
    };

    static SessionRules Session(JsonNode root, Galaxy galaxy) => new()
    {
        HostingMode = Enum<HostingMode>(root, "hosting_mode"),
        MaxPlayers = (int)Num(root, "max_players"),
        StartingShipClass = Find(Index(galaxy.ShipClasses), Str(root, "starting_ship_class"), "starting_ship_class"),
        StartingSystem = Find(Index(galaxy.StarSystems), Str(root, "starting_system"), "starting_system"),
    };

    static Dictionary<string, T> Index<T>(IEnumerable<T>? things) where T : NamedThing =>
        (things ?? []).ToDictionary(t => t.Id);
}
