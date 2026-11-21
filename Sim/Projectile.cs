using Godot;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>A projectile flying straight from where it was fired until it hits or outruns its weapon's range.</summary>
public sealed class Projectile
{
    public required WeaponType Weapon { get; init; }

    public required bool ByPilot { get; init; }

    public required Vector3 Origin { get; init; }

    public Vector3 Position { get; set; }

    public required Vector3 Velocity { get; init; }

    /// <summary>Whether the segment comes within the radius of the center, including when it starts or ends inside.</summary>
    public static bool SegmentHitsSphere(Vector3 from, Vector3 to, Vector3 center, float radius)
    {
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        var t = lengthSquared > 0 ? Mathf.Clamp((center - from).Dot(segment) / lengthSquared, 0, 1) : 0;
        return (from + segment * t).DistanceSquaredTo(center) <= radius * radius;
    }
}
