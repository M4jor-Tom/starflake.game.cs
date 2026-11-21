using Godot;
using Reqnroll;
using Starflake.Domain;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class RespawnSteps(World world)
{
    World? _control; // the same star system, run without the hit
    StarSystem? _system;
    List<SpaceEntity> _left = [];
    List<Vector3> _leftAt = [];

    World Control => _control ?? throw new InvalidOperationException("no star system given");

    [Given(@"a pilot ship holding ([\d.]+) fuel whose ship class has a max hull of ([\d.]+)")]
    public void GivenAPilotShipHoldingFuel(float fuel, float maxHull)
    {
        world.Galaxy.StartingShipClass.MaxHull = maxHull;
        var ship = world.Sim.Ship;
        ship.Fuel = fuel;
        _system = ship.CurrentSystem;
        // Away from the arrival point, turned, and moving head-on into the -X side World.FireAt shoots from,
        // so the respawn has a pose to undo and the shot still meets the ship.
        ship.Position = new Vector3D { X = 400, Y = -300, Z = 700 };
        ship.Velocity = new Vector3D { X = -30, Y = 0, Z = 0 };
        ship.Orientation = Attitude.From(new Quaternion(Vector3.Up, 1.2f));
    }

    [When("a hit takes its hull to 0")]
    [Scope(Feature = "Respawn")]
    public void WhenAHitTakesItsHullTo0() => world.HitToZero(world.Sim.Ship);

    [Then(@"the ship has ([\d.]+) hull")]
    public void ThenTheShipHasHull(float hull) => Assert.Equal(hull, world.Sim.Ship.Hull);

    [Then(@"the ship holds ([\d.]+) fuel")]
    public void ThenTheShipHoldsFuel(float fuel) => Assert.Equal(fuel, world.Sim.Ship.Fuel);

    [Then("the ship is in the same star system")]
    public void ThenTheShipIsInTheSameStarSystem() => Assert.Same(_system, world.Sim.Ship.CurrentSystem);

    static void Populate(World w, int asteroids, int raiders, int pods)
    {
        w.Galaxy.AsteroidField(w.Galaxy.StartingSystem, asteroids);
        w.Galaxy.RaiderPack(w.Galaxy.StartingSystem, raiders);
        var state = w.Sim.System;
        for (var i = 0; i < raiders; i++)
            state.RaiderShips![i].Velocity = new Vector3D { X = 40 + 5 * i, Y = 0, Z = 30 };
        for (var i = 0; i < pods; i++)
            (state.FuelPods ??= []).Add(new FuelPod { Id = $"pod-{i}", FuelAmount = 9, Position = new Vector3D { X = 70 * i, Y = 0, Z = -600 } });
    }

    [Given(@"a star system holding (\d+) asteroids, (\d+) raider ships and (\d+) fuel pods?")]
    public void GivenAStarSystemHolding(int asteroids, int raiders, int pods)
    {
        Populate(world, asteroids, raiders, pods);
        _control = new World();
        Populate(_control, asteroids, raiders, pods);
        var state = world.Sim.System;
        _left = [.. state.Asteroids!, .. state.RaiderShips!, .. state.FuelPods!];
        _leftAt = [.. _left.Select(e => e.Position.ToGodot())];
    }

    [When("the pilot ship is destroyed there")]
    public void WhenThePilotShipIsDestroyedThere()
    {
        world.HitToZero(world.Sim.Ship);
        Assert.Equal(world.Sim.Ship.ShipClass.MaxHull, world.Sim.Ship.Hull); // it respawned
        Control.Run(world.Steps * World.StepSeconds);
    }

    [Then(@"the star system holds the same (\d+) asteroids, (\d+) raider ships and (\d+) fuel pods?")]
    public void ThenTheStarSystemHoldsTheSame(int asteroids, int raiders, int pods)
    {
        var state = world.Sim.System;
        Assert.Equal(asteroids, state.Asteroids?.Count ?? 0);
        Assert.Equal(raiders, state.RaiderShips?.Count ?? 0);
        Assert.Equal(pods, state.FuelPods?.Count ?? 0);
        Assert.Equal(_left, [.. state.Asteroids!, .. state.RaiderShips!, .. state.FuelPods!]);
        Assert.Equal(_leftAt.Take(asteroids), state.Asteroids!.Select(a => a.Position.ToGodot()));
        Assert.Equal(_leftAt.Skip(asteroids + raiders), state.FuelPods!.Select(p => p.Position.ToGodot()));
    }

    [Then("each raider ship keeps its hull, its position and its velocity")]
    public void ThenEachRaiderShipKeepsItsHullPositionAndVelocity()
    {
        var flownAlone = Control.Sim.System.RaiderShips!;
        Assert.Equal(flownAlone.Select(r => (r.Hull, Pose.Of(r).Position, Pose.Of(r).Velocity)),
            world.Sim.System.RaiderShips!.Select(r => (r.Hull, Pose.Of(r).Position, Pose.Of(r).Velocity)));
    }
}
