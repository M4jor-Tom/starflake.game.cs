using Godot;
using Starflake.Design;
using Starflake.Domain;
using Starflake.Sim;

namespace Starflake.Tests.Steps;

/// <summary>
/// The context of one scenario, injected into every step class: a galaxy the Givens shape,
/// the real synced spec bound to it, and the simulation, started on first use.
/// </summary>
public sealed class World
{
    public const float Tolerance = 1e-3f;
    public const float StepSeconds = 1f / 60;

    Simulation? _sim;
    HashSet<string> _held = [];

    public World()
    {
        Galaxy = new GalaxyBuilder(Specs.SessionValue("starting_ship_class"), Specs.SessionValue("starting_system"));
        Spec = Specs.Load(Galaxy.Galaxy);
    }

    /// <summary>The real spec; its session's starting ship class and system are this world's.</summary>
    public Spec Spec { get; }

    public GalaxyBuilder Galaxy { get; }

    public FakeKeyLabels Keys { get; } = new();

    public Simulation Sim => _sim ??= Simulation.Start(Galaxy.Galaxy, Spec, new Random(1), Keys);

    /// <summary>The pilot ship's pose when the last <see cref="Run"/> began.</summary>
    public Pose Before { get; private set; }

    /// <summary>The pilot ship's speed when the last <see cref="Run"/> began, then after each of its steps.</summary>
    public List<float> Speeds { get; } = [];

    /// <summary>Sets the hold actions that are down during <see cref="Run"/>.</summary>
    public void Hold(params string[] actionIds) => _held = [.. actionIds];

    /// <summary>Steps the simulation for the given time in fixed steps of 1/60 s.</summary>
    public void Run(float seconds)
    {
        var input = new PilotInput(_held);
        Before = Pose.Of(Sim.Ship);
        Speeds.Clear();
        Speeds.Add(Before.Velocity.Length());
        for (var i = 0; i < StepsIn(seconds); i++)
        {
            Sim.Step(StepSeconds, input);
            Speeds.Add(Pose.Of(Sim.Ship).Velocity.Length());
        }
    }

    public static int StepsIn(float seconds) => (int)MathF.Round(seconds / StepSeconds);

    /// <summary>Asserts a value measured over discrete steps.</summary>
    public static void AssertMeasured(float expected, float actual) =>
        Assert.InRange(actual, expected - Tolerance * 100, expected + Tolerance * 100);
}

/// <summary>Position, velocity and attitude of an entity, with the domain defaults for absent ones.</summary>
public readonly record struct Pose(Vector3 Position, Vector3 Velocity, Quaternion Attitude)
{
    public static Pose Of(SpaceEntity entity) => new(entity.Position.ToGodot(),
        entity.Velocity?.ToGodot() ?? Vector3.Zero, entity.Orientation?.ToGodot() ?? Quaternion.Identity);

    public Vector3 Nose => Attitude * Vector3.Forward;
}

/// <summary>A keyboard layout that prints each physical key under its own name.</summary>
public sealed class FakeKeyLabels : IKeyLabels
{
    public string LabelOf(PhysicalKey key) => key.ToString();
}

/// <summary>
/// A galaxy for one scenario: a starting ship class and star system under the session's ids,
/// plus whatever the Givens add. Values a scenario does not state are plausible defaults.
/// </summary>
public sealed class GalaxyBuilder
{
    public GalaxyBuilder(string startingShipClassId, string startingSystemId)
    {
        StartingShipClass = NewShipClass(startingShipClassId);
        StartingSystem = NewSystem(startingSystemId, 10000, 10);
        Galaxy = new Galaxy
        {
            Id = "test", Name = "Test", ShipClasses = [StartingShipClass], StarSystems = [StartingSystem],
            RaiderTypes = [], AsteroidTypes = [],
        };
    }

    public Galaxy Galaxy { get; }

    public ShipClass StartingShipClass { get; }

    public StarSystem StartingSystem { get; }

    public ShipClass ShipClass(string id)
    {
        var shipClass = NewShipClass(id);
        Galaxy.ShipClasses!.Add(shipClass);
        return shipClass;
    }

    public StarSystem System(string id, float boundsRadius = 10000, float jumpCost = 10)
    {
        var system = NewSystem(id, boundsRadius, jumpCost);
        Galaxy.StarSystems.Add(system);
        return system;
    }

    public AsteroidField AsteroidField(StarSystem system, int count)
    {
        var type = new AsteroidType { Id = $"rock-{Galaxy.AsteroidTypes!.Count}", Name = "Rock", Radius = 20, FuelYield = 10, MaxIntegrity = 30 };
        Galaxy.AsteroidTypes.Add(type);
        var field = new AsteroidField { AsteroidType = type, Center = At(0, 0, -2000), Radius = 500, Count = count };
        (system.AsteroidFields ??= []).Add(field);
        return field;
    }

    public RaiderPack RaiderPack(StarSystem system, int count)
    {
        var type = new RaiderType
        {
            Id = $"raider-{Galaxy.RaiderTypes!.Count}", Name = "Raider", ShipClass = ShipClass($"raider-hull-{Galaxy.RaiderTypes.Count}"),
            FuelYield = 20, DetectionRange = 2000, KeepDistance = 250,
        };
        Galaxy.RaiderTypes.Add(type);
        var pack = new RaiderPack { RaiderType = type, Center = At(0, 0, -3000), Radius = 300, Count = count };
        (system.RaiderPacks ??= []).Add(pack);
        return pack;
    }

    /// <summary>Links two star systems both ways, so a ship can jump from one to the other and back.</summary>
    public static void Link(StarSystem a, StarSystem b)
    {
        (a.JumpTargets ??= []).Add(b);
        (b.JumpTargets ??= []).Add(a);
    }

    static Vector3D At(float x, float y, float z) => new() { X = x, Y = y, Z = z };

    static ShipClass NewShipClass(string id) => new()
    {
        Id = id, Name = id, MaxHull = 100, MaxSpeed = 500, Acceleration = 20, TurnRate = 1,
        FuelCapacity = 100, ScoopRadius = 30, TractorRange = 200, TractorSpeed = 50,
    };

    static StarSystem NewSystem(string id, float boundsRadius, float jumpCost) => new()
    {
        Id = id, Name = id, StarClass = SpectralClass.G, BoundsRadius = boundsRadius, JumpFuelCost = jumpCost,
    };
}
