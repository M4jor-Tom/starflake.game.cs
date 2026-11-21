using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using static Starflake.Domain.Json;

namespace Starflake.Domain;

/// <summary>
/// Builds a <see cref="Galaxy"/> from its JSON form. The shape is validated
/// against the ontology by tools/sync-ontology.sh; this loader also resolves
/// references by identifier, which LinkML validation does not check.
/// Hand-written binding: update it when the ontology changes.
/// </summary>
public static class GalaxyLoader
{
    public static Galaxy Parse(string json)
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
            ScoopRadius = Num(s, "scoop_radius"),
            TractorRange = Num(s, "tractor_range"),
            TractorSpeed = Num(s, "tractor_speed"),
            Weapons = Refs(weapons, s, "weapons"),
        });
        var raiders = Index(root["raider_types"], r => new RaiderType
        {
            Id = Str(r, "id"), Name = Str(r, "name"), Description = (string?)r["description"],
            ShipClass = Ref(ships, r, "ship_class"),
            DetectionRange = Num(r, "detection_range"),
            KeepDistance = Num(r, "keep_distance"),
            FuelYield = Num(r, "fuel_yield"),
        });
        var asteroids = Index(root["asteroid_types"], a => new AsteroidType
        {
            Id = Str(a, "id"), Name = Str(a, "name"), Description = (string?)a["description"],
            Radius = Num(a, "radius"),
            MaxIntegrity = Num(a, "max_integrity"),
            FuelYield = Num(a, "fuel_yield"),
        });

        var systems = Index(root["star_systems"], s => new StarSystem
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
                Count = (int)Num(f, "count"),
            }),
            RaiderPacks = Items(s["raider_packs"], p => new RaiderPack
            {
                RaiderType = Ref(raiders, p, "raider_type"),
                Center = Vec(p, "center"),
                Radius = Num(p, "radius"),
                Count = (int)Num(p, "count"),
            }),
        });
        // Systems reference each other: link jump routes once all of them exist.
        foreach (var s in Items(root["star_systems"], s => s))
            systems[Str(s, "id")].JumpTargets!.AddRange(Refs(systems, s, "jump_targets"));

        return new Galaxy
        {
            Id = Str(root, "id"), Name = Str(root, "name"), Description = (string?)root["description"],
            WeaponTypes = [.. weapons.Values],
            ShipClasses = [.. ships.Values],
            RaiderTypes = [.. raiders.Values],
            AsteroidTypes = [.. asteroids.Values],
            StarSystems = [.. systems.Values],
        };
    }

    static T Ref<T>(Dictionary<string, T> index, JsonNode node, string key) => Find(index, Str(node, key), key);

    static List<T> Refs<T>(Dictionary<string, T> index, JsonNode node, string key) =>
        Items(node[key], id => Find(index, (string)id!, key));

    static Vector3D Vec(JsonNode node, string key)
    {
        var v = node[key] ?? throw new InvalidDataException($"missing {key}");
        return new Vector3D { X = Num(v, "x"), Y = Num(v, "y"), Z = Num(v, "z") };
    }
}
