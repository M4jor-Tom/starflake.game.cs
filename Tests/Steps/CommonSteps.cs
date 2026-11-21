using Godot;
using Reqnroll;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

/// <summary>Phrases shared by several features.</summary>
[Binding]
public sealed class CommonSteps(World world)
{
    [When("a solo session starts")]
    public void WhenASoloSessionStarts() => _ = world.Sim; // the first use starts it

    [When(@"the player holds ""([a-z_]+)""")]
    public void WhenThePlayerHolds(string actionId)
    {
        world.Hold(actionId);
        world.Run(1);
    }

    [Then("^the (?:pilot )?ship is at rest at the arrival point of that star system, facing the star$")]
    public void ThenThePilotShipIsAtRestAtTheArrivalPointFacingTheStar()
    {
        var ship = Pose.Of(world.Sim.Ship);
        Assert.Equal(Vector3.Zero, ship.Velocity);
        Assert.Equal(Rules.ArrivalPoint(world.Sim.Ship.CurrentSystem, world.Spec.Tuning).ToGodot(), ship.Position);
        Assert.InRange(ship.Nose.Dot(-ship.Position.Normalized()), 1 - World.Tolerance, 1 + World.Tolerance);
    }

    [Then(@"the HUD flashes ""([a-z_]+)""")]
    public void ThenTheHudFlashes(string messageId) => Assert.Equal(messageId, world.Sim.MessageId);
}
