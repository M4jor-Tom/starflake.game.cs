using System;
using System.IO;
using System.Linq;
using Godot;
using Starflake.Design;
using Starflake.Domain;
using Starflake.Sim;
using FileAccess = Godot.FileAccess;

namespace Starflake.Game;

/// <summary>
/// Loads the content and the spec, runs the simulation one step per physics frame and draws it.
/// With the user argument <c>--boot-check</c> it also opens and closes the pause menu, opens it again and
/// quits through its quit button; any failure prints and exits 1.
/// </summary>
public partial class Main : Node3D
{
    const string BootCheckArgument = "--boot-check";
    const int BootCheckFrames = 30;
    const string QuitButton = "quit";

    readonly StarSystemView _view = new();
    readonly ChaseCamera _camera = new();
    readonly HudLayer _hud = new();
    readonly PauseMenu _menu = new();
    Simulation _sim = null!;
    Node3D _starfield = null!;
    bool _bootCheck;
    int _frames;

    /// <summary>The galaxy to load (set in Main.tscn).</summary>
    [Export(PropertyHint.File, "*.json")] public string ContentFile { get; set; } = "";

    /// <summary>The directory holding the synced spec/*.json (set in Main.tscn).</summary>
    [Export(PropertyHint.Dir)] public string SpecDirectory { get; set; } = "";

    public override void _Ready() => Guard(() =>
    {
        AddChild(Scenery.Space());
        _starfield = Scenery.Starfield(new Random());
        AddChild(_starfield);
        AddChild(_view);
        AddChild(_camera);
        AddChild(_hud);
        AddChild(_menu);
        _menu.Chosen += id => Guard(() => _sim.Choose(id));

        var galaxy = GalaxyLoader.Parse(Read(ContentFile));
        var spec = SpecLoader.Parse(SpecLoader.Files.ToDictionary(f => f, f => Read($"{SpecDirectory}/{f}.json")), galaxy);
        InputMapper.Register(spec.Controls);
        _sim = Simulation.Start(galaxy, spec, new Random(), new InputMapper.Labels());
        Refresh();
        _bootCheck = OS.GetCmdlineUserArgs().Contains(BootCheckArgument);
    });

    public override void _PhysicsProcess(double delta) => Guard(() =>
    {
        _sim.Step((float)delta, InputMapper.Read(_sim.Spec.Controls));
        Refresh();
        if (_sim.QuitRequested)
        {
            if (_bootCheck)
                GD.Print("boot check passed"); // the quit button was the check's last step
            GetTree().Quit(0);
        }
        else if (_bootCheck)
            BootCheck();
    });

    public override void _UnhandledInput(InputEvent @event) => Guard(() =>
    {
        foreach (var action in _sim.Spec.Controls.Actions.Where(a => a.Kind == ActionKind.Press && @event.IsActionPressed(a.Id)))
            _sim.Press(action.Id);
    });

    void Refresh()
    {
        _view.Refresh(_sim);
        _camera.Refresh(_sim);
        _starfield.Position = _camera.Position; // the stars never get closer
        _hud.Refresh(_sim);
        _menu.Refresh(_sim);
    }

    // Boots, lets the session run, presses pause three times through the input pipeline, then the menu's quit
    // button, whose choice quits the game in the next frame. It asserts no gameplay.
    void BootCheck()
    {
        switch (++_frames - BootCheckFrames)
        {
            case 0:
                PressPause();
                break;
            case 1:
                Check(_sim.OpenMenu != null && _menu.Visible, "pause opens the pause menu");
                PressPause();
                break;
            case 2:
                Check(_sim.OpenMenu == null && !_menu.Visible, "pause again closes it");
                PressPause();
                break;
            case 3:
                Check(_sim.OpenMenu != null && _menu.Visible, "pause opens it once more");
                var quit = _menu.FindChild(QuitButton, owned: false) as Button;
                Check(quit != null, "the pause menu has a quit button");
                quit!.EmitSignal(BaseButton.SignalName.Pressed);
                break;
            case > 3:
                Check(false, "the quit button quits the game");
                break;
        }
    }

    static void PressPause()
    {
        Input.ParseInputEvent(new InputEventAction { Action = Actions.Pause, Pressed = true });
        Input.FlushBufferedEvents(); // headless display servers never flush on their own
    }

    static void Check(bool ok, string what)
    {
        if (!ok)
            throw new InvalidOperationException($"boot check failed: {what}");
    }

    static string Read(string path) =>
        FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path) : throw new FileNotFoundException($"{path}: no such file");

    // A failure anywhere prints and ends the game with exit code 1, instead of running on in a broken state.
    void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            GD.PrintErr(e.ToString());
            SetPhysicsProcess(false);
            SetProcessUnhandledInput(false);
            GetTree().Quit(1);
        }
    }
}
