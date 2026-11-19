using System.Collections.Generic;
using System.Linq;
using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>Something projectiles can hit.</summary>
public interface ITarget
{
    Vector3 GlobalPosition { get; }

    float HitRadius { get; }

    void Hit(float damage);
}

/// <summary>Fires every weapon of a ship class, each on its own cooldown.</summary>
public sealed class Guns
{
    readonly WeaponType[] _weapons;
    readonly float[] _cooldowns;
    readonly bool _byPlayer;

    public Guns(List<WeaponType>? weapons, bool byPlayer)
    {
        _weapons = weapons?.ToArray() ?? [];
        _cooldowns = new float[_weapons.Length];
        _byPlayer = byPlayer;
        Range = _weapons.Length == 0 ? 0 : _weapons.Max(w => w.MaxRange);
    }

    public float Range { get; }

    public void Update(float dt, bool firing, StarSystemView view, Node3D shooter, Vector3 shooterVelocity)
    {
        for (var i = 0; i < _weapons.Length; i++)
        {
            _cooldowns[i] -= dt;
            if (!firing || _cooldowns[i] > 0)
                continue;
            _cooldowns[i] = 1 / _weapons[i].FireRate;
            view.AddChild(new ProjectileNode
            {
                Weapon = _weapons[i],
                View = view,
                ByPlayer = _byPlayer,
                Position = shooter.Position + -shooter.Basis.Z * (Scenery.ShipRadius + 2),
                Velocity = shooterVelocity + -shooter.Basis.Z * _weapons[i].ProjectileSpeed,
            });
        }
    }
}
