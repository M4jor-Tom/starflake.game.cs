using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>A readout the HUD shows now, with its text rendered.</summary>
public sealed record ShownReadout(string Id, HudAnchor Anchor, string Text);

/// <summary>Renders the texts of spec/hud.json for a session and keeps the message flashed last.</summary>
internal sealed partial class Hud(Spec spec, IKeyLabels keys)
{
    public const string Welcome = "welcome", Jumped = "jumped", JumpRefused = "jump_refused", ShipDestroyed = "ship_destroyed";

    const string SystemLine = "system", HullLine = "hull", Fuel = "fuel", FuelTankFull = "fuel_tank_full",
        JumpRouteReady = "jump_route_ready", JumpRouteFuelShort = "jump_route_fuel_short", JumpRouteNone = "jump_route_none",
        Counts = "counts", Crosshair = "crosshair", ControlsHelp = "controls_help";

    const string KeyPrefix = "key:";

    static readonly string[] ReadoutIds =
        [SystemLine, HullLine, Fuel, FuelTankFull, JumpRouteReady, JumpRouteFuelShort, JumpRouteNone, Counts, Crosshair, ControlsHelp];

    static readonly string[] MessageIds = [Welcome, Jumped, JumpRefused, ShipDestroyed];

    public string? MessageId { get; private set; }

    public string MessageText { get; private set; } = "";

    public float MessageTimeLeft { get; private set; }

    /// <summary>Fails at once, not mid-game, on an id named above that the spec lacks or a placeholder that names nothing.</summary>
    /// <exception cref="InvalidDataException">Naming the id or placeholder.</exception>
    public void Check(Simulation sim)
    {
        var readout = ReadoutIds.FirstOrDefault(id => !spec.Hud.Readouts.Exists(r => r.Id == id));
        if (readout != null)
            throw new InvalidDataException($"hud.json: no readout '{readout}'");
        var message = MessageIds.FirstOrDefault(id => !spec.Hud.Messages.Exists(m => m.Id == id));
        if (message != null)
            throw new InvalidDataException($"hud.json: no message '{message}'");
        foreach (var text in spec.Hud.Readouts.Select(r => r.Text).Concat(spec.Hud.Messages.Select(m => m.Text)))
            _ = Render(text, sim, sim.JumpTarget);
    }

    /// <summary>The readouts shown now, in spec order; one wording of each line that has alternatives.</summary>
    public IReadOnlyList<ShownReadout> Readouts(Simulation sim) =>
        spec.Hud.Readouts.Where(r => IsShown(r.Id, sim))
            .Select(r => new ShownReadout(r.Id, r.Anchor, Render(r.Text, sim, sim.JumpTarget))).ToList();

    static bool IsShown(string id, Simulation sim) => id switch
    {
        Fuel => !Rules.TankFull(sim.Ship),
        FuelTankFull => Rules.TankFull(sim.Ship),
        JumpRouteReady => sim.JumpTarget != null && Rules.CanJump(sim.Ship),
        JumpRouteFuelShort => sim.JumpTarget != null && !Rules.CanJump(sim.Ship),
        JumpRouteNone => sim.JumpTarget == null,
        _ => true,
    };

    /// <summary>Shows a message, replacing the current one, for the message duration; its target_name is the given system.</summary>
    public void Flash(string id, Simulation sim, StarSystem? target)
    {
        MessageId = id;
        MessageText = Render(spec.Message(id).Text, sim, target);
        MessageTimeLeft = spec.Hud.MessageDurationSeconds;
    }

    public void Step(float dt)
    {
        if (MessageId == null)
            return;
        MessageTimeLeft -= dt;
        if (MessageTimeLeft > 0)
            return;
        (MessageId, MessageText, MessageTimeLeft) = (null, "", 0);
    }

    string Render(string text, Simulation sim, StarSystem? target) =>
        Placeholder().Replace(text, match => Value(match.Groups[1].Value, sim, target));

    string Value(string placeholder, Simulation sim, StarSystem? target)
    {
        if (placeholder.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            var action = spec.Controls.Actions.Find(a => a.Id == placeholder[KeyPrefix.Length..]);
            if (action != null)
                return KeyNames.Of(action, keys);
        }
        else if (Json.TryEnum<HudField>(placeholder, out var field))
            return Value(field, sim, target);
        throw new InvalidDataException($"hud.json: unknown placeholder '{{{placeholder}}}'");
    }

    string Value(HudField field, Simulation sim, StarSystem? target)
    {
        var ship = sim.Ship;
        var system = ship.CurrentSystem;
        return field switch
        {
            HudField.SystemName => system.Name,
            HudField.StarClass => Json.SchemaText(system.StarClass).ToUpperInvariant(),
            HudField.Hull => Number(ship.Hull),
            HudField.MaxHull => Number(ship.ShipClass.MaxHull),
            HudField.Fuel => Number(ship.Fuel),
            HudField.FuelCapacity => Number(ship.ShipClass.FuelCapacity),
            HudField.TargetName => target?.Name ?? "",
            HudField.FuelMissing => Number(Math.Max(0, system.JumpFuelCost - ship.Fuel)),
            HudField.JumpFuelCost => Number(system.JumpFuelCost),
            HudField.AsteroidCount => Count(sim.System.Asteroids),
            HudField.RaiderCount => Count(sim.System.RaiderShips),
            HudField.FuelPodCount => Count(sim.System.FuelPods),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };
    }

    string Number(float value) =>
        value.ToString("F" + spec.Hud.NumberDecimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    static string Count<T>(List<T>? items) => (items?.Count ?? 0).ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\{([^{}]*)\}")]
    private static partial Regex Placeholder();
}
