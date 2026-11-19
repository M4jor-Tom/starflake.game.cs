using Godot;
using Starflake.Domain;

namespace Starflake.Game;

public partial class Hud : CanvasLayer
{
    Label _status = null!, _message = null!;
    double _messageTimeLeft;

    public override void _Ready()
    {
        _status = AddLabel(HorizontalAlignment.Left, VerticalAlignment.Top);
        _message = AddLabel(HorizontalAlignment.Center, VerticalAlignment.Top);
        AddLabel(HorizontalAlignment.Center, VerticalAlignment.Center).Text = "+";
        AddLabel(HorizontalAlignment.Center, VerticalAlignment.Bottom).Text = Controls.Help;
    }

    public void Show(PlayerShip ship, StarSystemState state, StarSystem? target)
    {
        var system = ship.CurrentSystem;
        var jump = target == null ? "No jump route"
            : Rules.CanJump(ship) ? $"Jump to {target.Name}: READY, press J"
            : $"Jump to {target.Name}: {system.JumpFuelCost - ship.Fuel:0} more fuel needed";
        _status.Text =
            $"{system.Name}  ·  class {system.StarClass} star\n" +
            $"Hull  {ship.Hull:0} / {ship.ShipClass.MaxHull:0}\n" +
            $"Fuel  {ship.Fuel:0} / {ship.ShipClass.FuelCapacity:0}{(Rules.TankFull(ship) ? "  (full)" : "")}\n" +
            $"{jump}\n" +
            $"Asteroids {state.Asteroids!.Count}  ·  Enemies {state.EnemyShips!.Count}  ·  Fuel drops {state.LootDrops!.Count}";
    }

    public void Flash(string text)
    {
        _message.Text = text;
        _messageTimeLeft = 4;
    }

    public override void _Process(double delta)
    {
        if (_messageTimeLeft > 0 && (_messageTimeLeft -= delta) <= 0)
            _message.Text = "";
    }

    Label AddLabel(HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var label = new Label
        {
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(label);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 16);
        return label;
    }
}
