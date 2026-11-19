using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>Idles until the player comes within detection range, then chases and shoots.</summary>
public partial class EnemyShipNode : EntityNode, ITarget
{
    const float KeepDistance = 250;
    const float AimCone = 0.15f; // radians
    Guns _guns = null!;

    EnemyShip Model => (EnemyShip)Entity;

    ShipClass Hull => Model.EnemyType.ShipClass;

    public float HitRadius => Scenery.ShipRadius;

    public override void _Ready()
    {
        AddChild(Scenery.Ship(player: false));
        AddChild(Scenery.EnemyMarker());
        _guns = new Guns(Hull.Weapons, byPlayer: false);
    }

    public override void _PhysicsProcess(double delta)
    {
        var toPlayer = View.Player.Position - Position;
        var distance = toPlayer.Length();
        var engaged = distance < Model.EnemyType.DetectionRange;

        var turn = engaged ? SteerToward(toPlayer) : Vector3.Zero;
        var throttle = engaged && distance > KeepDistance ? 1 : 0;
        Velocity = Flight.Step(this, Velocity, Hull, turn, throttle, (float)delta);

        var aimed = engaged && distance < _guns.Range && (-Basis.Z).AngleTo(toPlayer) < AimCone;
        _guns.Update((float)delta, aimed, View, this, Velocity);
    }

    /// <summary>Pitch and yaw inputs that turn the nose toward a world direction.</summary>
    Vector3 SteerToward(Vector3 direction)
    {
        var local = Basis.Inverse() * direction.Normalized();
        if (local.Z > 0) // behind: turn hard toward the nearer side
            return new Vector3(0, local.X >= 0 ? -1 : 1, 0);
        return new Vector3(Mathf.Clamp(local.Y * 4, -1, 1), Mathf.Clamp(-local.X * 4, -1, 1), 0);
    }

}
