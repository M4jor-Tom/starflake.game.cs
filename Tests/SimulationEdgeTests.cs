using Starflake.Design;
using Starflake.Domain;
using Starflake.Sim;
using Starflake.Tests.Steps;

namespace Starflake.Tests;

/// <summary>Edge cases of the simulation that no scenario states.</summary>
public class SimulationEdgeTests
{
    const float Dt = 1f / 60;

    // Galaxies.Minimal names its ship class "hulk" and its star system "sol".
    static (Galaxy, Spec) Minimal()
    {
        var galaxy = GalaxyLoader.Parse(Galaxies.Minimal);
        var session = Specs.SessionWith("starting_ship_class", "hulk");
        return (galaxy, Specs.LoadWith("session", session, galaxy));
    }

    // The shipped galaxy with one spec file replaced.
    static (Galaxy, Spec) Shipped(string file, string text)
    {
        var galaxy = GalaxyLoader.Parse(File.ReadAllText(Paths.Content));
        return (galaxy, Specs.LoadWith(file, text, galaxy));
    }

    static Simulation Start(Galaxy galaxy, Spec spec) => Simulation.Start(galaxy, spec, new Random(1), new FakeKeyLabels());

    [Fact]
    public void Start_refuses_a_hosting_mode_other_than_solo()
    {
        var (galaxy, spec) = Shipped("session", Specs.SessionWith("hosting_mode", "player_hosted"));
        var e = Assert.Throws<InvalidDataException>(() => Start(galaxy, spec));
        Assert.Contains("hosting_mode", e.Message);
    }

    [Fact]
    public void Start_fails_when_the_spec_lacks_an_action_the_game_uses()
    {
        var (galaxy, spec) = Shipped("controls", Specs.ControlsWithout(Actions.Jump));
        var e = Assert.Throws<InvalidDataException>(() => Start(galaxy, spec));
        Assert.Contains("jump", e.Message);
    }

    [Fact]
    public void A_system_without_regions_or_jump_targets_simulates()
    {
        var (galaxy, spec) = Minimal();
        var sim = Start(galaxy, spec);
        for (var i = 0; i < 60; i++)
            sim.Step(Dt, PilotInput.None);
        Assert.Null(sim.JumpTarget);
    }

    [Fact]
    public void A_ship_class_without_weapons_simulates_while_fire_is_held()
    {
        var (galaxy, spec) = Minimal();
        var sim = Start(galaxy, spec);
        var fire = new PilotInput(new HashSet<string> { Actions.Fire });
        for (var i = 0; i < 60; i++)
            sim.Step(Dt, fire);
        Assert.Empty(sim.Ship.ShipClass.Weapons ?? []);
    }
}
