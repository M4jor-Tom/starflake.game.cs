using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Starflake.Design;
using Starflake.Sim;
using GodotButton = Godot.MouseButton;
using SpecButton = Starflake.Design.MouseButton;

namespace Starflake.Game;

/// <summary>Turns the bindings of spec/controls.json into Godot input actions, and those actions back into pilot input.</summary>
public static class InputMapper
{
    static readonly Dictionary<PhysicalKey, Key> Keys = new()
    {
        [PhysicalKey.KeyW] = Key.W,
        [PhysicalKey.KeyS] = Key.S,
        [PhysicalKey.KeyA] = Key.A,
        [PhysicalKey.KeyD] = Key.D,
        [PhysicalKey.KeyQ] = Key.Q,
        [PhysicalKey.KeyE] = Key.E,
        [PhysicalKey.KeyJ] = Key.J,
        [PhysicalKey.ArrowUp] = Key.Up,
        [PhysicalKey.ArrowDown] = Key.Down,
        [PhysicalKey.Space] = Key.Space,
        [PhysicalKey.Tab] = Key.Tab,
        [PhysicalKey.Escape] = Key.Escape,
    };

    static readonly Dictionary<SpecButton, GodotButton> Buttons = new()
    {
        [SpecButton.Left] = GodotButton.Left,
    };

    /// <summary>Adds one input action per spec action, with an event per binding.</summary>
    /// <exception cref="InvalidDataException">A binding names a key or button the tables above lack.</exception>
    public static void Register(ControlScheme controls)
    {
        foreach (var action in controls.Actions)
        {
            InputMap.AddAction(action.Id);
            foreach (var binding in action.Bindings)
                InputMap.ActionAddEvent(action.Id, EventOf(binding));
        }
    }

    /// <summary>The hold actions down now.</summary>
    public static PilotInput Read(ControlScheme controls) =>
        new(controls.Actions.Where(a => a.Kind == ActionKind.Hold && Input.IsActionPressed(a.Id)).Select(a => a.Id).ToHashSet());

    static InputEvent EventOf(Binding binding) => binding switch
    {
        { Key: { } key } => new InputEventKey { PhysicalKeycode = KeyOf(key) },
        { Button: { } button } => new InputEventMouseButton
        {
            ButtonIndex = Buttons.TryGetValue(button, out var index)
                ? index
                : throw new InvalidDataException($"controls.json: no Godot mouse button for '{button}'"),
        },
        _ => throw new InvalidDataException($"controls.json: a {binding.Device} binding without a key or button"),
    };

    static Key KeyOf(PhysicalKey key) =>
        Keys.TryGetValue(key, out var godotKey) ? godotKey : throw new InvalidDataException($"controls.json: no Godot key for '{key}'");

    /// <summary>Names a physical key as the player's keyboard layout prints it; without a display, by its US-layout name.</summary>
    public sealed class Labels : IKeyLabels
    {
        readonly bool _hasLayout = DisplayServer.GetName() != "headless"; // the headless server knows no layout

        public string LabelOf(PhysicalKey key)
        {
            var physical = KeyOf(key);
            return OS.GetKeycodeString(_hasLayout ? DisplayServer.KeyboardGetLabelFromPhysical(physical) : physical);
        }
    }
}
