using Godot;
using Reqnroll;
using Starflake.Domain;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class FuelScoopSteps(World world)
{
    const float AnyFuelAmount = 7;

    FuelPod? _pod;
    Vector3 _podBefore;

    ShipClass Hull => world.Galaxy.StartingShipClass;

    FuelPod ThePod => _pod ?? throw new InvalidOperationException("no fuel pod given");

    Vector3 ShipAt => world.Sim.Ship.Position.ToGodot();

    [Given(@"a pilot ship whose ship class has a scoop radius of ([\d.]+) metres")]
    public void GivenAScoopRadius(float radius) => Hull.ScoopRadius = radius;

    [Given(@"a tractor range of ([\d.]+) metres and a tractor speed of ([\d.]+) metres per second")]
    public void GivenATractorRangeAndSpeed(float range, float speed) => (Hull.TractorRange, Hull.TractorSpeed) = (range, speed);

    [Given(@"a fuel capacity of ([\d.]+)")]
    public void GivenAFuelCapacity(float capacity) => Hull.FuelCapacity = capacity;

    [Given(@"the ship holds ([\d.]+) fuel")]
    public void GivenTheShipHoldsFuel(float fuel) => world.Sim.Ship.Fuel = fuel;

    // The pod is staged beside the ship at rest, along +X: the distance is exact, and one step follows.
    [When(@"a fuel pod is ([\d.]+) metres from the ship")]
    public void WhenAFuelPodIsMetresFromTheShip(float distance) => Drop(distance, AnyFuelAmount);

    [When(@"a fuel pod with a fuel amount of ([\d.]+) is ([\d.]+) metres from the ship")]
    public void WhenAFuelPodWithAFuelAmountIs(float amount, float distance) => Drop(distance, amount);

    [When(@"the fuel pod is exactly ([\d.]+) metres from the ship")]
    public void WhenTheFuelPodIsExactlyMetresFromTheShip(float distance)
    {
        ThePod.Position = Vector3D.From(ShipAt + Vector3.Right * distance);
        StepOnce();
    }

    void Drop(float distance, float amount)
    {
        _pod = new FuelPod { Id = "pod", FuelAmount = amount, Position = Vector3D.From(ShipAt + Vector3.Right * distance) };
        (world.Sim.System.FuelPods ??= []).Add(_pod);
        StepOnce();
    }

    void StepOnce()
    {
        _podBefore = ThePod.Position.ToGodot();
        world.Run(World.StepSeconds);
    }

    float Closed => _podBefore.DistanceTo(ShipAt) - ThePod.Position.ToGodot().DistanceTo(ShipAt);

    [Then(@"the fuel pod moves straight toward the ship at ([\d.]+) metres per second")]
    public void ThenTheFuelPodMovesStraightTowardTheShipAt(float speed)
    {
        World.AssertMeasured(speed, Closed / World.StepSeconds);
        var moved = ThePod.Position.ToGodot() - _podBefore;
        Assert.InRange(moved.Normalized().Dot((ShipAt - _podBefore).Normalized()), 1 - World.Tolerance, 1 + World.Tolerance);
    }

    [Then("the fuel pod still moves toward the ship and is not collected yet")]
    public void ThenTheFuelPodStillMovesTowardTheShip()
    {
        Assert.Contains(ThePod, world.Sim.System.FuelPods ?? []);
        World.AssertMeasured(Hull.TractorSpeed, Closed / World.StepSeconds);
    }

    [Then("^the fuel pod (?:still )?does not move$")]
    public void ThenTheFuelPodDoesNotMove() => Assert.Equal(_podBefore, ThePod.Position.ToGodot());

    [Then("the fuel pod is removed from the star system")]
    public void ThenTheFuelPodIsRemoved() => Assert.DoesNotContain(ThePod, world.Sim.System.FuelPods ?? []);

    [Then("the fuel pod stays in the star system")]
    public void ThenTheFuelPodStays() => Assert.Contains(ThePod, world.Sim.System.FuelPods ?? []);
}
