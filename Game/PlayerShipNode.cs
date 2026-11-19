using System;
using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>The local player's ship. It survives jumps; the system view around it is replaced.</summary>
public partial class PlayerShipNode : Node3D, ITarget
{
    public PlayerShip Model = null!;
    public StarSystemView View = null!;
    public Vector3 Velocity;
    Guns _guns = null!;

    public event Action? Died;

    public float HitRadius => Scenery.ShipRadius;

    public override void _Ready()
    {
        AddChild(Scenery.Ship(player: true));
        _guns = new Guns(Model.ShipClass.Weapons, byPlayer: true);
    }

    /// <summary>Places the ship at its model position, at rest, facing the star.</summary>
    public void Arrive()
    {
        Position = Model.Position.ToGodot();
        Quaternion = Quaternion.Identity;
        Velocity = Vector3.Zero;
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        var turn = new Vector3(
            Input.GetAxis(Controls.PitchDown, Controls.PitchUp),
            Input.GetAxis(Controls.YawRight, Controls.YawLeft),
            Input.GetAxis(Controls.RollRight, Controls.RollLeft));
        Velocity = Flight.Step(this, Velocity, Model.ShipClass, turn, Input.GetAxis(Controls.Reverse, Controls.Thrust), dt);

        // The system is a sphere: slide along its edge instead of leaving it.
        var bounds = Model.CurrentSystem.BoundsRadius;
        if (Position.Length() > bounds)
        {
            Position = Position.LimitLength(bounds);
            Velocity = Velocity.Slide(Position.Normalized());
        }

        _guns.Update(dt, Input.IsActionPressed(Controls.Fire), View, this, Velocity);
    }

    public void Hit(float damage)
    {
        if (!Rules.Damage(Model, damage))
            return;
        Rules.Respawn(Model);
        Arrive();
        Died?.Invoke();
    }
}
