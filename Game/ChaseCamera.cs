using Godot;

namespace Starflake.Game;

/// <summary>
/// Follows behind and above a target, looking parallel to its nose: shots fly
/// toward the screen centre at any range, so the crosshair stays truthful.
/// </summary>
public partial class ChaseCamera : Camera3D
{
    static readonly Vector3 Offset = new(0, 14, 48);
    public Node3D Target = null!;

    public override void _Ready()
    {
        Far = 60000;
        Fov = 70;
    }

    public void Snap() => Follow(1);

    public override void _PhysicsProcess(double delta) => Follow(1 - Mathf.Exp(-6 * (float)delta));

    void Follow(float weight)
    {
        var target = Target.GlobalTransform;
        GlobalPosition = GlobalPosition.Lerp(target * Offset, weight);
        LookAt(GlobalPosition - target.Basis.Z, target.Basis.Y);
    }
}
