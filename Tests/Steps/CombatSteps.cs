using Godot;
using Reqnroll;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class CombatSteps(World world)
{
    readonly List<WeaponType> _weapons = [];
    Asteroid? _asteroid;
    RaiderShip? _raider;
    WeaponType? _weapon;
    Projectile? _projectile;
    List<Projectile> _inFlight = [];
    (List<Projectile> Away, List<Projectile> Back) _afterJumps;

    ShipClass Hull => world.Galaxy.StartingShipClass;

    Projectile TheProjectile => _projectile ?? throw new InvalidOperationException("no projectile given");

    Asteroid TheAsteroid => _asteroid ?? throw new InvalidOperationException("no asteroid given");

    RaiderShip TheRaider => _raider ?? throw new InvalidOperationException("no raider ship given");

    WeaponType NewWeapon(float damage)
    {
        _weapon = world.Galaxy.Weapon();
        _weapon.Damage = damage;
        return _weapon;
    }

    // The shots the weapon fired, by step, in order.
    List<int> ShotsOf(WeaponType weapon) => [.. world.Fired.Where(f => f.Projectile.Weapon == weapon).Select(f => f.Step)];

    [Given(@"a pilot ship whose ship class has a weapon with a fire rate of ([\d.]+) shots per second")]
    public void GivenAPilotShipWithAWeaponFiring(float rate) => GivenAnotherWeaponFiring(rate);

    [Given(@"another weapon with a fire rate of ([\d.]+) shots per second")]
    public void GivenAnotherWeaponFiring(float rate)
    {
        var weapon = world.Galaxy.Weapon(Hull);
        weapon.FireRate = rate;
        _weapons.Add(weapon);
    }

    [When(@"the player starts holding ""([a-z_]+)""")]
    public void WhenThePlayerStartsHolding(string actionId)
    {
        world.Hold(actionId);
        world.Run(2);
    }

    [Then("each weapon fires one projectile at once")]
    public void ThenEachWeaponFiresOneProjectileAtOnce() =>
        Assert.All(_weapons, w => Assert.Equal(1, ShotsOf(w).Count(step => step == 1)));

    [Then(@"the (first|second) weapon fires again every ([\d.]+) seconds while ""fire"" is held")]
    public void ThenTheWeaponFiresAgainEvery(string which, float seconds)
    {
        var shots = ShotsOf(_weapons[which == "first" ? 0 : 1]);
        Assert.True(shots.Count >= 3, $"only {shots.Count} shots");
        Assert.All(shots.Zip(shots.Skip(1)), s => World.AssertMeasured(seconds, (s.Second - s.First) * World.StepSeconds));
    }

    [Given("the weapon has just fired")]
    public void GivenTheWeaponHasJustFired()
    {
        world.Hold(Actions.Fire);
        world.Run(World.StepSeconds);
        Assert.Single(ShotsOf(_weapons[0]));
    }

    [When(@"the player releases ""([a-z_]+)"" and holds it again ([\d.]+) seconds later")]
    public void WhenThePlayerReleasesAndHoldsItAgainLater(string actionId, float seconds)
    {
        world.Hold();
        world.Run(seconds);
        world.Hold(actionId);
        world.Run(1);
    }

    [Then(@"the weapon fires its next projectile ([\d.]+) seconds after the previous one")]
    public void ThenTheWeaponFiresItsNextProjectileAfterThePreviousOne(float seconds)
    {
        var shots = ShotsOf(_weapons[0]);
        Assert.True(shots.Count >= 2, "the weapon did not fire again");
        World.AssertMeasured(seconds, (shots[1] - shots[0]) * World.StepSeconds);
    }

    [Given(@"a ship moving at ([\d.]+) metres per second along its nose")]
    public void GivenAShipMovingAlongItsNose(float speed)
    {
        var ship = world.Sim.Ship;
        var attitude = new Quaternion(Vector3.Up, 0.6f) * new Quaternion(Vector3.Right, 0.3f); // not the default attitude
        ship.Orientation = Attitude.From(attitude);
        ship.Velocity = Vector3D.From(attitude * Vector3.Forward * speed);
    }

    [Given(@"a weapon with a projectile speed of ([\d.]+) metres per second")]
    public void GivenAWeaponWithAProjectileSpeedOf(float speed) => world.Galaxy.Weapon(Hull).ProjectileSpeed = speed;

    [When("the ship fires the weapon")]
    public void WhenTheShipFiresTheWeapon()
    {
        world.Hold(Actions.Fire);
        world.Run(World.StepSeconds);
        _projectile = Assert.Single(world.Sim.Projectiles);
    }

    [Then(@"the projectile starts on the line of the ship's nose, ahead of the ship by the ""([a-z_.]+)"" plus the ""([a-z_.]+)""")]
    public void ThenTheProjectileStartsAheadOfTheNose(string radius, string clearance)
    {
        var ahead = world.Before.Position + world.Before.Nose * (world.Spec.Number(radius) + world.Spec.Number(clearance));
        Assert.Equal(0, TheProjectile.Origin.DistanceTo(ahead), World.Tolerance);
    }

    [Then(@"the projectile flies straight at ([\d.]+) metres per second in the direction of the nose")]
    public void ThenTheProjectileFliesStraightAlongTheNose(float speed)
    {
        Assert.Equal(speed, TheProjectile.Velocity.Length(), World.Tolerance);
        Assert.Equal(1, TheProjectile.Velocity.Normalized().Dot(world.Before.Nose), World.Tolerance);
        var flown = TheProjectile.Origin + TheProjectile.Velocity * World.StepSeconds;
        Assert.Equal(0, TheProjectile.Position.DistanceTo(flown), World.Tolerance);
    }

    [Given(@"a projectile of a weapon with a max range of ([\d.]+) metres")]
    public void GivenAProjectileOfAWeaponWithAMaxRangeOf(float range)
    {
        world.Galaxy.Weapon(Hull).MaxRange = range;
        WhenTheShipFiresTheWeapon();
        world.Hold();
    }

    // Steps to the first step past the range; the projectile must stay in flight while within it.
    [When(@"the projectile is farther than ([\d.]+) metres from where it started")]
    public void WhenTheProjectileIsFartherThan(float range)
    {
        var p = TheProjectile;
        while (p.Origin.DistanceTo(p.Position) <= range)
        {
            Assert.Contains(p, world.Sim.Projectiles); // still in flight within range
            world.Run(World.StepSeconds);
        }
    }

    [Then("the projectile disappears")]
    public void ThenTheProjectileDisappears() => Assert.DoesNotContain(TheProjectile, world.Sim.Projectiles);

    [Given(@"an asteroid with ([\d.]+) integrity")]
    public void GivenAnAsteroidWithIntegrity(float integrity)
    {
        world.Galaxy.AsteroidField(world.Galaxy.StartingSystem, 1);
        _asteroid = world.Sim.System.Asteroids![0];
        _asteroid.Integrity = integrity;
    }

    [Given(@"a raider ship with ([\d.]+) hull")]
    public void GivenARaiderShipWithHull(float hull)
    {
        world.Galaxy.RaiderPack(world.Galaxy.StartingSystem, 1);
        _raider = world.Sim.System.RaiderShips![0];
        _raider.Hull = hull;
    }

    [Given(@"a projectile fired by the pilot ship from a weapon with a damage of ([\d.]+)")]
    public void GivenAProjectileFiredByThePilotShip(float damage) =>
        _projectile = world.FireAt((SpaceEntity?)_asteroid ?? TheRaider, NewWeapon(damage), byPilot: true);

    [When("the path of the projectile passes within the radius of the asteroid's type")]
    public void WhenThePathPassesWithinTheAsteroidsRadius() =>
        world.RunUntilWithin(TheProjectile, TheAsteroid, TheAsteroid.AsteroidType.Radius);

    [When(@"the path of the projectile passes within the ""([a-z_.]+)"" of the (raider|pilot) ship")]
    public void WhenThePathPassesWithinTheHitRadiusOf(string path, string ship) =>
        world.RunUntilWithin(TheProjectile, ship == "raider" ? TheRaider : world.Sim.Ship, world.Spec.Number(path));

    [Then(@"the asteroid has ([\d.]+) integrity")]
    public void ThenTheAsteroidHasIntegrity(float integrity) => Assert.Equal(integrity, TheAsteroid.Integrity);

    [Then(@"the raider ship has ([\d.]+) hull")]
    public void ThenTheRaiderShipHasHull(float hull) => Assert.Equal(hull, TheRaider.Hull);

    [When(@"it is hit by a projectile from a weapon with a damage of ([\d.]+)")]
    public void WhenItIsHitByAProjectileWithADamageOf(float damage)
    {
        _projectile = world.FireAt(TheAsteroid, NewWeapon(damage), byPilot: true);
        WhenThePathPassesWithinTheAsteroidsRadius();
    }

    // An asteroid and a raider ship lie on the projectile's path to the pilot ship.
    [Given(@"a projectile fired by a raider ship from a weapon with a damage of ([\d.]+)")]
    public void GivenAProjectileFiredByARaiderShip(float damage)
    {
        world.Galaxy.AsteroidField(world.Galaxy.StartingSystem, 1);
        world.Galaxy.RaiderPack(world.Galaxy.StartingSystem, 1);
        _projectile = world.FireAt(world.Sim.Ship, NewWeapon(damage), byPilot: false);
        _raider = world.Sim.System.RaiderShips![0];
        _asteroid = world.Sim.System.Asteroids![0];
        _raider.Position = Vector3D.From(TheProjectile.Position.Lerp(world.Sim.Ship.Position.ToGodot(), 0.3f));
        _asteroid.Position = Vector3D.From(TheProjectile.Position.Lerp(world.Sim.Ship.Position.ToGodot(), 0.6f));
    }

    [When("the path of the projectile passes through an asteroid or another raider ship")]
    public void WhenThePathPassesThroughAnAsteroidOrAnotherRaiderShip() =>
        world.RunUntilThrough(TheProjectile, TheAsteroid, TheAsteroid.AsteroidType.Radius);

    [Then("the asteroid or raider ship is not damaged and the projectile flies on")]
    public void ThenTheAsteroidOrRaiderShipIsNotDamaged()
    {
        Assert.Equal(TheAsteroid.AsteroidType.MaxIntegrity, TheAsteroid.Integrity);
        Assert.Equal(TheRaider.RaiderType.ShipClass.MaxHull, TheRaider.Hull);
        Assert.Contains(TheProjectile, world.Sim.Projectiles);
    }

    [Then(@"the pilot ship loses ([\d.]+) hull")]
    public void ThenThePilotShipLosesHull(float hull) => Assert.Equal(Hull.MaxHull - hull, world.Sim.Ship.Hull);

    [Given("projectiles are in flight in a star system")]
    public void GivenProjectilesAreInFlightInAStarSystem()
    {
        GalaxyBuilder.Link(world.Galaxy.StartingSystem, world.Galaxy.System("elsewhere"));
        world.Sim.Ship.Fuel = Hull.FuelCapacity;
        world.Galaxy.Weapon(Hull);
        world.Galaxy.Weapon(Hull);
        world.Hold(Actions.Fire);
        world.Run(World.StepSeconds);
        world.Hold();
        _inFlight = [.. world.Sim.Projectiles];
        Assert.NotEmpty(_inFlight);
    }

    // The round trip is one When, so the Then only reads what each jump left.
    [When("the pilot ship jumps to another star system")]
    public void WhenThePilotShipJumpsToAnotherStarSystem()
    {
        var home = world.Sim.Ship.CurrentSystem;
        world.Sim.Press(Actions.Jump);
        Assert.True(home != world.Sim.Ship.CurrentSystem, "the pilot ship did not jump away");
        var away = world.Sim.Projectiles.ToList();
        world.Sim.Press(Actions.Jump);
        Assert.Same(home, world.Sim.Ship.CurrentSystem);
        _afterJumps = (away, [.. world.Sim.Projectiles]);
    }

    [Then("those projectiles are gone, also when the pilot ship comes back")]
    public void ThenThoseProjectilesAreGone()
    {
        Assert.Empty(_afterJumps.Away.Intersect(_inFlight));
        Assert.Empty(_afterJumps.Back.Intersect(_inFlight));
    }
}
