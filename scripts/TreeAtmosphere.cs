using Godot;
using System.Collections.Generic;

namespace Aetheris;

public partial class TreeAtmosphere : Node2D
{
    private static readonly Shader FoliageShader = GD.Load<Shader>("res://assets/foliage/tree_wind.gdshader");
    private readonly List<TreeInstance> _trees = new();
    private readonly Dictionary<TreeInstance, (Sprite2D sprite, ShaderMaterial material)> _visuals = new();
    private World _world = null!;
    private DayNightCycle? _cycle;
    private double _seconds;

    public void Setup(World world, IEnumerable<TreeInstance> trees)
    {
        _world = world;
        _trees.AddRange(trees);
    }

    public override void _Ready()
    {
        _cycle = _world.GetParent().GetNodeOrNull<DayNightCycle>("DayNightCycle");
        foreach (TreeInstance tree in _trees)
        {
            var material = new ShaderMaterial { Shader = FoliageShader };
            Texture2D texture = tree.Variant.Texture;
            var sprite = new Sprite2D
            {
                Texture = texture,
                Position = tree.Root,
                Offset = new Vector2(0, -texture.GetHeight() / 2f + TreeCatalog.Embed),
                Material = material,
                ZIndex = -1 // trees stay in the background, behind the ground tiles
            };
            material.SetShaderParameter("tree_phase", tree.Id * 0.73f);
            material.SetShaderParameter("sway_pixels", tree.Variant.Sway);
            material.SetShaderParameter("leaf_start", tree.Variant.LeafStart);
            AddChild(sprite);
            _visuals[tree] = (sprite, material);
        }
        _world.TreeFelled += tree =>
        {
            if (_visuals.Remove(tree, out var visual)) visual.sprite.QueueFree();
        };
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
        foreach (var (tree, visual) in _visuals)
        {
            ShaderMaterial material = visual.material;
            material.SetShaderParameter("wind_time", (float)_seconds);
            material.SetShaderParameter("wind_strength", WindAt(tree.Root.X, _seconds));
            material.SetShaderParameter("sun_direction", sunDirection);
            material.SetShaderParameter("daylight", _cycle?.Daylight ?? 1f);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float daylight = _cycle?.Daylight ?? 1f;
        float direction = Mathf.Cos((_cycle?.Phase ?? 0.25f) * Mathf.Tau);
        foreach (TreeInstance tree in _trees)
        {
            if (tree.Felled) continue;
            Vector2 root = tree.Root;
            int ground = tree.BaseRow;
            float length = 50f + Mathf.Abs(direction) * 120f;
            float wind = WindAt(root.X, _seconds);
            for (int sample = -2; sample <= 12; sample++)
            {
                float progress = (sample + 2) / 14f;
                float horizontal = root.X + direction * (progress * length - 24f)
                    + wind * progress * 8f;
                int shadowColumn = Mathf.FloorToInt(horizontal / World.TileSize);
                if (!_world.IsSolid(shadowColumn, ground) || _world.IsSolid(shadowColumn, ground - 1)) continue;
                float opacity = (0.045f + daylight * 0.075f) * (1f - progress);
                DrawRect(new Rect2(horizontal - 14f, root.Y + 1f, 28f, 8f),
                    new Color(0.10f, 0.16f, 0.17f, opacity));
                DrawRect(new Rect2(horizontal - 8f, root.Y + 1f, 16f, 3f),
                    new Color(0.08f, 0.13f, 0.14f, opacity));
            }
        }

        float width = World.Width * World.TileSize;
        for (int wisp = 0; wisp < 16; wisp++)
        {
            float horizontal = Mathf.PosMod((float)(_seconds * 180.0) + wisp * 317f, width);
            float gust = GustAt(horizontal, _seconds);
            if (gust < 0.22f) continue;
            float vertical = 140f + wisp % 6 * 45f + Mathf.Sin((float)_seconds * 0.7f + wisp) * 9f;
            float length = 30f + gust * 50f;
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