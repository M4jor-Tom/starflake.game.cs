using System;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>
/// Game rules applied to the domain model. Keep Godot types out of this file:
/// it is meant to move into a shared library when a dedicated server needs it.
/// </summary>
public static class Rules
{
    public static GameSession NewSoloSession(ContentPack content, string pilotName)
    {
        var pilot = new Player { Id = "player-1", Name = pilotName };
        var start = content.Galaxy.StartingSystem;
        var ship = new PlayerShip
        {
            Id = "ship-1",
            Pilot = pilot,
            ShipClass = content.StartingShipClass,
            CurrentSystem = start,
            Hull = content.StartingShipClass.MaxHull,
            Fuel = 0,
            Position = ArrivalPoint(start),
        };
        return new GameSession
        {
            Id = "solo",
            ContentPack = content,
            HostingMode = HostingMode.Solo,
            MaxPlayers = 1,
            Host = pilot,
            Players = [pilot],
            PlayerShips = [ship],
            SystemStates = [],
        };
    }

    /// <summary>Where ships appear in a system: on its +Z axis, facing the star.</summary>
    public static Vector3D ArrivalPoint(StarSystem system) =>
        new() { X = 0, Y = 0, Z = system.BoundsRadius * 0.1f };

    /// <summary>The state of a system, spawned from its regions on the first visit.</summary>
    public static StarSystemState StateOf(GameSession session, StarSystem system, Random rng)
    {
        var state = session.SystemStates!.Find(s => s.StarSystem == system);
        if (state != null)
            return state;

        state = new StarSystemState { StarSystem = system, Asteroids = [], EnemyShips = [], LootDrops = [] };
        foreach (var field in system.AsteroidFields ?? [])
            for (var i = 0; i < field.SpawnCount; i++)
                state.Asteroids.Add(new Asteroid
                {
                    Id = $"{system.Id}-asteroid-{state.Asteroids.Count}",
                    AsteroidType = field.AsteroidType,
                    Integrity = field.AsteroidType.MaxIntegrity,
                    Position = Scatter(field, rng),
                });
        foreach (var group in system.EnemyGroups ?? [])
            for (var i = 0; i < group.SpawnCount; i++)
                state.EnemyShips.Add(new EnemyShip
                {
                    Id = $"{system.Id}-enemy-{state.EnemyShips.Count}",
                    EnemyType = group.EnemyType,
                    Hull = group.EnemyType.ShipClass.MaxHull,
                    Position = Scatter(group, rng),
                });
        session.SystemStates.Add(state);
        return state;
    }

    static Vector3D Scatter(SpawnRegion region, Random rng)
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

    /// <summary>Removes a destroyed asteroid or enemy and leaves its fuel where it was.</summary>
    public static LootDrop Destroy(StarSystemState state, SpaceEntity entity, Vector3D at)
    {
        float fuel;
        if (entity is Asteroid asteroid && state.Asteroids!.Remove(asteroid))
            fuel = asteroid.AsteroidType.FuelDrop;
        else if (entity is EnemyShip enemy && state.EnemyShips!.Remove(enemy))
            fuel = enemy.EnemyType.FuelDrop;
        else
            throw new ArgumentException($"{entity.Id} cannot be destroyed in {state.StarSystem.Id}");

        var loot = new LootDrop { Id = $"{entity.Id}-loot", FuelAmount = fuel, Position = at };
        state.LootDrops!.Add(loot);
        return loot;
    }

    public static bool TankFull(PlayerShip ship) => ship.Fuel >= ship.ShipClass.FuelCapacity;

    /// <returns>False, leaving the loot in space, when the tank is already full.</returns>
    public static bool Collect(PlayerShip ship, StarSystemState state, LootDrop loot)
    {
        if (TankFull(ship))
            return false;
        ship.Fuel = Math.Min(ship.ShipClass.FuelCapacity, ship.Fuel + loot.FuelAmount);
        state.LootDrops!.Remove(loot);
        return true;
    }

    public static bool CanJump(PlayerShip ship) => ship.Fuel >= ship.CurrentSystem.JumpFuelCost;

    public static bool TryJump(PlayerShip ship, StarSystem target)
    {
        if (!CanJump(ship) || ship.CurrentSystem.JumpTargets?.Contains(target) != true)
            return false;
        ship.Fuel -= ship.CurrentSystem.JumpFuelCost;
        ship.CurrentSystem = target;
        ship.Position = ArrivalPoint(target);
        return true;
    }

    /// <summary>A destroyed player ship comes back repaired at the arrival point, keeping its fuel.</summary>
    public static void Respawn(PlayerShip ship)
    {
        ship.Hull = ship.ShipClass.MaxHull;
        ship.Position = ArrivalPoint(ship.CurrentSystem);
    }
}
