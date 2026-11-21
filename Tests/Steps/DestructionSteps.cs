using Godot;
using Reqnroll;
using Starflake.Domain;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class DestructionSteps(World world)
{
    SpaceEntity? _entity;
    Vector3 _where;

    SpaceEntity TheEntity => _entity ?? throw new InvalidOperationException("no asteroid or raider ship given");

    [Given(@"an asteroid whose type has a fuel yield of ([\d.]+)")]
    public void GivenAnAsteroidWhoseTypeHasAFuelYieldOf(float fuel)
    {
        world.Galaxy.AsteroidField(world.Galaxy.StartingSystem, 1).AsteroidType.FuelYield = fuel;
        _entity = world.Sim.System.Asteroids![0];
    }

    [Given(@"a raider ship whose type has a fuel yield of ([\d.]+)")]
    public void GivenARaiderShipWhoseTypeHasAFuelYieldOf(float fuel)
    {
        world.Galaxy.RaiderPack(world.Galaxy.StartingSystem, 1).RaiderType.FuelYield = fuel;
        _entity = world.Sim.System.RaiderShips![0];
    }

    [When("^a hit takes its (?:integrity|hull) to 0$")]
    [Scope(Feature = "Destruction")]
    public void WhenAHitTakesItTo0()
    {
        _where = TheEntity.Position.ToGodot();
        world.HitToZero(TheEntity);
    }

    [Then("^the (?:asteroid|raider ship) is removed from the star system$")]
    public void ThenItIsRemovedFromTheStarSystem()
    {
        var state = world.Sim.System;
        Assert.DoesNotContain(TheEntity, (state.Asteroids ?? []).Cast<SpaceEntity>().Concat(state.RaiderShips ?? []));
    }

    [Then(@"a fuel pod with a fuel amount of ([\d.]+) floats where the (?:asteroid|raider ship) was")]
    public void ThenAFuelPodFloatsWhereItWas(float fuel)
    {
        var pod = Assert.Single(world.Sim.System.FuelPods ?? []);
        Assert.Equal(fuel, pod.FuelAmount);
        Assert.Equal(_where, pod.Position.ToGodot());
    }
}
