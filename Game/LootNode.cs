using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>Floating fuel, pulled toward the player ship and collected on contact.</summary>
public partial class LootNode : EntityNode
{
    const float PickupRadius = 30, MagnetRadius = 250, MagnetSpeed = 220;

    LootDrop Model => (LootDrop)Entity;

    public override void _Ready() => AddChild(Scenery.Loot());

    public override void _PhysicsProcess(double delta)
    {
        Rotate(Vector3.Up, 2 * (float)delta);
        var player = View.Player;
        if (Rules.TankFull(player.Model))
            return;

        var toPlayer = player.Position - Position;
        var distance = toPlayer.Length();
        if (distance < PickupRadius && Rules.Collect(player.Model, View.State, Model))
            QueueFree();
        else if (distance < MagnetRadius)
            Position += toPlayer.Normalized() * Mathf.Min(distance, MagnetSpeed * (float)delta);
    }
}
