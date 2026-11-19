using System;
using System.Linq;
using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>Loads content, starts a solo session and swaps star systems on jumps.</summary>
public partial class Main : Node3D
{
    const string ContentPath = "res://content/core.json";
    readonly Random _rng = new();
    GameSession _session = null!;
    PlayerShipNode _player = null!;
    ChaseCamera _camera = null!;
    Hud _hud = null!;
    Node3D _starfield = null!;
    StarSystemView? _view;
    int _targetIndex;

    internal GameSession Session => _session;

    internal StarSystemView View => _view!;

    public override void _Ready()
    {
        ContentPack content;
        try
        {
            content = ContentLoader.Parse(FileAccess.GetFileAsString(ContentPath));
        }
        catch (Exception e)
        {
            GD.PrintErr($"{ContentPath}: {e.Message}");
            GetTree().Quit(1);
            return;
        }

        Controls.Register();
        StartSolo(content);
        if (OS.GetCmdlineUserArgs().Contains("--self-test"))
            RunSelfTest(content);
    }

    void StartSolo(ContentPack content)
    {
        _session = Rules.NewSoloSession(content, "Pilot");

        AddChild(Scenery.Space());
        AddChild(_starfield = Scenery.Starfield(_rng));
        AddChild(_player = new PlayerShipNode { Model = _session.PlayerShips![0] });
        AddChild(_camera = new ChaseCamera { Target = _player });
        AddChild(_hud = new Hud());
        _player.Died += () => _hud.Flash("Ship destroyed. Repaired at the arrival point, fuel kept.");
        Enter(Ship.CurrentSystem);
        _hud.Flash("Shoot asteroids and pirates, collect their fuel, then jump.");
    }

    async void RunSelfTest(ContentPack content)
    {
        try
        {
            SelfTest.Run(content);
            await SelfTest.RunScene(this);
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PrintErr(e.Message);
            GetTree().Quit(1);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_view == null)
            return;
        _starfield.Position = _camera.Position;
        _hud.Show(Ship, _view.State, JumpTarget);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        var target = JumpTarget;
        if (target == null)
            return;
        if (input.IsActionPressed(Controls.NextTarget))
            _targetIndex++;
        else if (input.IsActionPressed(Controls.Jump))
        {
            var from = Ship.CurrentSystem;
            if (!Rules.TryJump(Ship, target))
            {
                _hud.Flash($"Jumping out of {from.Name} costs {from.JumpFuelCost:0} fuel.");
                return;
            }
            Enter(target);
            _hud.Flash($"Jumped to {target.Name}.");
        }
    }

    PlayerShip Ship => _player.Model;

    StarSystem? JumpTarget =>
        Ship.CurrentSystem.JumpTargets is { Count: > 0 } targets ? targets[_targetIndex % targets.Count] : null;

    void Enter(StarSystem system)
    {
        _view?.QueueFree(); // its entity nodes write their state back as they leave the tree
        _view = new StarSystemView { State = Rules.StateOf(_session, system, _rng), Player = _player };
        AddChild(_view);
        _player.View = _view;
        _player.Arrive();
        _camera.Snap();
        _targetIndex = 0;
    }
}
