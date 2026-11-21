using System.Globalization;
using System.Text.RegularExpressions;
using Reqnroll;
using Starflake.Design;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

[Binding]
public sealed partial class HudSteps(World world)
{
    // Fractional, so that rounding shows; neither is halfway between two shown values.
    const float FractionalHull = 12.6f, FractionalFuel = 7.7f;

    IEnumerable<string> Shown => world.Sim.Readouts.Select(r => r.Id);

    StarSystem Home => world.Galaxy.StartingSystem;

    float Duration(string slotPath) => world.Spec.Number(slotPath);

    // A jump target to press "jump" toward, added unless the star system has one.
    void EnsureJumpTarget()
    {
        if (Home.JumpTargets is not { Count: > 0 })
            GalaxyBuilder.Link(Home, world.Galaxy.System("neighbour"));
    }

    [Given("^a running session$")]
    public void GivenARunningSession()
    {
        world.Sim.Ship.Hull = FractionalHull;
        world.Sim.Ship.Fuel = FractionalFuel;
    }

    [Given(@"a pilot ship whose ship class has a fuel capacity of ([\d.]+)")]
    public void GivenAFuelCapacity(float capacity) => world.Galaxy.StartingShipClass.FuelCapacity = capacity;

    [Given(@"a pilot ship in a star system whose jump fuel cost is ([\d.]+)")]
    public void GivenAJumpFuelCost(float cost)
    {
        Home.JumpFuelCost = cost;
        EnsureJumpTarget();
    }

    // Stated as a state: the fuel is set, then the simulation steps once.
    [When(@"the ship holds ([\d.]+) fuel")]
    public void WhenTheShipHoldsFuel(float fuel)
    {
        world.Sim.Ship.Fuel = fuel;
        world.Run(World.StepSeconds);
    }

    [Given(@"the HUD is showing ""([a-z_]+)""")]
    public void GivenTheHudIsShowing(string messageId)
    {
        Assert.Equal(messageId, world.Sim.MessageId);
        world.Run(Duration("hud.message_duration_seconds") / 2); // part of its time gone, so a restart shows
        Assert.Equal(messageId, world.Sim.MessageId);
    }

    // Each message through the play that flashes it: a jump the ship has, or lacks, the fuel for.
    [When(@"the HUD flashes ""([a-z_]+)""")]
    public void WhenTheHudFlashes(string messageId)
    {
        EnsureJumpTarget();
        world.Sim.Ship.Fuel = messageId switch
        {
            "jumped" => Home.JumpFuelCost,
            "jump_refused" => 0,
            _ => throw new ArgumentException($"no play flashes '{messageId}'", nameof(messageId)),
        };
        world.Sim.Press(Actions.Jump);
        Assert.Equal(messageId, world.Sim.MessageId);
    }

    [Given(@"""([a-z_]+)"" is bound to the key labelled (\S+) on the player's keyboard")]
    public void GivenBoundToTheKeyLabelled(string actionId, string label)
    {
        var binding = Assert.Single(world.Spec.Action(actionId).Bindings);
        Assert.Equal(InputDevice.Keyboard, binding.Device);
        world.Keys.Labels[binding.Key!.Value] = label;
    }

    [Given(@"""([a-z_]+)"" is bound to the up arrow key")]
    public void GivenBoundToTheUpArrowKey(string actionId) =>
        Assert.Equal(PhysicalKey.ArrowUp, Assert.Single(world.Spec.Action(actionId).Bindings).Key);

    [Given(@"""([a-z_]+)"" is bound to the Space key and to the left mouse button")]
    public void GivenBoundToSpaceAndTheLeftMouseButton(string actionId) =>
        Assert.Collection(world.Spec.Action(actionId).Bindings,
            b => Assert.Equal((InputDevice.Keyboard, PhysicalKey.Space), (b.Device, b.Key!.Value)),
            b => Assert.Equal((InputDevice.Mouse, MouseButton.Left), (b.Device, b.Button!.Value)));

    [Then(@"^the HUD shows ((?:""[a-z_]+"", )*""[a-z_]+"" and ""[a-z_]+"")$")]
    public void ThenTheHudShowsAll(string ids)
    {
        foreach (var id in Quoted().Matches(ids).Select(m => m.Groups[1].Value))
            Assert.Contains(id, Shown);
    }

    [Then(@"^the HUD shows ""([a-z_]+)"" and not ""([a-z_]+)""$")]
    public void ThenTheHudShowsAndNot(string shown, string hidden)
    {
        Assert.Contains(shown, Shown);
        Assert.DoesNotContain(hidden, Shown);
    }

    [Then(@"^the HUD shows ""([a-z_]+)"" and neither ""([a-z_]+)"" nor ""([a-z_]+)""$")]
    public void ThenTheHudShowsAndNeither(string shown, string hidden, string alsoHidden)
    {
        ThenTheHudShowsAndNot(shown, hidden);
        Assert.DoesNotContain(alsoHidden, Shown);
    }

    [Then(@"every number is rounded to the ""([a-z_.]+)""")]
    public void ThenEveryNumberIsRounded(string slotPath)
    {
        var format = "F" + ((int)world.Spec.Number(slotPath)).ToString(CultureInfo.InvariantCulture);
        string Shows(float value) => value.ToString(format, CultureInfo.InvariantCulture);
        var hull = world.Galaxy.StartingShipClass;
        Assert.Equal(world.Spec.Readout("hull").Text.Replace("{hull}", Shows(FractionalHull)).Replace("{max_hull}", Shows(hull.MaxHull)),
            Text("hull"));
        Assert.Equal(world.Spec.Readout("fuel").Text.Replace("{fuel}", Shows(FractionalFuel)).Replace("{fuel_capacity}", Shows(hull.FuelCapacity)),
            Text("fuel"));
    }

    string Text(string readoutId) => world.Sim.Readouts.Single(r => r.Id == readoutId).Text;

    // The duration is measured in steps from the flash: shown on the last step before it ends, gone two steps later.
    [Then(@"^(?:the message|it) is shown for the ""([a-z_.]+)""(?: from that moment)?$")]
    public void ThenTheMessageIsShownFor(string slotPath)
    {
        var shown = world.Sim.MessageId;
        Assert.NotNull(shown);
        Assert.Equal(Duration(slotPath), world.Sim.MessageTimeLeft);
        world.Run(Duration(slotPath) - World.StepSeconds);
        Assert.Equal(shown, world.Sim.MessageId);
    }

    [Then("no message is shown afterwards")]
    public void ThenNoMessageIsShownAfterwards()
    {
        world.Run(2 * World.StepSeconds);
        Assert.Null(world.Sim.MessageId);
        Assert.Equal("", world.Sim.MessageText);
    }

    // Its text, with every placeholder filled, and nothing of the message it replaced.
    [Then(@"only ""([a-z_]+)"" is shown")]
    public void ThenOnlyIsShown(string messageId)
    {
        Assert.Equal(messageId, world.Sim.MessageId);
        var pieces = Placeholder().Split(world.Spec.Message(messageId).Text).Select(Regex.Escape);
        Assert.Matches("^" + string.Join(".*", pieces) + "$", world.Sim.MessageText);
        Assert.DoesNotContain("{", world.Sim.MessageText);
    }

    // In every readout shown now that names the action's keys: its spec text, the placeholder read as stated.
    [Then(@"the key placeholder of ""([a-z_]+)"" reads (.+)")]
    public void ThenTheKeyPlaceholderReads(string actionId, string expected)
    {
        var placeholder = $"{{key:{actionId}}}";
        var readouts = world.Spec.Hud.Readouts.Where(r => r.Text.Contains(placeholder) && Shown.Contains(r.Id)).ToList();
        Assert.NotEmpty(readouts);
        foreach (var readout in readouts)
        {
            var pieces = Regex.Split(readout.Text, @"(\{[^{}]*\})")
                .Select(p => p switch
                {
                    _ when p == placeholder => Regex.Escape(expected),
                    _ when Placeholder().IsMatch(p) => ".*", // another placeholder
                    _ => Regex.Escape(p),
                });
            Assert.Matches("^" + string.Concat(pieces) + "$", Text(readout.Id));
        }
    }

    [GeneratedRegex(@"""([a-z_]+)""")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Placeholder();
}
