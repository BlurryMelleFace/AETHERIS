using Godot;
using System.Collections.Generic;

namespace Aetheris;

public partial class TreeAtmosphere : Node2D
{
    private static readonly Texture2D PineArt = GD.Load<Texture2D>("res://assets/foliage/pine_64x128.svg");
    private static readonly Shader FoliageShader = GD.Load<Shader>("res://assets/foliage/tree_wind.gdshader");
    private readonly List<Vector2> _roots = new();
    private readonly List<ShaderMaterial> _materials = new();
    private World _world = null!;
    private DayNightCycle? _cycle;
    private double _seconds;

    public void Setup(World world, IEnumerable<Vector2> roots)
    {
        _world = world;
        _roots.AddRange(roots);
    }

    public override void _Ready()
    {
        _cycle = _world.GetParent().GetNodeOrNull<DayNightCycle>("DayNightCycle");
        foreach (Vector2 root in _roots)
        {
            int column = Mathf.FloorToInt(root.X / World.TileSize);
            float height = 115 + column % 7 * 10;
            float width = 72 + column % 3 * 8;
            var material = new ShaderMaterial { Shader = FoliageShader };
            var tree = new Sprite2D
            {
                Texture = PineArt,
                Position = root,
                Offset = new Vector2(0, -64),
                Scale = new Vector2(width / 64f, height / 128f),
                Material = material
            };
            material.SetShaderParameter("tree_phase", column * 0.73f);
            AddChild(tree);
            _materials.Add(material);
        }
        _Process(0);
    }

    public static float GustAt(float worldX, double seconds)
    {
        double localTime = seconds - worldX / 180.0;
        int nearest = (int)System.Math.Floor(localTime / 23.0);
        float gust = 0f;
        for (int eventIndex = nearest - 1; eventIndex <= nearest + 1; eventIndex++)
        {
            float variation = Hash(eventIndex + 19);
            double center = eventIndex * 23.0 + 6.0 + variation * 5.0;
            float distance = (float)System.Math.Abs(localTime - center) / (4f + Hash(eventIndex + 71) * 3f);
            float envelope = Mathf.Max(0f, 1f - distance * distance);
            gust += envelope * envelope * (0.18f + variation * 0.62f);
        }
        return gust;
    }

    public static float WindAt(float worldX, double seconds)
    {
        float time = (float)seconds;
        float breeze = 0.07f * Mathf.Sin(time * 0.43f + worldX * 0.004f)
            + 0.045f * Mathf.Sin(time * 0.71f - worldX * 0.009f + 2.1f);
        return 0.08f + breeze + GustAt(worldX, seconds);
    }

    private static float Hash(int value) => Mathf.PosMod(Mathf.Sin(value * 127.1f + 31.7f) * 43758.5453f, 1f);

    public override void _Process(double delta)
    {
        _seconds += delta;
        float sunDirection = Mathf.Cos((_cycle?.Phase ?? 0.25f) * Mathf.Tau);
        for (int index = 0; index < _materials.Count; index++)
        {
            ShaderMaterial material = _materials[index];
            material.SetShaderParameter("wind_time", (float)_seconds);
            material.SetShaderParameter("wind_strength", WindAt(_roots[index].X, _seconds));
            material.SetShaderParameter("sun_direction", sunDirection);
            material.SetShaderParameter("daylight", _cycle?.Daylight ?? 1f);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float daylight = _cycle?.Daylight ?? 1f;
        float direction = Mathf.Cos((_cycle?.Phase ?? 0.25f) * Mathf.Tau);
        foreach (Vector2 root in _roots)
        {
            int column = Mathf.FloorToInt(root.X / World.TileSize);
            int ground = Mathf.RoundToInt(root.Y / World.TileSize);
            if (!_world.IsSolid(column, ground)) continue;
            float length = 22f + Mathf.Abs(direction) * 62f;
            float wind = WindAt(root.X, _seconds);
            for (int sample = -2; sample <= 12; sample++)
            {
                float progress = (sample + 2) / 14f;
                float horizontal = root.X + direction * (progress * length - 8f)
                    + wind * progress * 4f;
                int shadowColumn = Mathf.FloorToInt(horizontal / World.TileSize);
                if (!_world.IsSolid(shadowColumn, ground) || _world.IsSolid(shadowColumn, ground - 1)) continue;
                float opacity = (0.045f + daylight * 0.075f) * (1f - progress);
                DrawRect(new Rect2(horizontal - 5f, root.Y + 1f, 10f, 5f),
                    new Color(0.10f, 0.16f, 0.17f, opacity));
                DrawRect(new Rect2(horizontal - 3f, root.Y + 1f, 6f, 2f),
                    new Color(0.08f, 0.13f, 0.14f, opacity));
            }
        }

        float width = World.Width * World.TileSize;
        for (int wisp = 0; wisp < 12; wisp++)
        {
            float horizontal = Mathf.PosMod((float)(_seconds * 180.0) + wisp * 317f, width);
            float gust = GustAt(horizontal, _seconds);
            if (gust < 0.22f) continue;
            float vertical = 300f + wisp % 4 * 27f + Mathf.Sin((float)_seconds * 0.7f + wisp) * 9f;
            float length = 18f + gust * 28f;
            for (int segment = 0; segment < 4; segment++)
            {
                float start = segment / 4f;
                float end = (segment + 1) / 4f;
                Vector2 from = new(horizontal + length * start, vertical + Mathf.Sin(start * Mathf.Pi) * 3f);
                Vector2 to = new(horizontal + length * end, vertical + Mathf.Sin(end * Mathf.Pi) * 3f);
                float opacity = (gust - 0.22f) * 0.18f * (0.3f + daylight * 0.7f)
                    * Mathf.Sin((segment + 0.5f) / 4f * Mathf.Pi);
                DrawLine(from, to, new Color(0.83f, 0.91f, 0.83f, opacity), 1f, true);
            }
        }
    }
}