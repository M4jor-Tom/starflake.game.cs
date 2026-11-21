using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Game;

/// <summary>The star system the pilot ship is in: its star and one node per ship, asteroid, fuel pod and projectile.</summary>
public partial class StarSystemView : Node3D
{
    readonly Dictionary<object, Node3D> _nodes = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<Node3D, Vector3> _spins = []; // cosmetic tumbles, by the mesh that turns
    StarSystemState? _shown;

    public void Refresh(Simulation sim)
    {
        if (sim.System != _shown)
            Rebuild(sim.System);
        var entities = Entities(sim).ToList();
        foreach (var gone in _nodes.Keys.Except(entities, ReferenceEqualityComparer.Instance).ToList())
            Drop(gone);
        foreach (var entity in entities)
            Place(_nodes.TryGetValue(entity, out var node) ? node : Add(entity), entity);
        if (sim.OpenMenu != null)
            return; // paused: nothing turns
        var dt = (float)GetPhysicsProcessDeltaTime();
        foreach (var (mesh, spin) in _spins)
            mesh.Rotate(spin.Normalized(), spin.Length() * dt);
    }

    static IEnumerable<object> Entities(Simulation sim) =>
        new object[] { sim.Ship }
            .Concat(sim.System.Asteroids ?? [])
            .Concat(sim.System.RaiderShips ?? [])
            .Concat(sim.System.FuelPods ?? [])
            .Concat(sim.Projectiles);

    void Rebuild(StarSystemState system)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        _nodes.Clear();
        _spins.Clear();
        AddChild(Scenery.Star(system.StarSystem));
        _shown = system;
    }

    Node3D Add(object entity)
    {
        var node = new Node3D();
        switch (entity)
        {
            case PilotShip:
                node.AddChild(Scenery.Ship(pilot: true));
                break;
            case RaiderShip:
                node.AddChild(Scenery.Ship(pilot: false));
                node.AddChild(Scenery.RaiderMarker());
                break;
            case Asteroid asteroid:
                var rng = new Random(StableHash(asteroid.Id)); // the same shape and tumble on every visit
                node.AddChild(Spinning(Scenery.Asteroid(asteroid.AsteroidType, rng), Scenery.AsteroidSpin(rng)));
                break;
            case FuelPod:
                node.AddChild(Spinning(Scenery.FuelPod(), Scenery.FuelPodSpin));
                break;
            case Projectile projectile:
                node.AddChild(Scenery.Bolt(projectile.ByPilot));
                break;
        }
        AddChild(node);
        _nodes[entity] = node;
        return node;
    }

    Node3D Spinning(Node3D mesh, Vector3 spin)
    {
        _spins[mesh] = spin;
        return mesh;
    }

    // string.GetHashCode differs between runs; this does not.
    static int StableHash(string id) => id.Aggregate(17, (hash, c) => unchecked(hash * 31 + c));

    // A projectile gone short of its weapon's range hit something: it leaves a spark where it ended.
    void Drop(object entity)
    {
        var node = _nodes[entity];
        _nodes.Remove(entity);
        foreach (var mesh in node.GetChildren().OfType<Node3D>())
            _spins.Remove(mesh);
        node.QueueFree();
        if (entity is Projectile p && p.Origin.DistanceTo(p.Position) <= p.Weapon.MaxRange)
            Scenery.Spark(this, p.Position);
    }

    static void Place(Node3D node, object entity)
    {
        switch (entity)
        {
            case SpaceEntity e:
                node.Position = e.Position.ToGodot();
                node.Quaternion = e.Orientation?.ToGodot() ?? Quaternion.Identity;
                break;
            case Projectile p:
                node.Position = p.Position;
                if (p.Velocity != Vector3.Zero) // a bolt at rest keeps the way it faces
                    node.Quaternion = new Quaternion(Vector3.Forward, p.Velocity.Normalized());
                break;
        }
    }
}
