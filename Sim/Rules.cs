using System;
using Godot;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>Pure game rules applied to the domain model.</summary>
public static class Rules
{
    /// <summary>Where ships appear in a star system: on its +Z axis, facing the star.</summary>
    public static Vector3D ArrivalPoint(StarSystem system, Tuning tuning) =>
        new() { X = 0, Y = 0, Z = system.BoundsRadius * tuning.Flight.ArrivalDistanceFraction };

    /// <summary>The state of a star system, spawned from its regions on the first visit.</summary>
    public static StarSystemState StateOf(GalaxyState state, StarSystem system, Random rng)
    {
        var states = state.SystemStates ??= [];
        var found = states.Find(s => s.StarSystem == system);
        if (found != null)
            return found;

        var spawned = new StarSystemState { StarSystem = system, Asteroids = [], RaiderShips = [], FuelPods = [] };
        foreach (var field in system.AsteroidFields ?? [])
            for (var i = 0; i < field.Count; i++)
                spawned.Asteroids.Add(new Asteroid
                {
                    Id = $"{system.Id}-asteroid-{spawned.Asteroids.Count}",
                    AsteroidType = field.AsteroidType,
                    Integrity = field.AsteroidType.MaxIntegrity,
                    Position = Scatter(field, rng),
                });
        foreach (var pack in system.RaiderPacks ?? [])
            for (var i = 0; i < pack.Count; i++)
                spawned.RaiderShips.Add(new RaiderShip
                {
                    Id = $"{system.Id}-raider-{spawned.RaiderShips.Count}",
                    RaiderType = pack.RaiderType,
                    Hull = pack.RaiderType.ShipClass.MaxHull,
                    Position = Scatter(pack, rng),
                });
        states.Add(spawned);
        return spawned;
    }

    static Vector3D Scatter(Region region, Random rng)
    {
        double x, y, z; // rejection sampling: uniform inside the unit sphere
        do (x, y, z) = (rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
        while (x * x + y * y + z * z > 1);
        return new Vector3D
        {
            X = region.Center.X + (float)x * region.Radius,
            Y = region.Center.Y + (float)y * region.Radius,
            Z = region.Center.Z + (float)z * region.Radius,
        };
    }

    /// <returns>True only for the hit that destroys the ship or breaks the asteroid.</returns>
    public static bool Damage(SpaceEntity entity, float amount)
    {
        switch (entity)
        {
            case Ship ship when ship.Hull > 0:
                ship.Hull = Math.Max(0, ship.Hull - amount);
                return ship.Hull == 0;
            case Asteroid asteroid when asteroid.Integrity > 0:
                asteroid.Integrity = Math.Max(0, asteroid.Integrity - amount);
                return asteroid.Integrity == 0;
            case Ship or Asteroid:
                return false; // already destroyed
            default:
                throw new ArgumentException($"{entity.Id} cannot take damage");
        }
    }

    /// <summary>Removes a destroyed asteroid or raider ship and leaves its fuel where it was.</summary>
    public static FuelPod Destroy(StarSystemState state, SpaceEntity entity)
    {
        float fuel;
        if (entity is Asteroid asteroid && state.Asteroids?.Remove(asteroid) == true)
            fuel = asteroid.AsteroidType.FuelYield;
        else if (entity is RaiderShip raider && state.RaiderShips?.Remove(raider) == true)
            fuel = raider.RaiderType.FuelYield;
        else
            throw new ArgumentException($"{entity.Id} cannot be destroyed in {state.StarSystem.Id}");

        var pod = new FuelPod { Id = $"{entity.Id}-pod", FuelAmount = fuel, Position = Vector3D.From(entity.Position.ToGodot()) };
        (state.FuelPods ??= []).Add(pod);
        return pod;
    }

    public static bool TankFull(PilotShip ship) => ship.Fuel >= ship.ShipClass.FuelCapacity;

    public static bool CanJump(PilotShip ship) => ship.Fuel >= ship.CurrentSystem.JumpFuelCost;

    /// <summary>Jumps to a jump target of the current star system, for the jump fuel cost of the system left.</summary>
    /// <returns>False, and the ship unchanged, when it holds less fuel than that cost or the target is not a jump target.</returns>
    public static bool TryJump(PilotShip ship, StarSystem target, Tuning tuning)
    {
        var from = ship.CurrentSystem;
        if (!CanJump(ship) || from.JumpTargets?.Contains(target) != true)
            return false;
        ship.Fuel -= from.JumpFuelCost;
        ship.CurrentSystem = target;
        Arrive(ship, tuning);
        return true;
    }

    /// <summary>Puts the ship at rest at the arrival point of its current star system, facing the star.</summary>
    public static void Arrive(PilotShip ship, Tuning tuning)
    {
        ship.Position = ArrivalPoint(ship.CurrentSystem, tuning);
        ship.Velocity = Vector3D.From(Vector3.Zero);
        ship.Orientation = Attitude.From(Quaternion.Identity); // the nose, -Z, points from +Z at the star
    }
}
