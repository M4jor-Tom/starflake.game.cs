using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Starflake.Design;
using Starflake.Sim;

namespace Starflake.Game;

/// <summary>Shows the readouts of the simulation's HUD, one label per anchor, and its message at the top centre.</summary>
public partial class HudLayer : CanvasLayer
{
    static readonly Dictionary<HudAnchor, (HorizontalAlignment, VerticalAlignment)> Alignments = new()
    {
        [HudAnchor.TopLeft] = (HorizontalAlignment.Left, VerticalAlignment.Top),
        [HudAnchor.Center] = (HorizontalAlignment.Center, VerticalAlignment.Center),
        [HudAnchor.BottomCenter] = (HorizontalAlignment.Center, VerticalAlignment.Bottom),
    };

    readonly Dictionary<HudAnchor, Label> _readouts = [];
    Label _message = null!;

    public override void _Ready()
    {
        foreach (var (anchor, alignment) in Alignments)
            _readouts[anchor] = AddLabel(alignment);
        _message = AddLabel((HorizontalAlignment.Center, VerticalAlignment.Top));
    }

    public void Refresh(Simulation sim)
    {
        var readouts = sim.Readouts;
        if (readouts.FirstOrDefault(r => !_readouts.ContainsKey(r.Anchor)) is { } unplaced)
            throw new InvalidDataException($"hud: readout '{unplaced.Id}' has anchor {unplaced.Anchor}, which the HUD does not lay out");
        foreach (var (anchor, label) in _readouts)
            label.Text = string.Join("\n", readouts.Where(r => r.Anchor == anchor).Select(r => r.Text));
        _message.Text = sim.MessageText;
    }

    Label AddLabel((HorizontalAlignment Horizontal, VerticalAlignment Vertical) alignment)
    {
        var label = Scenery.HudLabel(alignment.Horizontal, alignment.Vertical);
        AddChild(label);
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect); // the whole screen inside the margin; the alignment places the text
        (label.OffsetLeft, label.OffsetTop, label.OffsetRight, label.OffsetBottom) =
            (Scenery.HudMargin, Scenery.HudMargin, -Scenery.HudMargin, -Scenery.HudMargin);
        return label;
    }
}
