using System;
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
    readonly Random _rng;

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
        FlyPilotShip(dt, input);
        FlyRaiderShips(dt);
    }

    /// <summary>Acts on a press action of the spec. An action without an effect yet does nothing.</summary>
    /// <exception cref="InvalidDataException">The spec has no such action.</exception>
    public void Press(string actionId)
    {
        switch (Spec.Action(actionId).Id)
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
}
