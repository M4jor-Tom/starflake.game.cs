using System.IO;
using System.Linq;
using Starflake.Design;

namespace Starflake.Sim;

/// <summary>Names a physical key as the player's keyboard layout prints it; the engine knows the layout.</summary>
public interface IKeyLabels
{
    string LabelOf(PhysicalKey key);
}

/// <summary>How the HUD names the inputs bound to an action: keys that print no letter are named here, the rest by the layout.</summary>
public static class KeyNames
{
    public static string Of(ControlAction action, IKeyLabels labels) =>
        string.Join(" or ", action.Bindings.Select(b => Of(b, labels)));

    static string Of(Binding binding, IKeyLabels labels) => binding switch
    {
        { Key: PhysicalKey.ArrowUp } => "↑",
        { Key: PhysicalKey.ArrowDown } => "↓",
        { Key: PhysicalKey.Space } => "Space",
        { Key: PhysicalKey.Tab } => "Tab",
        { Key: PhysicalKey.Escape } => "Esc",
        { Key: { } key } => labels.LabelOf(key),
        { Button: MouseButton.Left } => "click",
        _ => throw new InvalidDataException($"controls.json: no name for a {binding.Device} binding"),
    };
}
