using Godot;
using Reqnroll;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class PauseSteps(World world)
{
    Snapshot? _before;
    int _stepsBefore;

    Snapshot Before => _before ?? throw new InvalidOperationException("nothing pressed or chosen");

    /// <summary>What a paused session must hold still: the motion of every ship, projectile and fuel pod,
    /// every hull and integrity, and the message on screen with its time left; and the menu open.</summary>
    sealed record Snapshot(List<object> Motion, List<float> Damage, (string? Id, float TimeLeft) Message, string? Menu)
    {
        public static Snapshot Of(Simulation sim)
        {
            List<Ship> ships = [sim.Ship, .. sim.System.RaiderShips ?? []];
            return new(
                [.. ships.Select(s => (object)Pose.Of(s)), .. sim.Projectiles.Select(p => (object)(p.Position, p.Velocity)),
                    .. (sim.System.FuelPods ?? []).Select(p => (object)Pose.Of(p))],
                [.. ships.Select(s => s.Hull), .. (sim.System.Asteroids ?? []).Select(a => a.Integrity)],
                (sim.MessageId, sim.MessageTimeLeft), sim.OpenMenu?.Id);
        }
    }

    // Everything that would change in a second of play if it were not frozen: the pilot ship moving with a weapon,
    // a raider ship moving and aiming at it within range, a projectile about to hit an asteroid,
    // a fuel pod inside tractor range, and the welcome message counting down.
    static void Stage(World w)
    {
        var home = w.Galaxy.StartingSystem;
        w.Galaxy.Weapon(w.Galaxy.StartingShipClass);
        w.Galaxy.Weapon(w.Galaxy.RaiderPack(home, 1).RaiderType.ShipClass);
        w.Galaxy.AsteroidField(home, 1);
        var sim = w.Sim;
        var ship = sim.Ship;
        ship.Velocity = new Vector3D { X = 0, Y = 0, Z = -15 };
        var raider = sim.System.RaiderShips![0];
        raider.Position = Vector3D.From(ship.Position.ToGodot() + Vector3.Forward * 600);
        raider.Orientation = Attitude.From(new Quaternion(Vector3.Up, MathF.PI)); // nose toward the pilot ship
        (sim.System.FuelPods ??= []).Add(new FuelPod
        {
            Id = "pod", FuelAmount = 9, Position = Vector3D.From(ship.Position.ToGodot() + Vector3.Right * 120),
        });
        w.FireAt(sim.System.Asteroids![0], w.Galaxy.Weapon(), byPilot: true);
        w.Run(0.25f);
    }

    // Paused for a second, with "fire" held, before the scenario goes on.
    static void OpenPauseMenu(World w)
    {
        Stage(w);
        w.Sim.Press(Actions.Pause);
        w.Hold(Actions.Fire);
        w.Run(1);
    }

    // The action, then a second of play with "fire" held, so that any weapon able to fire does.
    static void PlayOn(World w, Action<Simulation> act)
    {
        act(w.Sim);
        w.Hold(Actions.Fire);
        w.Run(1);
    }

    void ActAndPlayOn(Action<Simulation> act)
    {
        _before = Snapshot.Of(world.Sim);
        _stepsBefore = world.Steps;
        PlayOn(world, act);
    }

    [Given("a running session with ships and projectiles in motion")]
    public void GivenARunningSessionInMotion() => Stage(world);

    [Given(@"the menu ""([a-z_]+)"" is open")]
    public void GivenTheMenuIsOpen(string menuId)
    {
        OpenPauseMenu(world);
        Assert.Equal(menuId, world.Sim.OpenMenu?.Id);
    }

    [Given("the pilot ship holds enough fuel to jump")]
    public void GivenThePilotShipHoldsEnoughFuelToJump()
    {
        var home = world.Galaxy.StartingSystem;
        GalaxyBuilder.Link(home, world.Galaxy.System("neighbour"));
        world.Sim.Ship.Fuel = home.JumpFuelCost;
    }

    [When(@"the player presses ""([a-z_]+)""")]
    [Scope(Feature = "Pause")]
    public void WhenThePlayerPresses(string actionId) => ActAndPlayOn(sim => sim.Press(actionId));

    [When(@"the player chooses ""([a-z_]+)""")]
    public void WhenThePlayerChooses(string buttonId) => ActAndPlayOn(sim => sim.Choose(buttonId));

    [Then(@"the menu ""([a-z_]+)"" opens with its buttons in the order of the spec")]
    public void ThenTheMenuOpensWithItsButtons(string menuId)
    {
        var menu = Assert.IsType<Starflake.Design.Menu>(world.Sim.OpenMenu);
        Assert.Equal(menuId, menu.Id);
        var inSpec = world.Spec.Raw["menus"]["menus"]!.AsArray().Single(m => (string?)m!["id"] == menuId)!["buttons"]!.AsArray();
        Assert.Equal(inSpec.Select(b => (string?)b!["id"]), menu.Buttons.Select(b => b.Id));
    }

    [Then(@"the menu ""([a-z_]+)"" closes")]
    public void ThenTheMenuCloses(string menuId)
    {
        Assert.Equal(menuId, Before.Menu);
        Assert.Null(world.Sim.OpenMenu);
    }

    [Then("no ship, projectile or fuel pod moves")]
    public void ThenNothingMoves()
    {
        Assert.NotEmpty(Before.Motion);
        Assert.Equal(Before.Motion, Snapshot.Of(world.Sim).Motion);
    }

    [Then("no weapon fires and nothing takes damage")]
    public void ThenNoWeaponFiresAndNothingTakesDamage()
    {
        ThenNoWeaponFires();
        Assert.Equal(Before.Damage, Snapshot.Of(world.Sim).Damage);
    }

    [Then("no weapon fires")]
    public void ThenNoWeaponFires() => Assert.DoesNotContain(world.Fired, f => f.Step > _stepsBefore);

    [Then(@"a HUD message on screen stops counting down its ""hud\.message_duration_seconds""")]
    public void ThenTheMessageStopsCountingDown()
    {
        Assert.NotNull(Before.Message.Id);
        var (timeLeft, duration) = (Before.Message.TimeLeft, world.Spec.Number("hud.message_duration_seconds"));
        Assert.True(timeLeft > 0 && timeLeft < duration, $"{timeLeft} s left of {duration} s");
        Assert.Equal(Before.Message, Snapshot.Of(world.Sim).Message);
    }

    // As if never paused: the same session, staged alike and played on for the same second.
    [Then("every ship, projectile and fuel pod carries on from where it was, at the velocity it had")]
    public void ThenEverythingCarriesOn()
    {
        var unpaused = new World();
        Stage(unpaused);
        PlayOn(unpaused, _ => { });
        Assert.Equal(Snapshot.Of(unpaused.Sim).Motion, Snapshot.Of(world.Sim).Motion);
    }

    [Then(@"the session carries on as it does when the player chooses ""([a-z_]+)""")]
    public void ThenTheSessionCarriesOnAsWhenChoosing(string buttonId)
    {
        var chosen = new World();
        OpenPauseMenu(chosen);
        PlayOn(chosen, sim => sim.Choose(buttonId));
        var (expected, actual) = (Snapshot.Of(chosen.Sim), Snapshot.Of(world.Sim));
        Assert.Equal(expected.Motion, actual.Motion);
        Assert.Equal(expected.Damage, actual.Damage);
        Assert.Equal(expected.Message, actual.Message);
    }

    [Then("the game closes")]
    public void ThenTheGameCloses() => Assert.True(world.Sim.QuitRequested);
}
