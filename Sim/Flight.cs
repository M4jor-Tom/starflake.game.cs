using Godot;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>Arcade flight shared by the pilot ship and raider ships.</summary>
public static class Flight
{
    /// <summary>Turns and moves the ship in place: its Orientation, Velocity and Position.</summary>
    /// <param name="turn">Pitch, yaw and roll inputs in [-1, 1].</param>
    /// <param name="throttle">-1 (reverse) to 1 (full thrust); 0 lets drag slow the ship.</param>
    public static void Step(Ship ship, ShipClass hull, Vector3 turn, float throttle, FlightTuning tuning, float dt)
    {
        var angle = hull.TurnRate * dt;
        var attitude = ((ship.Orientation?.ToGodot() ?? Quaternion.Identity)
            * new Quaternion(Vector3.Right, turn.X * angle)
            * new Quaternion(Vector3.Up, turn.Y * angle)
            * new Quaternion(Vector3.Back, turn.Z * angle)).Normalized();

        var velocity = ship.Velocity?.ToGodot() ?? Vector3.Zero;
        velocity = Mathf.IsZeroApprox(throttle)
            ? velocity.MoveToward(Vector3.Zero, hull.Acceleration * tuning.DragFraction * dt) // ponytail: arcade drag, not Newtonian
            : velocity + attitude * Vector3.Forward * throttle * hull.Acceleration * dt;
        velocity = velocity.LimitLength(hull.MaxSpeed);

        ship.Orientation = Attitude.From(attitude);
        ship.Velocity = Vector3D.From(velocity);
        ship.Position = Vector3D.From(ship.Position.ToGodot() + velocity * dt);
    }
}
