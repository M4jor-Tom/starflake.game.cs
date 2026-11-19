using System.Collections.Generic;
using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>The loaded star system: its star and one node per entity of its state.</summary>
public partial class StarSystemView : Node3D
{
    public StarSystemState State = null!;
    public PlayerShipNode Player = null!;

    /// <summary>What player bolts can hit: the asteroids and enemies still standing.</summary>
    public readonly List<ITarget> Targets = [];

    public override void _Ready()
    {
        AddChild(Scenery.Star(State.StarSystem));
        foreach (var asteroid in State.Asteroids!)
            AddTarget(new AsteroidNode { Entity = asteroid, View = this });
        foreach (var enemy in State.EnemyShips!)
            AddTarget(new EnemyShipNode { Entity = enemy, View = this });
        foreach (var loot in State.LootDrops!)
            AddChild(new LootNode { Entity = loot, View = this });
    }

    /// <summary>Replaces a destroyed asteroid or enemy with its fuel drop.</summary>
    public void Destroy(EntityNode node)
    {
        var loot = Rules.Destroy(State, node.Entity, Vector3D.From(node.Position));
        Targets.Remove((ITarget)node);
        node.QueueFree();
        AddChild(new LootNode { Entity = loot, View = this });
    }

    void AddTarget<T>(T node) where T : EntityNode, ITarget
    {
        Targets.Add(node);
        AddChild(node);
    }
}
