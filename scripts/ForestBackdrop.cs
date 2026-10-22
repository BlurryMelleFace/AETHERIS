using Godot;

namespace Aetheris;

/// <summary>
/// Editable procedural parallax landscape, drawn from independent geometric layers.
/// Every color is exposed in the Inspector; shared CanvasModulate grades all layers.
/// No external panorama texture required.
/// </summary>
[Tool]
public partial class ForestBackdrop : Node2D
{
    [ExportGroup("Composition")]
    [Export] public float HorizonY { get; set; } = 400f;
    [Export] public float BaseWidth { get; set; } = 1600f;
    [Export] public float BaseHeight { get; set; } = 1000f;
    [Export] public int Seed { get; set; } = 8132;

    [ExportGroup("Depth / Parallax")]
    [Export(PropertyHint.Range, "0,0.25,0.01")] public float MountainParallax { get; set; } = 0.035f;
    [Export(PropertyHint.Range, "0,0.45,0.01")] public float ForestParallax { get; set; } = 0.13f;
    [Export(PropertyHint.Range, "0,0.65,0.01")] public float NearForestParallax { get; set; } = 0.26f;

    [ExportGroup("Palette")]
    [Export] public Color SkyTop { get; set; } = new("#263951");
    [Export] public Color SkyHorizon { get; set; } = new("#ba8584");
    [Export] public Color Snow { get; set; } = new("#c0cbd8");
    [Export] public Color DistantMountain { get; set; } = new("#52617b");
    [Export] public Color DistantForest { get; set; } = new("#2f4d53");
    [Export] public Color NearForest { get; set; } = new("#203e3d");
    [Export] public Color Mist { get; set; } = new(0.68f, 0.73f, 0.78f, 0.14f);

    [ExportGroup("Animation")]
    [Export] public float MistSpeed { get; set; } = 4f;

    private Node2D? _player;
    private float _time;

    public override void _Ready()
    {
        ZIndex = -10;
        _player = GetParent()?.GetNodeOrNull<Node2D>("Player");
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float px = Engine.IsEditorHint() ? 400f : (_player?.GlobalPosition.X ?? 400f);
        float py = Engine.IsEditorHint() ? 400f : (_player?.GlobalPosition.Y ?? 400f);
        // World-space anchored viewport coverage, overscanned for camera zoom.
        float left = px - BaseWidth / 2f;
        float top = py - 600f;
        for (int i = 0; i < 28; i++)
        {
            float t = i / 27f;
            DrawRect(new Rect2(left, top + i * BaseHeight / 28f,
                BaseWidth, BaseHeight / 28f + 2),
                SkyTop.Lerp(SkyHorizon, t * 0.84f));
        }

        // Far snow-capped peaks with their own silhouette and cap geometry.
        for (int i = -4; i < 11; i++)
        {
            float center = (i * 280f) + px * (1f - MountainParallax);
            float peakY = HorizonY - 330f - Hash(i, 5) * 170f + (py - 400f);
            float footY = HorizonY + 255f + (py - 400f);
            float halfWidth = 170f + Hash(i, 2) * 75f;
            DrawColoredPolygon(new Vector2[] {
                new(center - halfWidth, footY), new(center, peakY),
                new(center + halfWidth, footY)
            }, DistantMountain);
            DrawColoredPolygon(new Vector2[] {
                new(center - halfWidth * .28f, peakY + (footY - peakY) * .31f),
                new(center, peakY),
                new(center + halfWidth * .30f, peakY + (footY - peakY) * .31f),
                new(center + halfWidth * .08f, peakY + (footY - peakY) * .21f),
                new(center - halfWidth * .12f, peakY + (footY - peakY) * .25f)
            }, Snow);
        }

        ForestBand(px, py, DistantForest, ForestParallax, 425f, 85f, 48f);
        ForestBand(px, py, NearForest, NearForestParallax, 565f, 115f, 64f);

        for (int i = 0; i < 5; i++)
        {
            float y = py + 30f + i * 34f;
            float drift = Mathf.Sin(_time * .19f + i) * 13f + _time * MistSpeed % 65f;
            DrawRect(new Rect2(left - 50 + drift, y, BaseWidth + 100, 28), Mist);
        }
    }

    private void ForestBand(float px, float py, Color color, float parallax,
        float baseline, float height, float spacing)
    {
        float offset = px * (1f - parallax);
        for (int i = -12; i < 42; i++)
        {
            float x = i * spacing + offset;
            float ground = py - 400f + baseline
                + Mathf.Sin(i * .32f) * 38f;
            float h = height * (.68f + Hash(i, 12) * .65f);
            float w = 19f + h * .20f;
            DrawLine(new Vector2(x, ground),new Vector2(x, ground-h),
                color.Darkened(.26f), 5f);
            for (int layer = 0; layer < 3; layer++)
            {
                float ly = ground - h + layer * h * .23f;
                float lw = w * (.55f + layer * .27f);
                DrawColoredPolygon(new Vector2[] {
                    new(x, ly - h * .24f),
                    new(x - lw, ly + h * .25f),
                    new(x + lw, ly + h * .25f)
                },color);
            }
        }
    }

    private float Hash(int a,int b)
    {
        float value = Mathf.Sin((a * 127.1f + b * 311.7f + Seed * .01f) * .12f)
            * 43758.5453f;
        return Mathf.PosMod(value, 1f);
    }
}
