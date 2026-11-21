using System.Collections.Generic;

namespace Starflake.Sim;

/// <summary>The ids of spec/controls.json the simulation reacts to.</summary>
public static class Actions
{
    public const string Thrust = "thrust", Reverse = "reverse", YawLeft = "yaw_left", YawRight = "yaw_right",
        PitchUp = "pitch_up", PitchDown = "pitch_down", RollLeft = "roll_left", RollRight = "roll_right",
        Fire = "fire", NextTarget = "next_target", Jump = "jump", Pause = "pause";

    /// <summary>Every id above; the spec must define each one.</summary>
    public static readonly IReadOnlyList<string> All =
        [Thrust, Reverse, YawLeft, YawRight, PitchUp, PitchDown, RollLeft, RollRight, Fire, NextTarget, Jump, Pause];
}
