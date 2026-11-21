using Godot;
using Reqnroll;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

[Binding]
public sealed class RaiderAiSteps(World world)
{
    // Short runs: the pilot ship stays where each When put it, relative to the raider ship.
    const float Seconds = 0.25f, FiringSeconds = 1.5f;
    const float DetectedAt = 1000;

    RaiderShip? _raider;
    RaiderIntent _intent;                  // what the raider decided for the placement of the last When
    Pose _before;                          // the raider ship when that When began
    int _firedBefore;                      // world.Fired when that When began
    readonly List<WeaponType> _weapons = [];

    RaiderShip Raider => _raider ?? throw new InvalidOperationException("no raider ship given");

    PilotShip Pilot => world.Sim.Ship;

    RaiderType Type => Raider.RaiderType;

    float TurnRate => Type.ShipClass.TurnRate;

    float Gain => world.Spec.Number("raider_ai.steering_gain");

    float AimCone => world.Spec.Number("raider_ai.aim_cone");

    // At rest at the origin with the default attitude, armed so that "does not fire" is a decision, not a lack of guns.
    void GivenARaider(float detectionRange, float? keepDistance = null)
    {
        var pack = world.Galaxy.RaiderPack(world.Galaxy.StartingSystem, 1);
        pack.RaiderType.DetectionRange = detectionRange;
        if (keepDistance is { } keep)
            pack.RaiderType.KeepDistance = keep;
        _raider = world.Sim.System.RaiderShips![0];
        _raider.Position = Vector3D.From(Vector3.Zero);
        _raider.Velocity = Vector3D.From(Vector3.Zero);
        _raider.Orientation = Attitude.From(Quaternion.Identity);
        world.Galaxy.Weapon(Type.ShipClass).MaxRange = detectionRange * 2;
    }

    // The pilot ship goes at the offset from the raider ship, then one run of the simulation.
    void Place(Vector3 offset, float seconds = Seconds)
    {
        Pilot.Position = Vector3D.From(Raider.Position.ToGodot() + offset);
        Pilot.Velocity = Vector3D.From(Vector3.Zero);
        _intent = RaiderAi.Decide(Raider, Pilot, world.Spec.Tuning.RaiderAi, RaiderAi.LongestRange(Type.ShipClass));
        _before = Pose.Of(Raider);
        _firedBefore = world.Fired.Count;
        world.Hold();
        world.Run(seconds);
    }

    // Along the raider ship's own axes: -Z is straight ahead.
    void PlaceLocal(Vector3 direction, float distance, float seconds = Seconds) =>
        Place(Pose.Of(Raider).Attitude * direction.Normalized() * distance, seconds);

    float AngleToPilot(Pose raider) => raider.Nose.AngleTo(Pilot.Position.ToGodot() - raider.Position);

    IEnumerable<Projectile> RaiderShots => world.Fired.Skip(_firedBefore).Where(f => !f.Projectile.ByPilot).Select(f => f.Projectile);

    [Given(@"a raider ship whose type has a detection range of ([\d.]+) metres")]
    public void GivenADetectionRange(float range) => GivenARaider(range);

    [Given(@"a raider ship whose type has a detection range of ([\d.]+) metres and a keep distance of ([\d.]+) metres")]
    public void GivenADetectionRangeAndKeepDistance(float range, float keep) => GivenARaider(range, keep);

    [When(@"the pilot ship is farther than ([\d.]+) metres from it")]
    public void WhenFartherThan(float metres) => Place(Vector3.Forward * (metres + 500));

    [When(@"the pilot ship is exactly ([\d.]+) metres from it")]
    public void WhenExactlyFrom(float metres) => Place(Vector3.Forward * metres);

    // Off the nose, so that turning toward it can be seen.
    [When(@"the pilot ship is closer than ([\d.]+) metres to it and farther than ([\d.]+) metres")]
    public void WhenBetween(float far, float near) => PlaceLocal(new Vector3(1, 0, -2), (far + near) / 2);

    [When(@"the pilot ship is closer to it than ([\d.]+) metres")]
    public void WhenCloserThan(float metres) => PlaceLocal(new Vector3(1, 0, -2), metres / 2);

    [Then("the raider ship does not turn")]
    public void ThenDoesNotTurn()
    {
        Assert.Equal(Vector3.Zero, _intent.Turn);
        Assert.Equal(_before.Attitude, Pose.Of(Raider).Attitude);
    }

    [Then("^the raider ship (?:still )?does not thrust$")]
    public void ThenDoesNotThrust()
    {
        Assert.Equal(0, _intent.Throttle);
        Assert.Equal(Vector3.Zero, Pose.Of(Raider).Velocity);
    }

    [Then("the raider ship does not fire")]
    public void ThenDoesNotFire()
    {
        Assert.False(_intent.Fire);
        Assert.Empty(RaiderShots);
    }

    [Then("the raider ship still does not turn, thrust or fire")]
    public void ThenStillDoesNothing()
    {
        ThenDoesNotTurn();
        ThenDoesNotThrust();
        ThenDoesNotFire();
    }

    [Then("the raider ship turns its nose toward the pilot ship")]
    public void ThenTurnsTowardThePilotShip()
    {
        Assert.NotEqual(Vector3.Zero, _intent.Turn);
        Assert.True(AngleToPilot(Pose.Of(Raider)) < AngleToPilot(_before), "the nose angle to the pilot ship did not shrink");
    }

    [Then("the raider ship thrusts at full throttle")]
    public void ThenThrustsAtFullThrottle()
    {
        Assert.Equal(1, _intent.Throttle);
        Assert.True(Pose.Of(Raider).Velocity.Length() > 0, "the raider ship did not speed up");
    }

    // Ahead of the nose, off it on both axes, so that one input is capped and the other is not.
    [Given("a raider ship that has detected the pilot ship ahead of it")]
    public void GivenDetectedAhead()
    {
        GivenARaider(DetectedAt * 2);
        PlaceLocal(new Vector3(0.8f, 0.05f, -1), DetectedAt);
    }

    [Then(@"its pitch and yaw inputs are the sideways offsets of the pilot ship's direction multiplied by the ""raider_ai.steering_gain""")]
    public void ThenInputsAreOffsetsTimesGain()
    {
        var direction = (Pilot.Position.ToGodot() - _before.Position).Normalized();
        Assert.InRange(_intent.Turn.X, direction.Y * Gain - World.Tolerance, direction.Y * Gain + World.Tolerance);
        Assert.Equal(Mathf.Clamp(-direction.X * Gain, -1, 1), _intent.Turn.Y);
    }

    [Then("each input is capped at a full turn input")]
    public void ThenInputsAreCapped()
    {
        var direction = (Pilot.Position.ToGodot() - _before.Position).Normalized();
        Assert.True(MathF.Abs(direction.X * Gain) > 1, "the placement does not call for a capped input");
        Assert.All([_intent.Turn.X, _intent.Turn.Y], input => Assert.InRange(input, -1, 1));
        Assert.Equal(-1, _intent.Turn.Y); // the pilot ship is on the right
    }

    [Then("the raider ship never rolls")]
    public void ThenNeverRolls() => Assert.Equal(0, _intent.Turn.Z);

    [Given("a raider ship that has detected the pilot ship behind it")]
    public void GivenDetectedBehind() => GivenARaider(DetectedAt * 2);

    [When("the pilot ship is on its right")]
    public void WhenOnItsRight() => PlaceLocal(new Vector3(1, 0, 1), DetectedAt);

    [When("the pilot ship is straight behind it")]
    public void WhenStraightBehind() => PlaceLocal(Vector3.Back, DetectedAt);

    [When("the pilot ship is on its left")]
    public void WhenOnItsLeft() => PlaceLocal(new Vector3(-1, 0, 1), DetectedAt);

    [Then("^the raider ship yaws (right|left) at the turn rate of its ship class, without pitching$")]
    public void ThenYawsAtTheTurnRate(string side)
    {
        var yaw = side == "right" ? -1 : 1; // positive yaw swings the nose left
        Assert.Equal(new Vector3(0, yaw, 0), _intent.Turn);
        var delta = _before.Attitude.Inverse() * Pose.Of(Raider).Attitude;
        Assert.InRange(delta.GetAngle(), TurnRate * Seconds - World.Tolerance, TurnRate * Seconds + World.Tolerance);
        Assert.Equal(yaw, MathF.Sign(delta.GetEuler().Y));
        Assert.InRange(delta.GetEuler().X, -World.Tolerance, World.Tolerance);
    }

    // A far keep distance keeps the raider ship at the origin, so that the distances below are exact.
    [Given("a raider ship that has detected the pilot ship")]
    public void GivenDetected() => GivenARaider(DetectedAt * 3, DetectedAt * 3);

    [Given(@"the longest max range among the weapons of its ship class is ([\d.]+) metres")]
    public void GivenTheLongestMaxRange(float range)
    {
        var hull = Type.ShipClass;
        hull.Weapons!.Clear();
        foreach (var (maxRange, fireRate) in new[] { (range, 4f), (range / 2, 2f) }) // the longest is not the first
        {
            var weapon = world.Galaxy.Weapon(hull);
            (weapon.MaxRange, weapon.FireRate) = (maxRange, fireRate);
            _weapons.Add(weapon);
        }
    }

    [When(@"the pilot ship is closer than ([\d.]+) metres and the angle between the raider's nose and the pilot ship's direction is less than the ""raider_ai.aim_cone""")]
    public void WhenInRangeAndInTheCone(float range)
    {
        var angle = AimCone / 2;
        PlaceLocal(new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle)), range / 2, FiringSeconds);
    }

    [When(@"the pilot ship is ([\d.]+) metres away or farther")]
    public void WhenOutOfRange(float range) => Place(Vector3.Forward * range, World.StepSeconds); // one step: the decision, before the raider turns

    [When(@"the angle between the raider's nose and the pilot ship's direction is the ""raider_ai.aim_cone"" or more")]
    public void WhenOutOfTheCone()
    {
        var angle = AimCone * 2;
        PlaceLocal(new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle)), DetectedAt, World.StepSeconds); // one step, as above
    }

    [Then("the raider ship fires every weapon of its ship class, each at its own fire rate")]
    public void ThenFiresEveryWeapon()
    {
        Assert.True(_intent.Fire);
        Assert.All(_weapons, weapon =>
        {
            var shots = RaiderShots.Count(p => p.Weapon == weapon);
            Assert.InRange(shots, weapon.FireRate * FiringSeconds - 1, weapon.FireRate * FiringSeconds + 1);
        });
    }
}
