using System;
using System.Collections.Generic;
using Godot;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>Fires every weapon of a ship's class, each on its own cooldown, kept per ship across star systems.</summary>
public sealed class Guns
{
    // ponytail: entries of destroyed raiders stay; a few floats each, prune on destroy if sessions grow long.
    readonly Dictionary<(Ship Ship, int Weapon), float> _cooldowns = [];

    /// <summary>Counts the ship's cooldowns down by dt; while firing, every ready weapon fires from the nose.</summary>
    /// <returns>The projectiles fired in this step.</returns>
    public List<Projectile> Step(Ship ship, ShipClass hull, bool firing, FlightTuning tuning, float dt)
    {
        var fired = new List<Projectile>();
        var weapons = hull.Weapons ?? [];
        for (var i = 0; i < weapons.Count; i++)
        {
            var cooldown = _cooldowns.GetValueOrDefault((ship, i));
            if (firing && cooldown <= 0)
            {
                cooldown += 1 / weapons[i].FireRate; // carries the overshoot, so the rate holds between steps
                fired.Add(Shot(ship, weapons[i], tuning));
            }
            _cooldowns[(ship, i)] = firing ? cooldown - dt : Math.Max(cooldown - dt, 0);
        }
        return fired;
    }

    static Projectile Shot(Ship ship, WeaponType weapon, FlightTuning tuning)
    {
        var nose = (ship.Orientation?.ToGodot() ?? Quaternion.Identity) * Vector3.Forward;
        var muzzle = ship.Position.ToGodot() + nose * (tuning.ShipHitRadius + tuning.MuzzleClearance);
        return new Projectile
        {
            Weapon = weapon, ByPilot = ship is PilotShip, Origin = muzzle, Position = muzzle,
            Velocity = (ship.Velocity?.ToGodot() ?? Vector3.Zero) + nose * weapon.ProjectileSpeed,
        };
    }
}
