using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>Arcade flight shared by player and AI ships.</summary>
public static class Flight
{
    /// <param name="turn">Pitch, yaw and roll inputs in [-1, 1].</param>
    /// <param name="throttle">-1 (reverse) to 1 (full thrust); 0 lets drag slow the ship.</param>
    /// <returns>The new velocity; the body has already moved by it.</returns>
    public static Vector3 Step(Node3D body, Vector3 velocity, ShipClass hull, Vector3 turn, float throttle, float dt)
    {
        var angle = hull.TurnRate * dt;
        body.RotateObjectLocal(Vector3.Right, turn.X * angle);
        body.RotateObjectLocal(Vector3.Up, turn.Y * angle);
        body.RotateObjectLocal(Vector3.Back, turn.Z * angle);
        body.Basis = body.Basis.Orthonormalized();

        velocity = throttle != 0
            ? velocity + -body.Basis.Z * throttle * hull.Acceleration * dt
            : velocity.MoveToward(Vector3.Zero, hull.Acceleration * 0.5f * dt); // ponytail: arcade drag, not Newtonian
        velocity = velocity.LimitLength(hull.MaxSpeed);
        body.Position += velocity * dt;
        return velocity;
    }
}
