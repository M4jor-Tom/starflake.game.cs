using Godot;
using Reqnroll;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class SystemBoundsSteps(World world)
{
    Ship? _ship;
    Pose _flownAlone;

    Ship Subject => _ship ?? throw new InvalidOperationException("no ship given");

    float Bounds => world.Sim.Ship.CurrentSystem.BoundsRadius;

    // Just inside the bounds, heading out of them and along them.
    static void PlaceAtTheEdge(Ship ship, float bounds)
    {
        ship.Position = new Vector3D { X = 0, Y = 0, Z = bounds - 1 };
        ship.Velocity = new Vector3D { X = 60, Y = 0, Z = 120 };
    }

    [Given(@"a pilot ship in a star system with a bounds radius of ([\d.]+) metres")]
    public void GivenAPilotShipInAStarSystemWithABoundsRadiusOf(float radius)
    {
        world.Galaxy.StartingSystem.BoundsRadius = radius;
        _ship = world.Sim.Ship;
        PlaceAtTheEdge(_ship, radius);
    }

    [Given(@"a raider ship in a star system with a bounds radius of ([\d.]+) metres")]
    public void GivenARaiderShipInAStarSystemWithABoundsRadiusOf(float radius)
    {
        world.Galaxy.StartingSystem.BoundsRadius = radius;
        world.Galaxy.RaiderPack(world.Galaxy.StartingSystem, 1);
        _ship = world.Sim.System.RaiderShips![0];
        PlaceAtTheEdge(_ship, radius);
    }

    [When(@"the ship would move farther than ([\d.]+) metres from the star")]
    [When(@"the raider ship moves farther than ([\d.]+) metres from the star")]
    public void WhenTheShipMovesFartherThan(float distance)
    {
        _flownAlone = FlownAlone(Subject);
        Assert.True(_flownAlone.Position.Length() > distance, "flight alone stays inside");
        world.Hold();
        world.Run(World.StepSeconds);
    }

    // Where one step of flight without input takes the ship, on a copy of it.
    Pose FlownAlone(Ship ship)
    {
        var copy = new Probe
        {
            Id = "probe", Hull = ship.Hull, Position = Vector3D.From(ship.Position.ToGodot()),
            Velocity = ship.Velocity, Orientation = ship.Orientation,
        };
        var hull = ship is RaiderShip raider ? raider.RaiderType.ShipClass : world.Sim.Ship.ShipClass;
        Flight.Step(copy, hull, Vector3.Zero, 0, world.Spec.Tuning.Flight, World.StepSeconds);
        return Pose.Of(copy);
    }

    sealed class Probe : Ship;

    [Then(@"the ship is held at ([\d.]+) metres from the star")]
    public void ThenTheShipIsHeldAt(float distance)
    {
        var position = Pose.Of(Subject).Position;
        Assert.Equal(distance, position.Length(), World.Tolerance * 10);
        Assert.Equal(1, position.Normalized().Dot(_flownAlone.Position.Normalized()), World.Tolerance);
    }

    [Then("the ship keeps only the part of its velocity that runs along the bounds")]
    public void ThenTheShipKeepsOnlyItsVelocityAlongTheBounds()
    {
        var ship = Pose.Of(Subject);
        var outward = ship.Position.Normalized();
        var along = _flownAlone.Velocity.Slide(outward);
        Assert.True(along.Length() > 0, "no velocity along the bounds to keep");
        Assert.Equal(0, ship.Velocity.Dot(outward), World.Tolerance);
        Assert.True(ship.Velocity.IsEqualApprox(along), $"{ship.Velocity} is not {along}");
    }

    [Then("the raider ship keeps its position and its velocity")]
    public void ThenTheRaiderShipKeepsItsPositionAndItsVelocity()
    {
        var raider = Pose.Of(Subject);
        Assert.True(raider.Position.Length() > Bounds, "the raider ship was held");
        Assert.Equal(_flownAlone.Position, raider.Position);
        Assert.Equal(_flownAlone.Velocity, raider.Velocity);
    }
}
