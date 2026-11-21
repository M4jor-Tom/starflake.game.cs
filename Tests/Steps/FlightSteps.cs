using Godot;
using Reqnroll;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class FlightSteps(World world)
{
    ShipClass Hull => world.Galaxy.StartingShipClass;

    void SetVelocity(Vector3 velocity) => world.Sim.Ship.Velocity = Vector3D.From(velocity);

    [Given(@"a pilot ship at rest whose ship class has an acceleration of ([\d.]+) metres per second squared")]
    public void GivenAPilotShipAtRestWithAnAccelerationOf(float acceleration)
    {
        Hull.Acceleration = acceleration;
        _ = world.Sim; // a new session starts at rest
    }

    [Given(@"a pilot ship moving at ([\d.]+) metres per second(?: along its nose)?")]
    public void GivenAPilotShipMovingAt(float speed) => SetVelocity(Pose.Of(world.Sim.Ship).Nose * speed);

    [Given(@"a pilot ship moving at its ship class max speed of ([\d.]+) metres per second")]
    public void GivenAPilotShipMovingAtItsMaxSpeedOf(float speed)
    {
        Hull.MaxSpeed = speed;
        SetVelocity(Pose.Of(world.Sim.Ship).Nose * speed);
    }

    [Given(@"a pilot ship whose ship class has a turn rate of ([\d.]+) radians per second")]
    public void GivenAPilotShipWithATurnRateOf(float rate)
    {
        Hull.TurnRate = rate;
        _ = world.Sim;
    }

    [When(@"the player holds ""([a-z_]+)"" for ([\d.]+) seconds?")]
    public void WhenThePlayerHoldsFor(string actionId, float seconds)
    {
        world.Hold(actionId);
        world.Run(seconds);
    }

    // Runs past the moment drag stops the ship, and one second longer.
    [When(@"the player holds neither ""thrust"" nor ""reverse""")]
    public void WhenThePlayerHoldsNeitherThrustNorReverse()
    {
        world.Hold();
        var drag = world.Spec.Tuning.Flight.DragFraction * Hull.Acceleration;
        world.Run(1 + Pose.Of(world.Sim.Ship).Velocity.Length() / drag + 1);
    }

    [When(@"the player holds ""([a-z_]+)"" and ""([a-z_]+)"" together")]
    public void WhenThePlayerHoldsTogether(string first, string second)
    {
        world.Hold(first, second);
        world.Run(1);
    }

    [When(@"the player turns the ship without holding ""thrust"" or ""reverse""")]
    public void WhenThePlayerTurnsTheShipWithoutThrottle()
    {
        world.Hold(Actions.PitchUp, Actions.YawLeft);
        world.Run(1);
    }

    [Then(@"the ship moves at ([\d.]+) metres per second in the direction (of|opposite to) its nose")]
    public void ThenTheShipMovesAlongItsNose(float speed, string direction)
    {
        var ship = Pose.Of(world.Sim.Ship);
        World.AssertMeasured(speed, ship.Velocity.Length());
        var along = direction == "of" ? 1 : -1;
        Assert.InRange(ship.Velocity.Normalized().Dot(ship.Nose), along - World.Tolerance, along + World.Tolerance);
    }

    [Then(@"the ship moves at ([\d.]+) metres per second")]
    public void ThenTheShipMovesAt(float speed) => World.AssertMeasured(speed, Pose.Of(world.Sim.Ship).Velocity.Length());

    [Then(@"the ship slows down at the ""([a-z_.]+)"" of its ship class acceleration")]
    public void ThenTheShipSlowsDownAtTheFractionOfItsAcceleration(string path) => AssertSpeedLostInFirstSecond(path);

    void AssertSpeedLostInFirstSecond(string path)
    {
        var lost = world.Speeds[0] - world.Speeds[World.StepsIn(1)];
        World.AssertMeasured(world.Spec.Number(path) * Hull.Acceleration, lost);
    }

    [Then("the ship stays at rest once its speed reaches 0")]
    public void ThenTheShipStaysAtRestOnceItsSpeedReaches0()
    {
        var stop = world.Speeds.FindIndex(s => s <= 0);
        Assert.InRange(stop, 1, world.Speeds.Count - 2);
        Assert.All(world.Speeds.Skip(stop), s => Assert.Equal(0f, s));
    }

    // The nose and sides after the run, seen from the ship's frame when it began.
    (Vector3 Nose, Vector3 Left) TurnedFrame()
    {
        var turn = world.Before.Attitude.Inverse() * Pose.Of(world.Sim.Ship).Attitude;
        return (turn * Vector3.Forward, turn * Vector3.Left);
    }

    [Then(@"the nose (rises|drops) at ([\d.]+) radians per second relative to the ship itself")]
    public void ThenTheNosePitchesAt(string way, float rate)
    {
        var (nose, _) = TurnedFrame();
        World.AssertMeasured(rate, Vector3.Forward.AngleTo(nose));
        Assert.Equal(way == "rises" ? 1 : -1, MathF.Sign(nose.Y));
        Assert.Equal(0, nose.X, World.Tolerance);
    }

    [Then(@"the nose swings to the ship's (left|right) at ([\d.]+) radians per second")]
    public void ThenTheNoseYawsAt(string side, float rate)
    {
        var (nose, _) = TurnedFrame();
        World.AssertMeasured(rate, Vector3.Forward.AngleTo(nose));
        Assert.Equal(side == "left" ? -1 : 1, MathF.Sign(nose.X));
        Assert.Equal(0, nose.Y, World.Tolerance);
    }

    [Then(@"the ship's (left|right) side dips at ([\d.]+) radians per second")]
    public void ThenTheShipRollsAt(string side, float rate)
    {
        var (nose, left) = TurnedFrame();
        var dipping = side == "left" ? left : -left;
        World.AssertMeasured(rate, (side == "left" ? Vector3.Left : Vector3.Right).AngleTo(dipping));
        Assert.True(dipping.Y < 0, $"the {side} side rose");
        Assert.Equal(0, Vector3.Forward.AngleTo(nose), World.Tolerance);
    }

    [Then("the ship keeps moving in its former direction while drag slows it")]
    public void ThenTheShipKeepsMovingInItsFormerDirection()
    {
        var ship = Pose.Of(world.Sim.Ship);
        Assert.True(world.Before.Nose.AngleTo(ship.Nose) > World.Tolerance, "the ship did not turn");
        Assert.Equal(1, ship.Velocity.Normalized().Dot(world.Before.Velocity.Normalized()), World.Tolerance);
        AssertSpeedLostInFirstSecond("flight.drag_fraction");
    }
}
