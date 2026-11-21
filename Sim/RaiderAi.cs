using System;
using System.Linq;
using Godot;
using Starflake.Design;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>What a raider ship asks of its flight and guns for one step; Turn is (pitch, yaw, roll).</summary>
public readonly record struct RaiderIntent(Vector3 Turn, float Throttle, bool Fire);

/// <summary>Idles until the pilot ship comes within detection range, then chases it and shoots.</summary>
public static class RaiderAi
{
    public static readonly RaiderIntent Idle = new(Vector3.Zero, 0, false);

    /// <param name="longestWeaponRange">The longest max range among the weapons of the raider's ship class.</param>
    public static RaiderIntent Decide(RaiderShip raider, PilotShip pilot, RaiderAiTuning tuning, float longestWeaponRange)
    {
        var toPilot = pilot.Position.ToGodot() - raider.Position.ToGodot();
        var distance = toPilot.Length();
        if (distance >= raider.RaiderType.DetectionRange)
            return Idle;

        var attitude = raider.Orientation?.ToGodot() ?? Quaternion.Identity;
        var local = attitude.Inverse() * toPilot.Normalized(); // zero when exactly on the pilot ship: no NaN
        var turn = local.Z > 0 // behind: turn hard toward the nearer side
            ? new Vector3(0, local.X >= 0 ? -1 : 1, 0)
            : new Vector3(Mathf.Clamp(local.Y * tuning.SteeringGain, -1, 1), Mathf.Clamp(-local.X * tuning.SteeringGain, -1, 1), 0);
        var aimed = (attitude * Vector3.Forward).AngleTo(toPilot) < tuning.AimCone;
        return new RaiderIntent(turn, distance > raider.RaiderType.KeepDistance ? 1 : 0, distance < longestWeaponRange && aimed);
    }

    /// <summary>The longest max range among the weapons of the ship class; 0 without weapons.</summary>
    public static float LongestRange(ShipClass hull) => (hull.Weapons ?? []).Select(w => w.MaxRange).DefaultIfEmpty(0).Max();
}
