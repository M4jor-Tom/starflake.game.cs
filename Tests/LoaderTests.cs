using Godot;
using Starflake.Domain;

namespace Starflake.Tests;

public class LoaderTests
{
    [Fact]
    public void The_shipped_galaxy_loads()
    {
        var g = GalaxyLoader.Parse(File.ReadAllText(Paths.Content));
        Assert.Equal(5, g.StarSystems.Count);
        Assert.Equal(30, g.ShipClasses!.Single(s => s.Id == "scout").ScoopRadius);
        Assert.Same(g.StarSystems[0], g.StarSystems[1].JumpTargets![0]);
    }

    [Fact]
    public void An_unknown_reference_names_the_key_and_id()
    {
        var e = Assert.Throws<InvalidDataException>(() => GalaxyLoader.Parse(Galaxies.WithUnknownRaiderType));
        Assert.Contains("raider_type", e.Message);
        Assert.Contains("ghost", e.Message);
    }

    [Fact]
    public void Optional_lists_may_be_absent()
    {
        var g = GalaxyLoader.Parse(Galaxies.Minimal);
        Assert.Empty(g.StarSystems[0].JumpTargets ?? []);
        Assert.Empty(g.ShipClasses![0].Weapons ?? []);
    }

    [Fact]
    public void Godot_maths_runs_without_the_engine()
    {
        Assert.Equal(new Vector3(0, 0, -1), Basis.Identity * Vector3.Forward);
    }
}
