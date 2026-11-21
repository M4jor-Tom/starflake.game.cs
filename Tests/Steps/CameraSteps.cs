using Godot;
using Reqnroll;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class CameraSteps(World world)
{
    Transform3D ShipFrame
    {
        get
        {
            var ship = Pose.Of(world.Sim.Ship);
            return new Transform3D(new Basis(ship.Attitude), ship.Position);
        }
    }

    // Where the camera.offset puts the camera now, in the ship's own frame.
    Vector3 OffsetPosition => ShipFrame * new Vector3(
        world.Spec.Number("camera.offset.x"), world.Spec.Number("camera.offset.y"), world.Spec.Number("camera.offset.z"));

    static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(expected.DistanceTo(actual) < World.Tolerance, $"expected {expected}, got {actual}");

    // Spec call 1: steadily means at rest, after enough steps for the follow to settle.
    // Away from the arrival point and turned about every axis, so the offset has a frame to be in.
    [Given("a pilot ship flying steadily")]
    public void GivenAPilotShipFlyingSteadily()
    {
        var ship = world.Sim.Ship;
        ship.Position = new Vector3D { X = 400, Y = -300, Z = 700 };
        ship.Velocity = Vector3D.From(Vector3.Zero);
        ship.Orientation = Attitude.From(new Quaternion(new Vector3(0.3f, 1, -0.4f).Normalized(), 1.1f));
        world.Run(5);
    }

    [Then(@"the camera is at the ""camera\.offset"" in the ship's own frame")]
    [Then(@"the camera is at once at its ""camera\.offset"" position")]
    public void ThenTheCameraIsAtItsOffset() => AssertNear(OffsetPosition, world.Sim.Camera.Position);

    [Then(@"the field of view is the ""camera\.fov_degrees""")]
    public void ThenTheFieldOfViewIs() => Assert.Equal(world.Spec.Number("camera.fov_degrees"), world.Sim.Camera.FovDegrees);

    [Then(@"nothing farther than the ""camera\.far_plane"" is drawn")]
    public void ThenNothingFartherIsDrawn() => Assert.Equal(world.Spec.Number("camera.far_plane"), world.Sim.Camera.FarPlane);

    [When("the ship moves or turns")]
    public void WhenTheShipMovesOrTurns()
    {
        world.Hold(Actions.Thrust, Actions.YawLeft, Actions.PitchUp);
        world.Run(1);
    }

    // Over the last step the gap to where the offset puts the camera shrank by exp(-follow_rate * dt).
    [Then(@"the camera closes the gap to its ""camera\.offset"" position at the ""camera\.follow_rate""")]
    public void ThenTheCameraClosesTheGap()
    {
        var target = OffsetPosition;
        var gapBefore = target - world.CameraBeforeLastStep;
        Assert.True(gapBefore.Length() > 1, $"no gap to close: {gapBefore}");
        var shrink = MathF.Exp(-world.Spec.Number("camera.follow_rate") * World.StepSeconds);
        AssertNear(gapBefore * shrink, target - world.Sim.Camera.Position);
    }

    // camera.aim is parallel_to_nose: looking along the nose is looking along the aim.
    [Then(@"the camera looks along the ""camera\.aim"", with the ship's up as its up")]
    [Then("the camera's direction follows the ship's at once, without smoothing")]
    public void ThenTheCamerasDirectionFollowsTheShips()
    {
        var ship = ShipFrame.Basis;
        var camera = world.Sim.Camera.Basis;
        AssertNear(-ship.Z, -camera.Z);
        AssertNear(ship.Y, camera.Y);
    }

    // A neighbour whose smaller bounds put its arrival point elsewhere, so the camera has somewhere to snap to.
    [When("the pilot ship jumps to another star system")]
    [Scope(Feature = "Camera")]
    public void WhenThePilotShipJumpsToAnotherStarSystem()
    {
        var home = world.Galaxy.StartingSystem;
        var neighbour = world.Galaxy.System("neighbour", boundsRadius: 7300);
        GalaxyBuilder.Link(home, neighbour);
        world.Sim.Ship.Fuel = home.JumpFuelCost;
        world.Sim.Press(Actions.Jump);
        Assert.Same(neighbour, world.Sim.Ship.CurrentSystem);
    }
}
