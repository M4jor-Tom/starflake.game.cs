using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>
/// Shows one <see cref="SpaceEntity"/> of the loaded system. The node owns the
/// transform while in the tree and writes it back to the model when it leaves,
/// so a system looks the same when the player returns.
/// </summary>
public partial class EntityNode : Node3D
{
    public SpaceEntity Entity = null!;
    public StarSystemView View = null!;
    public Vector3 Velocity;

    public override void _EnterTree()
    {
        Position = Entity.Position.ToGodot();
        Velocity = Entity.Velocity?.ToGodot() ?? Vector3.Zero;
        if (Entity.Orientation != null)
            Quaternion = Entity.Orientation.ToGodot();
    }

    /// <summary>Damages the entity and replaces it with its loot when destroyed (asteroids, enemies).</summary>
    public void Hit(float damage)
    {
        if (Rules.Damage(Entity, damage))
            View.Destroy(this);
    }

    public override void _ExitTree()
    {
        Entity.Position = Vector3D.From(Position);
        Entity.Velocity = Vector3D.From(Velocity);
        Entity.Orientation = Attitude.From(Quaternion);
    }
}
