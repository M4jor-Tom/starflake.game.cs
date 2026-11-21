using System;
using Godot;
using Starflake.Design;
using Starflake.Sim;

namespace Starflake.Game;

/// <summary>
/// Shows the simulation's open menu in a centred panel: its title and one button per menu button, in order.
/// Pressing one raises <see cref="Chosen"/> with the button's id.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    Button? _first;
    bool _built;

    public event Action<string>? Chosen;

    public override void _Ready() => Visible = false;

    public void Refresh(Simulation sim)
    {
        var open = sim.OpenMenu != null;
        if (open == Visible)
            return;
        if (sim.OpenMenu is { } menu && !_built)
            Build(menu); // the session has one menu
        Visible = open;
        if (open)
            _first?.GrabFocus();
    }

    void Build(Menu menu)
    {
        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var background = new PanelContainer();
        center.AddChild(background);
        var panel = new VBoxContainer();
        background.AddChild(panel);
        panel.AddChild(new Label { Text = menu.Title, HorizontalAlignment = HorizontalAlignment.Center });
        foreach (var button in menu.Buttons)
        {
            var node = new Button { Text = button.Label };
            node.Pressed += () => Chosen?.Invoke(button.Id);
            panel.AddChild(node);
            _first ??= node;
        }
        _built = true;
    }
}
