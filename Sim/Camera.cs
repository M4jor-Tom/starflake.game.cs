using System;
using Godot;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>
/// The chase camera of spec/camera.json: behind and above the pilot ship at its offset, looking parallel to the
/// nose (camera.aim), so shots fly toward the screen centre at any range. The position trails; the direction does not.
/// </summary>
public sealed class Camera
{
    readonly CameraRig _rig;

    internal Camera(CameraRig rig) => _rig = rig;

    public Vector3 Position { get; private set; }

    public Basis Basis { get; private set; } = Basis.Identity;

    public float FovDegrees => _rig.FovDegrees;

    public float FarPlane => _rig.FarPlane;

    /// <summary>Puts the camera at once at its offset from the ship.</summary>
    internal void Snap(SpaceEntity ship) => Position = Aim(ship);

    /// <summary>Closes the gap to the offset by 1 - exp(-follow_rate * dt), whatever the step length.</summary>
    internal void Step(SpaceEntity ship, float dt) => Position = Position.Lerp(Aim(ship), 1 - MathF.Exp(-_rig.FollowRate * dt));

    // Turns the camera with the ship and returns where the offset puts it.
    Vector3 Aim(SpaceEntity ship)
    {
        var frame = new Transform3D(new Basis(ship.Orientation?.ToGodot() ?? Quaternion.Identity), ship.Position.ToGodot());
        Basis = frame.Basis;
        return frame * _rig.Offset.ToGodot();
    }
}
