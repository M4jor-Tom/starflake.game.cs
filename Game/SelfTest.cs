using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>
/// The core loop on real content, without rendering:
/// <c>godot --headless -- --self-test</c> exits 1 on the first failed check.
/// </summary>
public static class SelfTest
{
    public static void Run(ContentPack content)
    {
        var session = Rules.NewSoloSession(content, "Test");
        var ship = session.PlayerShips![0];
        var sol = content.Galaxy.StartingSystem;
        var state = Rules.StateOf(session, sol, new Random(1));
        Check(state.Asteroids!.Count == sol.AsteroidFields!.Sum(f => f.SpawnCount)
              && state.EnemyShips!.Count == sol.EnemyGroups!.Sum(g => g.SpawnCount),
            "the starting system spawns its asteroid fields and enemy groups");
        Check(!Rules.CanJump(ship), "a new ship has no fuel to jump");

        var first = state.Asteroids[0];
        Check(!Rules.Damage(first, first.Integrity - 1) && Rules.Damage(first, 1) && !Rules.Damage(first, 1),
            "only the hit reaching zero integrity breaks an asteroid");
        foreach (var asteroid in state.Asteroids.ToList())
        {
            Rules.Damage(asteroid, asteroid.Integrity);
            Rules.Destroy(state, asteroid, asteroid.Position);
        }
        foreach (var loot in state.LootDrops!.ToList())
            Rules.Collect(ship, state, loot);
        var looted = Math.Min(ship.ShipClass.FuelCapacity, sol.AsteroidFields!.Sum(f => f.SpawnCount * f.AsteroidType.FuelDrop));
        Check(ship.Fuel == looted && state.LootDrops!.Count == 0, "collected loot adds its fuel, up to the tank capacity");

        var target = sol.JumpTargets![0];
        var left = ship.Fuel - sol.JumpFuelCost;
        Check(Rules.TryJump(ship, target) && ship.Fuel == left && ship.CurrentSystem == target,
            "the starting system's asteroids pay for a jump, which spends its cost and moves the ship");
        ship.Fuel = target.JumpFuelCost - 1;
        Check(!Rules.TryJump(ship, sol), "a ship short of the jump cost stays");
        Check(Rules.StateOf(session, sol, new Random(2)) == state && state.Asteroids.Count == 0,
            "a visited system keeps its state");
    }

    /// <summary>The running game, through real nodes, physics frames and input events.</summary>
    public static async Task RunScene(Main game)
    {
        var ship = game.Session.PlayerShips![0];
        var asteroid = game.View.State.Asteroids![0];
        var integrity = asteroid.Integrity;
        var weapon = ship.ShipClass.Weapons![0];
        game.View.AddChild(new ProjectileNode
        {
            Weapon = weapon, View = game.View, ByPlayer = true,
            Position = asteroid.Position.ToGodot() + new Vector3(0, 0, 60),
            Velocity = new Vector3(0, 0, -weapon.ProjectileSpeed),
        });
        await Frames(game, 10);
        Check(asteroid.Integrity == integrity - weapon.Damage, "a bolt damages the asteroid in its path");

        var target = ship.CurrentSystem.JumpTargets![0];
        ship.Fuel = ship.CurrentSystem.JumpFuelCost;
        Input.ParseInputEvent(new InputEventAction { Action = Controls.Jump, Pressed = true });
        Input.FlushBufferedEvents(); // headless display servers never flush on their own
        await Frames(game, 3);
        Check(ship.CurrentSystem == target && game.View.State.StarSystem == target && ship.Fuel == 0,
            "pressing J with enough fuel loads the target system");
        Check(game.View.GetChildren().OfType<AsteroidNode>().Count() == game.View.State.Asteroids!.Count,
            "the new system shows one node per asteroid");
    }

    static async Task Frames(Node node, int count)
    {
        for (var i = 0; i < count; i++)
            await node.ToSignal(node.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    static void Check(bool ok, string what)
    {
        if (!ok)
            throw new Exception($"self-test failed: {what}");
        GD.Print($"ok: {what}");
    }
}
