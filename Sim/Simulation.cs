using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>One solo session: the galaxy state, the spec it plays by, and the pilot ship.</summary>
public sealed class Simulation
{
    readonly Random _rng;
    readonly Guns _guns = new();
    readonly List<Projectile> _projectiles = [];
    readonly Hud _hud;
    const string PauseMenu = "pause_menu", Resume = "resume", Quit = "quit";
    int _targetIndex;

    Simulation(GalaxyState state, Spec spec, PilotShip ship, Random rng, IKeyLabels keys)
    {
        _hud = new Hud(spec, keys);
        Camera = new Camera(spec.Camera);
        State = state;
        Spec = spec;
        Ship = ship;
        _rng = rng;
        System = Rules.StateOf(State, ship.CurrentSystem, rng);
    }

    public GalaxyState State { get; }

    public Spec Spec { get; }

    public PilotShip Ship { get; }

    /// <summary>The state of the pilot ship's current star system.</summary>
    public StarSystemState System { get; private set; }

    /// <summary>The projectiles in flight in the current star system.</summary>
    public IReadOnlyList<Projectile> Projectiles => _projectiles;

    /// <summary>The readouts the HUD shows now, in spec order, with their texts rendered.</summary>
    public IReadOnlyList<ShownReadout> Readouts => _hud.Readouts(this);

    /// <summary>The id of the HUD message shown now; null when none is.</summary>
    public string? MessageId => _hud.MessageId;

    /// <summary>The rendered text of the message shown now; empty when none is.</summary>
    public string MessageText => _hud.MessageText;

    /// <summary>How long the message shown now stays; 0 when none is.</summary>
    public float MessageTimeLeft => _hud.MessageTimeLeft;

    /// <summary>The menu shown now; while one is, the session is paused.</summary>
    public Menu? OpenMenu { get; private set; }

    /// <summary>Set once the player has chosen to quit; the engine closes the game.</summary>
    public bool QuitRequested { get; private set; }

    /// <summary>The chase camera, moved at the end of each step.</summary>
    public Camera Camera { get; }

    /// <summary>The selected jump target; null in a star system without jump targets.</summary>
    public StarSystem? JumpTarget =>
        Ship.CurrentSystem.JumpTargets is { Count: > 0 } targets ? targets[_targetIndex % targets.Count] : null;

    /// <exception cref="InvalidDataException">The spec asks for another hosting mode, lacks an action, readout, message,
    /// menu or button the game uses, or has a HUD text with a placeholder that names nothing.</exception>
    public static Simulation Start(Galaxy galaxy, Spec spec, Random rng, IKeyLabels keys)
    {
        if (spec.Session.HostingMode != HostingMode.Solo)
            throw new InvalidDataException($"session.json: hosting_mode {spec.Session.HostingMode} is not supported, only solo");
        var missing = Actions.All.FirstOrDefault(id => !spec.Controls.Actions.Exists(a => a.Id == id));
        if (missing != null)
            throw new InvalidDataException($"controls.json: no action '{missing}'");
        var menu = spec.Menus.Menus.Find(m => m.Id == PauseMenu)
            ?? throw new InvalidDataException($"menus.json: no menu '{PauseMenu}'");
        var button = new[] { Resume, Quit }.FirstOrDefault(id => !menu.Buttons.Exists(b => b.Id == id));
        if (button != null)
            throw new InvalidDataException($"menus.json: no button '{button}' in menu '{PauseMenu}'");

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
        var sim = new Simulation(state, spec, ship, rng, keys);
        sim._hud.Check(sim);
        sim.Camera.Snap(ship);
        sim.Flash(Hud.Welcome);
        return sim;
    }

    /// <summary>Advances the session by one step; each concern runs in turn. A paused session does not change.</summary>
    public void Step(float dt, PilotInput input)
    {
        if (OpenMenu != null)
            return;
        _hud.Step(dt); // first, so a message flashed during this step is shown for its whole duration
        foreach (var shot in _guns.Step(Ship, Ship.ShipClass, input.IsHeld(Actions.Fire), Spec.Tuning.Flight, dt))
            Spawn(shot); // from where the ship is when the step begins
        FlyPilotShip(dt, input);
        FlyRaiderShips(dt);
        MoveProjectiles(dt);
        FuelScoop.Step(Ship, System, dt);
        Camera.Step(Ship, dt); // last, after any respawn: the camera trails the ship back instead of snapping
    }

    /// <summary>Puts a fired projectile in flight in the current star system.</summary>
    public void Spawn(Projectile projectile) => _projectiles.Add(projectile);

    /// <summary>Acts on a press action of the spec. Pause opens and closes the pause menu; while it is open, nothing else
    /// does anything. An action without an effect does nothing, and so do next_target and jump in a star system
    /// without jump targets.</summary>
    /// <exception cref="ArgumentException">The spec has no such action.</exception>
    public void Press(string actionId)
    {
        if (!Spec.Controls.Actions.Exists(a => a.Id == actionId))
            throw new ArgumentException($"no action '{actionId}'", nameof(actionId));
        if (OpenMenu != null && actionId != Actions.Pause)
            return;
        switch (actionId)
        {
            case Actions.Pause:
                OpenMenu = OpenMenu == null ? Spec.Menu(PauseMenu) : null;
                break;
            case Actions.NextTarget when JumpTarget is not null:
                _targetIndex = (_targetIndex + 1) % Ship.CurrentSystem.JumpTargets!.Count;
                break;
            case Actions.Jump when JumpTarget is { } target:
                Jump(target);
                break;
        }
    }

    // The star system left keeps its state in State.SystemStates; its projectiles in flight are gone.
    void Jump(StarSystem target)
    {
        if (!Rules.TryJump(Ship, target, Spec.Tuning))
        {
            Flash(Hud.JumpRefused);
            return;
        }
        _projectiles.Clear();
        _targetIndex = 0;
        System = Rules.StateOf(State, target, _rng);
        Camera.Snap(Ship);
        Flash(Hud.Jumped, target);
    }

    void Flash(string messageId, StarSystem? target = null) => _hud.Flash(messageId, this, target ?? JumpTarget);

    /// <summary>Acts on a button of the open menu: resume closes it, quit asks the engine to close the game.
    /// Without an open menu there is nothing to choose.</summary>
    /// <exception cref="ArgumentException">No menu of the spec has such a button.</exception>
    public void Choose(string buttonId)
    {
        if (!Spec.Menus.Menus.Exists(m => m.Buttons.Exists(b => b.Id == buttonId)))
            throw new ArgumentException($"no menu button '{buttonId}'", nameof(buttonId));
        if (OpenMenu == null)
            return;
        switch (buttonId)
        {
            case Resume:
                OpenMenu = null;
                break;
            case Quit:
                QuitRequested = true;
                break;
        }
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
        {
            var hull = raider.RaiderType.ShipClass;
            var intent = RaiderAi.Decide(raider, Ship, Spec.Tuning.RaiderAi, RaiderAi.LongestRange(hull));
            foreach (var shot in _guns.Step(raider, hull, intent.Fire, Spec.Tuning.Flight, dt))
                Spawn(shot); // from where the raider is when the step begins
            Flight.Step(raider, hull, intent.Turn, intent.Throttle, Spec.Tuning.Flight, dt);
        }
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
        Rules.Arrive(Ship, Spec.Tuning);
        Flash(Hud.ShipDestroyed);
    }
}
