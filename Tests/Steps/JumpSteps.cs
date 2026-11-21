using Godot;
using Reqnroll;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class JumpSteps(World world)
{
    StarSystem? _target;
    (StarSystem System, float Fuel, Pose Pose, string? Message)? _before;
    float _messageTimeLeftBefore;

    StarSystem Home => world.Galaxy.StartingSystem;

    StarSystem Named(string name) => world.Galaxy.Galaxy.StarSystems.Single(s => s.Name == name);

    // Away from the arrival point, turned and moving, so an arrival or a refusal has a pose to show.
    void Displace()
    {
        var ship = world.Sim.Ship;
        ship.Position = new Vector3D { X = 310, Y = -190, Z = 870 };
        ship.Velocity = new Vector3D { X = 23, Y = 0, Z = -41 };
        ship.Orientation = Attitude.From(new Quaternion(Vector3.Up, 0.8f));
        _before = (ship.CurrentSystem, ship.Fuel, Pose.Of(ship), world.Sim.MessageId);
        _messageTimeLeftBefore = world.Sim.MessageTimeLeft;
    }

    // Two jump targets of the starting system, each with two of its own, and fuel to spare.
    void TwoTargets()
    {
        foreach (var name in new[] { "first", "second" })
        {
            var target = world.Galaxy.System(name);
            (Home.JumpTargets ??= []).Add(target);
            target.JumpTargets = [world.Galaxy.System($"{name}-onward"), Home];
        }
        world.Sim.Ship.Fuel = world.Sim.Ship.ShipClass.FuelCapacity;
    }

    [Given(@"a pilot ship in a star system whose jump targets are, in order, (\w+) and (\w+)")]
    public void GivenJumpTargetsInOrder(string first, string second) =>
        Home.JumpTargets = [world.Galaxy.System(first), world.Galaxy.System(second)];

    [Given(@"^(\w+) is the selected jump target$")]
    [Then(@"^(\w+) is the selected jump target$")]
    public void ThenIsTheSelectedJumpTarget(string name) => Assert.Same(Named(name), world.Sim.JumpTarget);

    [Given("a pilot ship whose selected jump target is not the first jump target of its star system")]
    [Given("a pilot ship whose selected jump target is the second jump target of its star system")]
    public void GivenTheSecondJumpTargetIsSelected()
    {
        TwoTargets();
        world.Sim.Press(Actions.NextTarget);
        _target = Home.JumpTargets![1];
        Assert.Same(_target, world.Sim.JumpTarget);
    }

    [When("the ship arrives in another star system")]
    public void WhenTheShipArrivesInAnotherStarSystem()
    {
        world.Sim.Press(Actions.Jump);
        Assert.Same(_target, world.Sim.Ship.CurrentSystem);
    }

    [Then("the first jump target of that star system is the selected jump target")]
    public void ThenTheFirstJumpTargetIsSelected() => Assert.Same(world.Sim.Ship.CurrentSystem.JumpTargets![0], world.Sim.JumpTarget);

    [Then("the second jump target is still the selected jump target")]
    public void ThenTheSecondJumpTargetIsStillSelected() => Assert.Same(_target, world.Sim.JumpTarget);

    [Given(@"a pilot ship holding ([\d.]+) fuel in a star system whose jump fuel cost is ([\d.]+)")]
    public void GivenAPilotShipHoldingFuelInAStarSystemWhoseJumpFuelCostIs(float fuel, float cost)
    {
        Home.JumpFuelCost = cost;
        _target = world.Galaxy.System("neighbour");
        GalaxyBuilder.Link(Home, _target);
        world.Sim.Ship.Fuel = fuel;
        Displace();
    }

    [Given("a neighbouring star system is the selected jump target")]
    public void GivenANeighbouringStarSystemIsSelected() => Assert.Same(_target, world.Sim.JumpTarget);

    [Given(@"the selected jump target has a jump fuel cost of ([\d.]+)")]
    public void GivenTheSelectedJumpTargetHasAJumpFuelCostOf(float cost) => world.Sim.JumpTarget!.JumpFuelCost = cost;

    [Then("the ship is in the target star system")]
    public void ThenTheShipIsInTheTargetStarSystem() => Assert.Same(_target, world.Sim.Ship.CurrentSystem);

    [Then("the ship is in the same star system, where it was")]
    public void ThenTheShipIsInTheSameStarSystemWhereItWas()
    {
        var (system, _, pose, _) = _before!.Value;
        Assert.Same(system, world.Sim.Ship.CurrentSystem);
        Assert.Equal(pose, Pose.Of(world.Sim.Ship));
    }

    [Given("a pilot ship in a star system that has no jump targets")]
    public void GivenAStarSystemWithoutJumpTargets()
    {
        Assert.Empty(Home.JumpTargets ?? []);
        world.Sim.Ship.Fuel = world.Sim.Ship.ShipClass.FuelCapacity; // enough to jump, were there a target
        Displace();
    }

    [Then("nothing happens")]
    public void ThenNothingHappens()
    {
        var (system, fuel, pose, message) = _before!.Value;
        var ship = world.Sim.Ship;
        Assert.Equal((system, fuel, pose, message), (ship.CurrentSystem, ship.Fuel, Pose.Of(ship), world.Sim.MessageId));
        Assert.Null(world.Sim.JumpTarget);
    }

    [Then("nothing happens and the HUD flashes no message")]
    public void ThenNothingHappensAndTheHudFlashesNoMessage()
    {
        ThenNothingHappens(); // the same message as before, which a new flash would have replaced
        Assert.Equal(_messageTimeLeftBefore, world.Sim.MessageTimeLeft); // nor restarted
    }
}
