using Godot;

namespace Starflake.Game;

/// <summary>Input actions, registered in code so the bindings live next to their use.</summary>
public static class Controls
{
    public const string Thrust = "thrust", Reverse = "reverse";
    public const string YawLeft = "yaw_left", YawRight = "yaw_right";
    public const string PitchUp = "pitch_up", PitchDown = "pitch_down";
    public const string RollLeft = "roll_left", RollRight = "roll_right";
    public const string Fire = "fire", NextTarget = "next_target", Jump = "jump";

    public const string Help =
        "W/S thrust  ·  A/D yaw  ·  ↑/↓ pitch  ·  Q/E roll  ·  Space or click fire  ·  Tab next jump target  ·  J jump";

    public static void Register()
    {
        Bind(Thrust, Key.W);
        Bind(Reverse, Key.S);
        Bind(YawLeft, Key.A);
        Bind(YawRight, Key.D);
        Bind(PitchUp, Key.Up);
        Bind(PitchDown, Key.Down);
        Bind(RollLeft, Key.Q);
        Bind(RollRight, Key.E);
        Bind(Fire, Key.Space);
        Bind(NextTarget, Key.Tab);
        Bind(Jump, Key.J);
        InputMap.ActionAddEvent(Fire, new InputEventMouseButton { ButtonIndex = MouseButton.Left });
    }

    static void Bind(string action, Key key)
    {
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
