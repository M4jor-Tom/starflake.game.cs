using Godot;
using Reqnroll;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class SessionSteps(World world)
{
    StarSystem? _system;
    AsteroidField? _field;
    RaiderPack? _pack;
    List<(Asteroid Asteroid, Vector3 Position)> _asteroidsLeft = [];
    List<(FuelPod Pod, Vector3 Position)> _podsLeft = [];
    RaiderShip? _raider;
    (float Hull, Pose Pose)? _raiderWhenLeft;

    StarSystem System => _system ?? throw new InvalidOperationException("no star system given");

    // The session ids a scenario quotes, resolved in this world's spec.
    object SessionThing(string path) => path switch
    {
        "session.starting_ship_class" => world.Spec.Session.StartingShipClass,
        "session.starting_system" => world.Spec.Session.StartingSystem,
        _ => throw new ArgumentException($"no session id at '{path}'"),
    };

    [Given(@"the ""session\.starting_ship_class"" has a max hull of ([\d.]+)")]
    public void GivenTheStartingShipClassHasAMaxHullOf(float hull) => world.Spec.Session.StartingShipClass.MaxHull = hull;

    [Then(@"the pilot ship is of the ""([a-z_.]+)""")]
    public void ThenThePilotShipIsOfThe(string path) => Assert.Same(SessionThing(path), world.Sim.Ship.ShipClass);

    [Then(@"the pilot ship is in the ""([a-z_.]+)""")]
    public void ThenThePilotShipIsInThe(string path) => Assert.Same(SessionThing(path), world.Sim.Ship.CurrentSystem);

    [Then(@"the pilot ship has ([\d.]+) hull and ([\d.]+) fuel")]
    public void ThenThePilotShipHasHullAndFuel(float hull, float fuel)
    {
        Assert.Equal(hull, world.Sim.Ship.Hull);
        Assert.Equal(fuel, world.Sim.Ship.Fuel);
    }

    [Then("the pilot ship is at rest at the arrival point of that star system, facing the star")]
    public void ThenThePilotShipIsAtRestAtTheArrivalPointFacingTheStar()
    {
        var ship = Pose.Of(world.Sim.Ship);
        Assert.Equal(Vector3.Zero, ship.Velocity);
        Assert.Equal(Rules.ArrivalPoint(world.Sim.Ship.CurrentSystem, world.Spec.Tuning).ToGodot(), ship.Position);
        Assert.InRange(ship.Nose.Dot(-ship.Position.Normalized()), 1 - World.Tolerance, 1 + World.Tolerance);
    }

    [Given(@"a star system with a bounds radius of ([\d.]+) metres")]
    public void GivenAStarSystemWithABoundsRadiusOf(float radius) => _system = world.Galaxy.System("arrival", radius);

    [Then(@"its arrival point is on its \+Z axis, at the ""([a-z_.]+)"" of the bounds radius from the star")]
    public void ThenItsArrivalPointIsOnItsZAxisAtTheFractionOfTheBoundsRadius(string path)
    {
        var point = Rules.ArrivalPoint(System, world.Spec.Tuning);
        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
        Assert.Equal(world.Spec.Number(path) * System.BoundsRadius, point.Z, World.Tolerance);
    }

    // Starting the session is how the pilot ship first enters the starting system.
    [Given("a star system that no ship has visited")]
    public void GivenAStarSystemThatNoShipHasVisited() => _system = world.Galaxy.StartingSystem;

    [Given(@"it has an asteroid field with a count of (\d+)")]
    public void GivenItHasAnAsteroidFieldWithACountOf(int count) => _field = world.Galaxy.AsteroidField(System, count);

    [Given(@"it has a raider pack with a count of (\d+)")]
    public void GivenItHasARaiderPackWithACountOf(int count) => _pack = world.Galaxy.RaiderPack(System, count);

    [When("the pilot ship enters the star system")]
    public void WhenThePilotShipEntersTheStarSystem() => _ = world.Sim;

    [Then(@"the star system holds (\d+) asteroids of the field's asteroid type, each at its max integrity")]
    public void ThenTheStarSystemHoldsAsteroidsOfTheFieldsType(int count)
    {
        var asteroids = world.Sim.System.Asteroids ?? [];
        Assert.Equal(count, asteroids.Count);
        Assert.All(asteroids, a => Assert.Same(_field!.AsteroidType, a.AsteroidType));
        Assert.All(asteroids, a => Assert.Equal(a.AsteroidType.MaxIntegrity, a.Integrity));
    }

    [Then(@"the star system holds (\d+) raider ships of the pack's raider type, each at the max hull of its ship class")]
    public void ThenTheStarSystemHoldsRaiderShipsOfThePacksType(int count)
    {
        var raiders = world.Sim.System.RaiderShips ?? [];
        Assert.Equal(count, raiders.Count);
        Assert.All(raiders, r => Assert.Same(_pack!.RaiderType, r.RaiderType));
        Assert.All(raiders, r => Assert.Equal(r.RaiderType.ShipClass.MaxHull, r.Hull));
    }

    [Then("each of them is within the radius of its region, measured from the region's center")]
    public void ThenEachOfThemIsWithinTheRadiusOfItsRegion()
    {
        var state = world.Sim.System;
        Assert.All(state.Asteroids ?? [], a => AssertWithin(_field!, a));
        Assert.All(state.RaiderShips ?? [], r => AssertWithin(_pack!, r));
    }

    static void AssertWithin(Region region, SpaceEntity entity) =>
        Assert.InRange(entity.Position.ToGodot().DistanceTo(region.Center.ToGodot()), 0, region.Radius);

    [Given(@"the pilot ship has broken (\d+) of the (\d+) asteroids of a star system")]
    public void GivenThePilotShipHasBrokenAsteroids(int broken, int count)
    {
        _system = world.Galaxy.StartingSystem;
        _field = world.Galaxy.AsteroidField(System, count);
        ArrangeRoundTrip();
        var asteroids = world.Sim.System.Asteroids!;
        asteroids.RemoveRange(0, broken);
        _asteroidsLeft = [.. asteroids.Select(a => (a, a.Position.ToGodot()))];
    }

    [Given(@"(\d+) fuel pods? (?:is|are) left floating there")]
    public void GivenFuelPodsAreLeftFloatingThere(int count)
    {
        var pods = world.Sim.System.FuelPods ??= [];
        for (var i = 0; i < count; i++)
            pods.Add(new FuelPod { Id = $"pod-{i}", FuelAmount = 10, Position = new Vector3D { X = 100 * i, Y = 0, Z = -500 } });
        _podsLeft = [.. pods.Select(p => (p, p.Position.ToGodot()))];
    }

    [Given(@"a raider ship that has lost ([\d.]+) of its hull and is moving")]
    public void GivenARaiderShipThatHasLostHullAndIsMoving(float lost)
    {
        _system = world.Galaxy.StartingSystem;
        world.Galaxy.RaiderPack(System, 1);
        ArrangeRoundTrip();
        _raider = world.Sim.System.RaiderShips![0];
        _raider.Hull -= lost;
        _raider.Velocity = new Vector3D { X = 40, Y = 0, Z = 30 };
    }

    // A neighbouring star system linked both ways, and fuel for the two jumps.
    void ArrangeRoundTrip()
    {
        GalaxyBuilder.Link(System, world.Galaxy.System("elsewhere"));
        world.Sim.Ship.Fuel = world.Sim.Ship.ShipClass.FuelCapacity;
    }

    [When("the pilot ship jumps away and comes back")]
    public void WhenThePilotShipJumpsAwayAndComesBack()
    {
        var home = world.Sim.Ship.CurrentSystem;
        if (_raider is not null)
            _raiderWhenLeft = (_raider.Hull, Pose.Of(_raider));
        world.Sim.Press(Actions.Jump);
        Assert.True(home != world.Sim.Ship.CurrentSystem, "the pilot ship did not jump away");
        world.Sim.Press(Actions.Jump);
        Assert.Same(home, world.Sim.Ship.CurrentSystem);
    }

    [Then(@"the star system holds the same (\d+) asteroids, where they were")]
    public void ThenTheStarSystemHoldsTheSameAsteroidsWhereTheyWere(int count)
    {
        var asteroids = world.Sim.System.Asteroids ?? [];
        Assert.Equal(count, asteroids.Count);
        Assert.Equal(_asteroidsLeft.Select(a => a.Asteroid), asteroids);
        Assert.Equal(_asteroidsLeft.Select(a => a.Position), asteroids.Select(a => a.Position.ToGodot()));
    }

    [Then("the same fuel pod, where it was")]
    public void ThenTheSameFuelPodWhereItWas()
    {
        var pods = world.Sim.System.FuelPods ?? [];
        Assert.Equal(_podsLeft.Select(p => p.Pod), pods);
        Assert.Equal(_podsLeft.Select(p => p.Position), pods.Select(p => p.Position.ToGodot()));
    }

    [Then("nothing has spawned again")]
    public void ThenNothingHasSpawnedAgain()
    {
        var state = world.Sim.System;
        Assert.Equal(_asteroidsLeft.Count, state.Asteroids?.Count ?? 0);
        Assert.Empty(state.RaiderShips ?? []);
    }

    [Then("the raider ship has the same hull, position, velocity and attitude as when the pilot ship left")]
    public void ThenTheRaiderShipIsAsWhenThePilotShipLeft()
    {
        var (hull, pose) = _raiderWhenLeft!.Value;
        Assert.Same(_raider, Assert.Single(world.Sim.System.RaiderShips ?? []));
        Assert.Equal(hull, _raider!.Hull);
        Assert.Equal(pose, Pose.Of(_raider));
    }
}
