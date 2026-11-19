using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;

namespace Starflake.Domain;

/// <summary>
/// Builds a <see cref="ContentPack"/> from its JSON form. The shape is validated
/// against the ontology by tools/sync-ontology.sh; this loader also resolves
/// references by identifier, which LinkML validation does not check.
/// Hand-written binding: update it when the ontology changes.
/// </summary>
public static class ContentLoader
{
    public static ContentPack Parse(string json)
    {
        var root = JsonNode.Parse(json) ?? throw new InvalidDataException("empty content");

        var weapons = Index(root["weapon_types"], w => new WeaponType
        {
            Id = Str(w, "id"), Name = Str(w, "name"), Description = (string?)w["description"],
            Damage = Num(w, "damage"),
            FireRate = Num(w, "fire_rate"),
            ProjectileSpeed = Num(w, "projectile_speed"),
            MaxRange = Num(w, "max_range"),
        });
        var ships = Index(root["ship_classes"], s => new ShipClass
        {
            Id = Str(s, "id"), Name = Str(s, "name"), Description = (string?)s["description"],
            MaxHull = Num(s, "max_hull"),
            MaxSpeed = Num(s, "max_speed"),
            Acceleration = Num(s, "acceleration"),
            TurnRate = Num(s, "turn_rate"),
            FuelCapacity = Num(s, "fuel_capacity"),
            Weapons = Refs(weapons, s, "weapons"),
        });
        var enemies = Index(root["enemy_types"], e => new EnemyType
        {
            Id = Str(e, "id"), Name = Str(e, "name"), Description = (string?)e["description"],
            ShipClass = Ref(ships, e, "ship_class"),
            DetectionRange = Num(e, "detection_range"),
            FuelDrop = Num(e, "fuel_drop"),
        });
        var asteroids = Index(root["asteroid_types"], a => new AsteroidType
        {
            Id = Str(a, "id"), Name = Str(a, "name"), Description = (string?)a["description"],
            Radius = Num(a, "radius"),
            MaxIntegrity = Num(a, "max_integrity"),
            FuelDrop = Num(a, "fuel_drop"),
        });

        var galaxy = root["galaxy"] ?? throw new InvalidDataException("missing galaxy");
        var systems = Index(galaxy["star_systems"], s => new StarSystem
        {
            Id = Str(s, "id"), Name = Str(s, "name"), Description = (string?)s["description"],
            StarClass = Enum<SpectralClass>(s, "star_class"),
            BoundsRadius = Num(s, "bounds_radius"),
            JumpFuelCost = Num(s, "jump_fuel_cost"),
            JumpTargets = [],
            AsteroidFields = Items(s["asteroid_fields"], f => new AsteroidField
            {
                AsteroidType = Ref(asteroids, f, "asteroid_type"),
                Center = Vec(f, "center"),
                Radius = Num(f, "radius"),
                SpawnCount = (int)Num(f, "spawn_count"),
            }),
            EnemyGroups = Items(s["enemy_groups"], g => new EnemyGroup
            {
                EnemyType = Ref(enemies, g, "enemy_type"),
                Center = Vec(g, "center"),
                Radius = Num(g, "radius"),
                SpawnCount = (int)Num(g, "spawn_count"),
            }),
        });
        // Systems reference each other: link jump routes once all of them exist.
        foreach (var s in Items(galaxy["star_systems"], s => s))
            systems[Str(s, "id")].JumpTargets!.AddRange(Refs(systems, s, "jump_targets"));

        return new ContentPack
        {
            Id = Str(root, "id"), Name = Str(root, "name"), Description = (string?)root["description"],
            WeaponTypes = [.. weapons.Values],
            ShipClasses = [.. ships.Values],
            EnemyTypes = [.. enemies.Values],
            AsteroidTypes = [.. asteroids.Values],
            StartingShipClass = Ref(ships, root, "starting_ship_class"),
            Galaxy = new Galaxy
            {
                Id = Str(galaxy, "id"), Name = Str(galaxy, "name"), Description = (string?)galaxy["description"],
                StarSystems = [.. systems.Values],
                StartingSystem = Ref(systems, galaxy, "starting_system"),
            },
        };
    }

    static List<T> Items<T>(JsonNode? list, Func<JsonNode, T> build) =>
        list?.AsArray().Select(n => build(n!)).ToList() ?? [];

    static Dictionary<string, T> Index<T>(JsonNode? list, Func<JsonNode, T> build) where T : NamedThing =>
        Items(list, build).ToDictionary(t => t.Id);

    static T Find<T>(Dictionary<string, T> index, string id, string key) =>
        index.TryGetValue(id, out var found) ? found : throw new InvalidDataException($"{key}: unknown id '{id}'");

    static T Ref<T>(Dictionary<string, T> index, JsonNode node, string key) => Find(index, Str(node, key), key);

    static List<T> Refs<T>(Dictionary<string, T> index, JsonNode node, string key) =>
        Items(node[key], id => Find(index, (string)id!, key));

    static string Str(JsonNode node, string key) =>
        (string?)node[key] ?? throw new InvalidDataException($"missing {key}");

    static float Num(JsonNode node, string key) =>
        (float?)node[key] ?? throw new InvalidDataException($"missing {key}");

    static Vector3D Vec(JsonNode node, string key)
    {
        var v = node[key] ?? throw new InvalidDataException($"missing {key}");
        return new Vector3D { X = Num(v, "x"), Y = Num(v, "y"), Z = Num(v, "z") };
    }

    // Enum members carry their schema text in [EnumMember], e.g. SpectralClass.G <- "g".
    static T Enum<T>(JsonNode node, string key) where T : struct, System.Enum
    {
        var text = Str(node, key);
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.GetCustomAttribute<EnumMemberAttribute>()?.Value == text)
                return (T)field.GetValue(null)!;
        throw new InvalidDataException($"{key}: unknown value '{text}'");
    }
}
