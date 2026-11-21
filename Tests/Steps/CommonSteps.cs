using Reqnroll;

namespace Starflake.Tests.Steps;

/// <summary>Phrases shared by several features.</summary>
[Binding]
public sealed class CommonSteps(World world)
{
    [When("a solo session starts")]
    public void WhenASoloSessionStarts() => _ = world.Sim; // the first use starts it

    [When(@"the player holds ""([a-z_]+)""")]
    public void WhenThePlayerHolds(string actionId)
    {
        world.Hold(actionId);
        world.Run(1);
    }
}
