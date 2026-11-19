using System;
using Godot;
using Starflake.Domain;

namespace Starflake.Game;

public partial class AsteroidNode : EntityNode, ITarget
{
    Vector3 _spinAxis = Vector3.Up;
    float _spin;

    Asteroid Model => (Asteroid)Entity;

    public float HitRadius => Model.AsteroidType.Radius;

    public override void _Ready()
    {
        var rng = new Random(Model.Id.Length * 7919 + (int)Model.Position.X);
        AddChild(Scenery.Asteroid(Model.AsteroidType, rng));
        _spinAxis = Scenery.RandomDirection(rng);
        _spin = 0.1f + 0.3f * rng.NextSingle();
    }

    public override void _PhysicsProcess(double delta) => Rotate(_spinAxis, _spin * (float)delta);

}
