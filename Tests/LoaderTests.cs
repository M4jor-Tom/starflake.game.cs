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

    [Fact]
    public void The_synced_spec_loads_against_the_shipped_galaxy()
    {
        var s = Specs.Load();
        Assert.Equal("scout", s.Session.StartingShipClass.Id);
        Assert.Equal(s.Raw["controls"]["actions"]!.AsArray().Count, s.Controls.Actions.Count);
        Assert.Equal(s.Tuning.Flight.DragFraction, s.Number("flight.drag_fraction"));
        Assert.Equal(s.Camera.FovDegrees, s.Number("camera.fov_degrees"));
    }

    [Fact]
    public void A_starting_system_missing_from_the_galaxy_fails_naming_it()
    {
        var e = Assert.Throws<InvalidDataException>(() => Specs.LoadWith("session", Specs.Edited("session", s => s["starting_system"] = "nowhere")));
        Assert.Contains("starting_system", e.Message);
        Assert.Contains("nowhere", e.Message);
    }

    [Fact]
    public void A_missing_key_names_its_file()
    {
        var e = Assert.Throws<InvalidDataException>(() => Specs.LoadWith("tuning", "{\"flight\":{}}"));
        Assert.Contains("tuning", e.Message);
        Assert.Contains("raider_ai", e.Message);
    }

    [Fact]
    public void An_unknown_key_names_its_file_and_the_key()
    {
        var tuning = Specs.Edited("tuning", t => t["flight"]!["drag_fractoin"] = 0);
        var e = Assert.Throws<InvalidDataException>(() => Specs.LoadWith("tuning", tuning));
        Assert.Contains("tuning.json", e.Message);
        Assert.Contains("'drag_fractoin'", e.Message);
    }

    [Fact]
    public void An_unknown_id_lookup_names_it()
    {
        Assert.Contains("warp", Assert.Throws<InvalidDataException>(() => Specs.Load().Action("warp")).Message);
    }

    [Fact]
    public void An_unknown_slot_path_fails()
    {
        Assert.Throws<InvalidDataException>(() => Specs.Load().Number("flight.nope"));
    }
}
