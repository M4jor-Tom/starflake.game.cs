using System;
using System.IO;
using Godot;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>
/// The chase camera of spec/camera.json: behind and above the pilot ship at its offset, looking where camera.aim says.
/// The position trails; the direction does not.
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
    internal void Snap(SpaceEntity ship) => (Basis, Position) = Aim(ship);

    /// <summary>Turns with the ship and closes the gap to the offset by 1 - exp(-follow_rate * dt), whatever the step length.</summary>
    internal void Step(SpaceEntity ship, float dt)
    {
        (Basis, var target) = Aim(ship);
        Position = Position.Lerp(target, 1 - MathF.Exp(-_rig.FollowRate * dt));
    }

    // The basis camera.aim gives the camera, and where the offset, in the ship's frame, puts it.
    /// <exception cref="InvalidDataException">An aim not handled here, so a new one is never silently ignored.</exception>
    (Basis Basis, Vector3 Position) Aim(SpaceEntity ship)
    {
        var frame = new Basis(ship.Orientation?.ToGodot() ?? Quaternion.Identity);
        var basis = _rig.Aim switch
        {
            CameraAim.ParallelToNose => frame, // shots fly toward the screen centre at any range
            _ => throw new InvalidDataException($"camera.json: aim {_rig.Aim} is not supported"),
        };
        return (basis, ship.Position.ToGodot() + frame * _rig.Offset.ToGodot());
    }
}
