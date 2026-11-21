using Godot;
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

    [Theory]
    [InlineData("readouts", "fuel_tank_full")]
    [InlineData("messages", "jumped")]
    public void Start_fails_when_the_hud_spec_lacks_an_id_the_game_names(string list, string id)
    {
        var hud = Specs.Edited("hud", h =>
        {
            var items = h[list]!.AsArray();
            items.Remove(items.First(i => i!["id"]!.GetValue<string>() == id));
        });
        var (galaxy, spec) = Shipped("hud", hud);
        var e = Assert.Throws<InvalidDataException>(() => Start(galaxy, spec));
        Assert.Contains("hud.json", e.Message);
        Assert.Contains($"'{id}'", e.Message);
    }

    [Theory]
    [InlineData("readouts", "{warp_factor}")]
    [InlineData("readouts", "{key:warp}")]
    [InlineData("messages", "{warp_factor}")]
    public void Start_fails_on_a_hud_text_with_a_placeholder_that_names_nothing(string list, string placeholder)
    {
        var (galaxy, spec) = Shipped("hud", Specs.Edited("hud", h => h[list]![0]!["text"] = placeholder));
        var e = Assert.Throws<InvalidDataException>(() => Start(galaxy, spec));
        Assert.Contains("hud.json", e.Message);
        Assert.Contains(placeholder, e.Message);
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
        Assert.Empty(sim.Projectiles);
    }

    [Fact]
    public void Press_and_Choose_refuse_an_unknown_id_alike()
    {
        var (galaxy, spec) = Minimal();
        var sim = Start(galaxy, spec);
        Assert.Throws<ArgumentException>(() => sim.Press("warp"));
        Assert.Throws<ArgumentException>(() => sim.Choose("warp"));
    }

    [Theory]
    [InlineData(0, 0, 100, true)]
    [InlineData(0, 5.1f, 100, false)]
    [InlineData(0, 0, 3, true)] // starts and ends inside
    [InlineData(4, 0, 100, true)]
    [InlineData(3, 4.1f, 100, false)]
    public void A_swept_segment_hits_a_sphere_of_radius_5_at_the_origin(float x, float y, float startZ, bool hits) =>
        Assert.Equal(hits, Projectile.SegmentHitsSphere(new Vector3(x, y, startZ), new Vector3(x, y, -startZ), Vector3.Zero, 5));

    // A world with one asteroid of radius 20 in the starting system.
    static (World World, Asteroid Asteroid) OneAsteroid()
    {
        var world = new World();
        world.Galaxy.AsteroidField(world.Galaxy.StartingSystem, 1).AsteroidType.Radius = 20;
        return (world, world.Sim.System.Asteroids![0]);
    }

    [Fact]
    public void A_projectile_crossing_a_whole_asteroid_in_one_step_still_hits()
    {
        var (world, asteroid) = OneAsteroid();
        var weapon = world.Galaxy.Weapon();
        weapon.ProjectileSpeed = 100000;
        var integrity = asteroid.Integrity;
        world.FireAt(asteroid, weapon, byPilot: true);
        world.Sim.Step(Dt, PilotInput.None);
        Assert.Equal(integrity - weapon.Damage, asteroid.Integrity);
        Assert.Empty(world.Sim.Projectiles);
    }

    [Fact]
    public void Two_projectiles_breaking_one_asteroid_in_one_step_leave_one_fuel_pod()
    {
        var (world, asteroid) = OneAsteroid();
        var weapon = world.Galaxy.Weapon();
        weapon.Damage = asteroid.Integrity;
        world.FireAt(asteroid, weapon, byPilot: true);
        var second = world.FireAt(asteroid, weapon, byPilot: true);
        world.Run(1);
        Assert.Empty(world.Sim.System.Asteroids ?? []);
        Assert.Single(world.Sim.System.FuelPods ?? []);
        Assert.Contains(second, world.Sim.Projectiles); // nothing left to hit: it flies on
    }

    // Two raider shots on the pilot ship and a pilot shot on an asteroid, all landing in one step.
    [Fact]
    public void A_projectile_that_destroys_the_pilot_ship_does_not_disturb_the_rest_of_the_step()
    {
        var (world, asteroid) = OneAsteroid();
        var ship = world.Sim.Ship;
        ship.Position = new Vector3D { X = 500, Y = 0, Z = 0 }; // away from the arrival point it respawns at
        var lethal = world.Galaxy.Weapon();
        (lethal.Damage, lethal.ProjectileSpeed) = (ship.Hull, 100000);
        var gun = world.Galaxy.Weapon();
        gun.ProjectileSpeed = 100000;
        world.FireAt(ship, lethal, byPilot: false);
        var second = world.FireAt(ship, lethal, byPilot: false);
        world.FireAt(asteroid, gun, byPilot: true);
        var integrity = asteroid.Integrity;

        world.Sim.Step(Dt, PilotInput.None);

        Assert.Equal(ship.ShipClass.MaxHull, ship.Hull);
        Assert.Equal("ship_destroyed", world.Sim.MessageId);
        Assert.Equal(Rules.ArrivalPoint(ship.CurrentSystem, world.Spec.Tuning).ToGodot(), ship.Position.ToGodot());
        Assert.Equal([second], world.Sim.Projectiles); // the respawned ship was gone from its path: one respawn
        Assert.Equal(integrity - gun.Damage, asteroid.Integrity);
        var pose = Pose.Of(ship);
        Assert.True(float.IsFinite(pose.Position.LengthSquared() + pose.Velocity.LengthSquared() + pose.Attitude.LengthSquared()));
        Assert.True(float.IsFinite(second.Position.LengthSquared()));
    }

    [Fact]
    public void A_projectile_with_zero_velocity_does_not_produce_NaN()
    {
        var world = new World();
        world.Galaxy.Weapon(world.Galaxy.StartingShipClass).ProjectileSpeed = 0;
        world.Galaxy.RaiderPack(world.Galaxy.StartingSystem, 1);
        var raider = world.Sim.System.RaiderShips![0];
        world.Hold(Actions.Fire);
        world.Run(World.StepSeconds);
        var projectile = Assert.Single(world.Sim.Projectiles);
        raider.Position = Vector3D.From(projectile.Position); // a target exactly where it sits
        world.Run(1);
        Assert.All(world.Sim.Projectiles, p => Assert.True(float.IsFinite(p.Position.LengthSquared()), $"{p.Position}"));
        Assert.True(raider.Hull < raider.RaiderType.ShipClass.MaxHull, "the projectile sitting on the raider did not hit it");
    }

    [Fact]
    public void A_fuel_pod_exactly_at_the_ship_is_collected_without_NaN()
    {
        var world = new World();
        var ship = world.Sim.Ship;
        var pod = new FuelPod { Id = "pod", FuelAmount = 9, Position = Vector3D.From(ship.Position.ToGodot()) };
        (world.Sim.System.FuelPods ??= []).Add(pod);
        world.Sim.Step(Dt, PilotInput.None);
        Assert.Equal(pod.FuelAmount, ship.Fuel);
        Assert.DoesNotContain(pod, world.Sim.System.FuelPods);
        var pose = Pose.Of(ship);
        Assert.True(float.IsFinite(pose.Position.LengthSquared() + pose.Velocity.LengthSquared() + pose.Attitude.LengthSquared()));
    }

    [Fact]
    public void A_fuel_pod_exactly_at_the_ship_is_pulled_without_NaN_when_the_scoop_cannot_take_it()
    {
        var world = new World();
        var ship = world.Sim.Ship;
        world.Galaxy.StartingShipClass.ScoopRadius = 0; // distance 0 is not below it: the tractor branch, at zero distance
        var pod = new FuelPod { Id = "pod", FuelAmount = 9, Position = Vector3D.From(ship.Position.ToGodot()) };
        (world.Sim.System.FuelPods ??= []).Add(pod);
        Assert.False(Rules.TankFull(ship));
        world.Sim.Step(Dt, PilotInput.None);
        Assert.Contains(pod, world.Sim.System.FuelPods);
        Assert.True(float.IsFinite(pod.Position.ToGodot().LengthSquared()));
    }

    [Fact]
    public void A_raider_exactly_at_the_pilot_ship_decides_without_NaN()
    {
        var world = new World();
        world.Galaxy.RaiderPack(world.Galaxy.StartingSystem, 1);
        var raider = world.Sim.System.RaiderShips![0];
        raider.Position = Vector3D.From(world.Sim.Ship.Position.ToGodot());
        var intent = RaiderAi.Decide(raider, world.Sim.Ship, world.Spec.Tuning.RaiderAi, 1000);
        Assert.True(float.IsFinite(intent.Turn.LengthSquared()));
        world.Sim.Step(Dt, PilotInput.None);
        var pose = Pose.Of(raider);
        Assert.True(float.IsFinite(pose.Position.LengthSquared() + pose.Velocity.LengthSquared() + pose.Attitude.LengthSquared()));
    }
}
