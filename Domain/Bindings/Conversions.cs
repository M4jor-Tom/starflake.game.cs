using Godot;

namespace Starflake.Domain;

// Hand-written halves of generated partial classes: conversions to engine types.

public partial class Vector3D
{
    public Vector3 ToGodot() => new(X, Y, Z);

    public static Vector3D From(Vector3 v) => new() { X = v.X, Y = v.Y, Z = v.Z };
}

public partial class Attitude
{
    public Quaternion ToGodot() => new(X, Y, Z, W);

    public static Attitude From(Quaternion q) => new() { X = q.X, Y = q.Y, Z = q.Z, W = q.W };
}
