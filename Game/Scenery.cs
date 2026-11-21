using System;
using Godot;
using Starflake.Domain;

namespace Starflake.Game;

/// <summary>Greybox visuals built from primitive meshes; swap for real assets later.</summary>
public static class Scenery
{
    public static readonly Color RaiderColor = new(1f, 0.35f, 0.2f);

    static readonly Mesh ShipMesh = new PrismMesh { Size = new Vector3(8, 20, 4) };
    static readonly Material PilotHull = Lit(new Color(0.3f, 0.85f, 1f));
    static readonly Material RaiderHull = Lit(RaiderColor);
    static readonly Mesh RockMesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 8, Rings = 5, Material = Lit(new Color(0.45f, 0.42f, 0.4f)) };
    static readonly Mesh FuelPodMesh = new BoxMesh { Size = new Vector3(6, 6, 6), Material = Glow(new Color(0.3f, 1f, 0.5f)) };
    static readonly Mesh PilotBolt = new BoxMesh { Size = new Vector3(0.6f, 0.6f, 10), Material = Glow(new Color(1f, 0.9f, 0.3f)) };
    static readonly Mesh RaiderBolt = new BoxMesh { Size = new Vector3(0.8f, 0.8f, 8), Material = Glow(new Color(1f, 0.25f, 0.2f)) };
    static readonly Mesh SparkMesh = new SphereMesh { Radius = 4, Height = 8, Material = Glow(new Color(1f, 0.8f, 0.5f)) };

    /// <summary>How fast a fuel pod turns about its up axis, in radians per second.</summary>
    public static readonly Vector3 FuelPodSpin = 2 * Vector3.Up;

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

    public static MeshInstance3D Ship(bool pilot) => new()
    {
        Mesh = ShipMesh,
        MaterialOverride = pilot ? PilotHull : RaiderHull,
        RotationDegrees = new Vector3(-90, 0, 0), // prism apex along -Z, the node's forward
    };

    /// <summary>A diamond drawn at constant screen size and through everything, so far ships stay findable.</summary>
    public static Label3D RaiderMarker() => new()
    {
        Text = "◆",
        Modulate = RaiderColor,
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

    /// <summary>A random tumble for an asteroid: an axis scaled by radians per second.</summary>
    public static Vector3 AsteroidSpin(Random rng) => RandomDirection(rng) * (0.1f + 0.3f * rng.NextSingle());

    public static MeshInstance3D FuelPod() => new() { Mesh = FuelPodMesh };

    public static MeshInstance3D Bolt(bool byPilot) => new() { Mesh = byPilot ? PilotBolt : RaiderBolt };

    /// <summary>A flash that frees itself an instant after it is added.</summary>
    public static void Spark(Node parent, Vector3 at)
    {
        var spark = new MeshInstance3D { Mesh = SparkMesh, Position = at };
        parent.AddChild(spark);
        spark.CreateTween().TweenCallback(Callable.From(spark.QueueFree)).SetDelay(0.12);
    }

    /// <summary>A HUD text placed on the whole screen at an alignment, outlined so it reads over the star.</summary>
    public static Label HudLabel(HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var label = new Label
        {
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        return label;
    }

    /// <summary>The margin between the HUD labels and the screen edge, in pixels.</summary>
    public const int HudMargin = 16;

    static Vector3 RandomDirection(Random rng) =>
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
