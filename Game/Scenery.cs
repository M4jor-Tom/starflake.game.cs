using System;
using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>Greybox visuals built from primitive meshes; swap for real assets later.</summary>
public static class Scenery
{
    /// <summary>Hit radius of every ship, matching <see cref="ShipMesh"/>.</summary>
    public const float ShipRadius = 12;

    public static readonly Color EnemyColor = new(1f, 0.35f, 0.2f);

    static readonly Mesh ShipMesh = new PrismMesh { Size = new Vector3(8, 20, 4) };
    static readonly Material PlayerHull = Lit(new Color(0.3f, 0.85f, 1f));
    static readonly Material EnemyHull = Lit(EnemyColor);
    static readonly Mesh RockMesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 8, Rings = 5, Material = Lit(new Color(0.45f, 0.42f, 0.4f)) };
    static readonly Mesh LootMesh = new BoxMesh { Size = new Vector3(6, 6, 6), Material = Glow(new Color(0.3f, 1f, 0.5f)) };
    static readonly Mesh PlayerBolt = new BoxMesh { Size = new Vector3(0.6f, 0.6f, 10), Material = Glow(new Color(1f, 0.9f, 0.3f)) };
    static readonly Mesh EnemyBolt = new BoxMesh { Size = new Vector3(0.8f, 0.8f, 8), Material = Glow(new Color(1f, 0.25f, 0.2f)) };
    static readonly Mesh SparkMesh = new SphereMesh { Radius = 4, Height = 8, Material = Glow(new Color(1f, 0.8f, 0.5f)) };

    public static WorldEnvironment Space() => new()
    {
        Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.01f, 0.01f, 0.03f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.4f, 0.4f, 0.5f),
            AmbientLightEnergy = 0.5f,
        },
    };

    /// <summary>Distant stars; keep it centred on the camera so they never get closer.</summary>
    public static MultiMeshInstance3D Starfield(Random rng)
    {
        var stars = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new SphereMesh { Radius = 20, Height = 40, RadialSegments = 4, Rings = 2, Material = Glow(Colors.White) },
            InstanceCount = 800,
        };
        for (var i = 0; i < stars.InstanceCount; i++)
            stars.SetInstanceTransform(i, new Transform3D(Basis.Identity, RandomDirection(rng) * 25000));
        return new MultiMeshInstance3D { Multimesh = stars };
    }

    public static Node3D Star(StarSystem system)
    {
        var color = StarColor(system.StarClass);
        var radius = StarRadius(system.StarClass);
        var star = new Node3D();
        star.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = radius, Height = 2 * radius, Material = Glow(color) } });
        star.AddChild(new OmniLight3D
        {
            LightColor = color,
            LightEnergy = 1.5f,
            OmniRange = system.BoundsRadius * 3,
            OmniAttenuation = 0,
        });
        return star;
    }

    public static MeshInstance3D Ship(bool player) => new()
    {
        Mesh = ShipMesh,
        MaterialOverride = player ? PlayerHull : EnemyHull,
        RotationDegrees = new Vector3(-90, 0, 0), // prism apex along -Z, the node's forward
    };

    /// <summary>A diamond drawn at constant screen size and through everything, so far ships stay findable.</summary>
    public static Label3D EnemyMarker() => new()
    {
        Text = "◆",
        Modulate = EnemyColor,
        FontSize = 32,
        OutlineSize = 0,
        FixedSize = true,
        PixelSize = 0.0025f,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
    };

    public static MeshInstance3D Asteroid(AsteroidType type, Random rng) => new()
    {
        Mesh = RockMesh,
        Scale = type.Radius * new Vector3(Stretch(rng), Stretch(rng), Stretch(rng)),
    };

    public static MeshInstance3D Loot() => new() { Mesh = LootMesh };

    public static MeshInstance3D Bolt(bool byPlayer) => new() { Mesh = byPlayer ? PlayerBolt : EnemyBolt };

    /// <summary>A short flash where a bolt hit; it frees itself, or goes with its parent.</summary>
    public static void Spark(Node parent, Vector3 at)
    {
        var spark = new MeshInstance3D { Mesh = SparkMesh, Position = at };
        parent.AddChild(spark);
        spark.CreateTween().TweenCallback(Callable.From(spark.QueueFree)).SetDelay(0.12);
    }

    public static Vector3 RandomDirection(Random rng) =>
        new Vector3(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f).Normalized();

    static float Stretch(Random rng) => 0.8f + 0.4f * rng.NextSingle();

    static StandardMaterial3D Glow(Color color) =>
        new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = color };

    static StandardMaterial3D Lit(Color color) => new() { AlbedoColor = color, Roughness = 0.9f };

    static float StarRadius(SpectralClass c) => c switch
    {
        SpectralClass.O or SpectralClass.B => 400,
        SpectralClass.M => 120,
        _ => 220,
    };

    static Color StarColor(SpectralClass c) => c switch
    {
        SpectralClass.O => new Color(0.6f, 0.7f, 1f),
        SpectralClass.B => new Color(0.7f, 0.8f, 1f),
        SpectralClass.A => new Color(0.9f, 0.92f, 1f),
        SpectralClass.F => new Color(1f, 1f, 0.9f),
        SpectralClass.G => new Color(1f, 0.93f, 0.7f),
        SpectralClass.K => new Color(1f, 0.75f, 0.45f),
        _ => new Color(1f, 0.5f, 0.35f),
    };
}
