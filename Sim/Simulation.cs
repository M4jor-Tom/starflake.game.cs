using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>Names a physical key as the player's keyboard layout prints it; the engine knows the layout.</summary>
public interface IKeyLabels
{
    string LabelOf(PhysicalKey key);
}

/// <summary>One solo session: the galaxy state, the spec it plays by, and the pilot ship.</summary>
public sealed class Simulation
{
    const string ShipDestroyed = "ship_destroyed";

    readonly Random _rng;
    readonly Guns _guns = new();
    readonly List<Projectile> _projectiles = [];

    Simulation(GalaxyState state, Spec spec, PilotShip ship, Random rng)
    {
        State = state;
        Spec = spec;
        Ship = ship;
        _rng = rng;
        System = Enter(ship.CurrentSystem);
    }

    public GalaxyState State { get; }

    public Spec Spec { get; }

    public PilotShip Ship { get; }

    /// <summary>The state of the pilot ship's current star system.</summary>
    public StarSystemState System { get; private set; }

    /// <summary>The projectiles in flight in the current star system.</summary>
    public IReadOnlyList<Projectile> Projectiles => _projectiles;

    /// <summary>The id of the HUD message flashed last; null before any.</summary>
    public string? MessageId { get; private set; }

    /// <summary>The selected jump target; null in a star system without jump targets.</summary>
    public StarSystem? JumpTarget { get; private set; }

    /// <exception cref="InvalidDataException">The spec asks for another hosting mode, or lacks an action the game uses.</exception>
    public static Simulation Start(Galaxy galaxy, Spec spec, Random rng, IKeyLabels keys)
    {
        if (spec.Session.HostingMode != HostingMode.Solo)
            throw new InvalidDataException($"session.json: hosting_mode {spec.Session.HostingMode} is not supported, only solo");
        var missing = Actions.All.FirstOrDefault(id => !spec.Controls.Actions.Exists(a => a.Id == id));
        if (missing != null)
            throw new InvalidDataException($"controls.json: no action '{missing}'");

        var pilot = new Pilot { Id = "pilot-1", Name = "Pilot" };
        var hull = spec.Session.StartingShipClass;
        var system = spec.Session.StartingSystem;
        var ship = new PilotShip
        {
            Id = "ship-1", Pilot = pilot, ShipClass = hull, CurrentSystem = system, Hull = hull.MaxHull, Fuel = 0,
            Position = Rules.ArrivalPoint(system, spec.Tuning),
            Velocity = Vector3D.From(Vector3.Zero),
            Orientation = Attitude.From(Quaternion.Identity),
        };
        var state = new GalaxyState { Id = "solo", Galaxy = galaxy, Pilots = [pilot], PilotShips = [ship], SystemStates = [] };
        return new Simulation(state, spec, ship, rng);
    }

    StarSystemState Enter(StarSystem system)
    {
        JumpTarget = system.JumpTargets?.FirstOrDefault();
        return Rules.StateOf(State, system, _rng);
    }

    /// <summary>Advances the session by one step; each concern runs in turn.</summary>
    public void Step(float dt, PilotInput input)
    {
        foreach (var shot in _guns.Step(Ship, Ship.ShipClass, input.IsHeld(Actions.Fire), Spec.Tuning.Flight, dt))
            Spawn(shot); // from where the ship is when the step begins
        FlyPilotShip(dt, input);
        FlyRaiderShips(dt);
        MoveProjectiles(dt);
    }

    /// <summary>Puts a fired projectile in flight in the current star system.</summary>
    public void Spawn(Projectile projectile) => _projectiles.Add(projectile);

    /// <summary>Acts on a press action of the spec. An action without an effect yet does nothing.</summary>
    /// <exception cref="ArgumentException">The spec has no such action.</exception>
    public void Press(string actionId)
    {
        if (!Spec.Controls.Actions.Exists(a => a.Id == actionId))
            throw new ArgumentException($"no action '{actionId}'", nameof(actionId));
        switch (actionId)
        {
            // next_target and jump come with the jump drive, pause with the pause menu.
            default:
                return;
        }
    }

    /// <summary>Acts on a button of the open menu; without an open menu there is nothing to choose.</summary>
    /// <exception cref="ArgumentException">No menu of the spec has such a button.</exception>
    public void Choose(string buttonId)
    {
        if (!Spec.Menus.Menus.Exists(m => m.Buttons.Exists(b => b.Id == buttonId)))
            throw new ArgumentException($"no menu button '{buttonId}'", nameof(buttonId));
    }

    void FlyPilotShip(float dt, PilotInput input)
    {
        var turn = new Vector3(
            input.Axis(Actions.PitchDown, Actions.PitchUp),
            input.Axis(Actions.YawRight, Actions.YawLeft),
            input.Axis(Actions.RollRight, Actions.RollLeft));
        Flight.Step(Ship, Ship.ShipClass, turn, input.Axis(Actions.Reverse, Actions.Thrust), Spec.Tuning.Flight, dt);
        HoldInBounds(Ship, Ship.CurrentSystem.BoundsRadius);
    }

    // The star system is a sphere: the pilot ship slides along its edge instead of leaving it.
    static void HoldInBounds(Ship ship, float bounds)
    {
        var position = ship.Position.ToGodot();
        if (position.Length() <= bounds)
            return;
        position = position.LimitLength(bounds);
        ship.Position = Vector3D.From(position);
        ship.Velocity = Vector3D.From((ship.Velocity?.ToGodot() ?? Vector3.Zero).Slide(position.Normalized()));
    }

    void FlyRaiderShips(float dt)
    {
        foreach (var raider in System.RaiderShips ?? [])
            Flight.Step(raider, raider.RaiderType.ShipClass, Vector3.Zero, 0, Spec.Tuning.Flight, dt);
    }

    // Each projectile sweeps its path of this step, so a fast one cannot pass through a thin target.
    void MoveProjectiles(float dt)
    {
        foreach (var projectile in _projectiles.ToList()) // a hit removes projectiles and targets mid-loop
        {
            var from = projectile.Position;
            projectile.Position += projectile.Velocity * dt;
            var hit = TargetsOf(projectile).FirstOrDefault(t =>
                Projectile.SegmentHitsSphere(from, projectile.Position, t.Entity.Position.ToGodot(), t.Radius)).Entity;
            if (hit != null)
            {
                _projectiles.Remove(projectile);
                Hit(hit, projectile.Weapon.Damage);
            }
            // ponytail: a projectile with zero net velocity never leaves its range and never expires; expire on a lifetime if that matters.
            else if (projectile.Origin.DistanceTo(projectile.Position) > projectile.Weapon.MaxRange)
                _projectiles.Remove(projectile);
        }
    }

    // Pilot projectiles hit asteroids and raider ships, raider projectiles only the pilot ship.
    IEnumerable<(SpaceEntity Entity, float Radius)> TargetsOf(Projectile projectile)
    {
        var shipRadius = Spec.Tuning.Flight.ShipHitRadius;
        if (!projectile.ByPilot)
            return [(Ship, shipRadius)];
        return (System.Asteroids ?? []).Select(a => ((SpaceEntity)a, a.AsteroidType.Radius))
            .Concat((System.RaiderShips ?? []).Select(r => ((SpaceEntity)r, shipRadius)));
    }

    void Hit(SpaceEntity entity, float damage)
    {
        if (!Rules.Damage(entity, damage))
            return;
        if (entity == Ship)
            Respawn();
        else
            Rules.Destroy(System, entity);
    }

    // Repaired, at rest at the arrival point of the same star system; fuel and the star system are kept.
    void Respawn()
    {
        Ship.Hull = Ship.ShipClass.MaxHull;
        Ship.Position = Rules.ArrivalPoint(Ship.CurrentSystem, Spec.Tuning);
        Ship.Velocity = Vector3D.From(Vector3.Zero);
        Ship.Orientation = Attitude.From(Quaternion.Identity);
        MessageId = ShipDestroyed;
    }
}
