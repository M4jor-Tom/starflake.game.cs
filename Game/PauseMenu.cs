using Godot;
using Starflake.Design;
using Starflake.Sim;

namespace Starflake.Game;

/// <summary>Shows the simulation's open menu: its title and one button per menu button, in order. Pressing one chooses it.</summary>
public partial class PauseMenu : CanvasLayer
{
    Button? _first;

    public override void _Ready() => Visible = false;

    public void Refresh(Simulation sim)
    {
        var open = sim.OpenMenu != null;
        if (open == Visible)
            return;
        if (sim.OpenMenu is { } menu && _first == null)
            Build(sim, menu); // the session has one menu
        Visible = open;
        if (open)
            _first?.GrabFocus();
    }

    void Build(Simulation sim, Menu menu)
    {
        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var panel = new VBoxContainer();
        center.AddChild(panel);
        panel.AddChild(new Label { Text = menu.Title, HorizontalAlignment = HorizontalAlignment.Center });
        foreach (var button in menu.Buttons)
        {
            var node = new Button { Text = button.Label };
            node.Pressed += () => sim.Choose(button.Id);
            panel.AddChild(node);
            _first ??= node;
        }
    }
}
