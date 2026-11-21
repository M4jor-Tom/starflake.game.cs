using Godot;
using Starflake.Sim;

namespace Starflake.Game;

/// <summary>Shows the simulation's chase camera.</summary>
public partial class ChaseCamera : Camera3D
{
    public void Refresh(Simulation sim)
    {
        var camera = sim.Camera;
        GlobalTransform = new Transform3D(camera.Basis, camera.Position);
        Fov = camera.FovDegrees;
        Far = camera.FarPlane;
    }
}
