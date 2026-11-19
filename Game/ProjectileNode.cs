using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>A bolt flying straight until it hits a target or exceeds its weapon's range.</summary>
public partial class ProjectileNode : Node3D
{
    public WeaponType Weapon = null!;
    public StarSystemView View = null!;
    public bool ByPlayer;
    public Vector3 Velocity;
    Vector3 _origin;

    public override void _Ready()
    {
        _origin = Position;
        Basis = Basis.LookingAt(Velocity.Normalized());
        AddChild(Scenery.Bolt(ByPlayer));
    }

    public override void _PhysicsProcess(double delta)
    {
        var from = Position;
        Position += Velocity * (float)delta;
        if (ByPlayer)
        {
            foreach (var target in View.Targets)
                if (TryHit(from, target))
                    return; // a hit may remove the target: stop iterating at once
        }
        else if (TryHit(from, View.Player))
            return;
        if (_origin.DistanceTo(Position) > Weapon.MaxRange)
            QueueFree();
    }

    bool TryHit(Vector3 from, ITarget target)
    {
        if (Geometry3D.SegmentIntersectsSphere(from, Position, target.GlobalPosition, target.HitRadius).Length == 0)
            return false;
        target.Hit(Weapon.Damage);
        Scenery.Spark(View, Position);
        QueueFree();
        return true;
    }
}
