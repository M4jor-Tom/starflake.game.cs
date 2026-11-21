namespace Starflake.Tests;

/// <summary>Small galaxies as JSON text, for loader tests.</summary>
internal static class Galaxies
{
    const string ShipClass =
        """{"id":"hulk","name":"Hulk","max_hull":1,"max_speed":1,"acceleration":1,"turn_rate":1,"fuel_capacity":1,"scoop_radius":0,"tractor_range":0,"tractor_speed":0}""";

    const string StarSystem =
        """{"id":"sol","name":"Sol","star_class":"g","bounds_radius":1,"jump_fuel_cost":1""";

    public const string Minimal =
        $$"""{"id":"g","name":"G","ship_classes":[{{ShipClass}}],"star_systems":[{{StarSystem}}}]}""";

    public const string WithUnknownRaiderType =
        $$"""{"id":"g","name":"G","ship_classes":[{{ShipClass}}],"star_systems":[{{StarSystem}},"raider_packs":[{"raider_type":"ghost","center":{"x":0,"y":0,"z":0},"radius":1,"count":1}]}]}""";
}
