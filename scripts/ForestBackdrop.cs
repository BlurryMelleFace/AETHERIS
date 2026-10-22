using Godot;

namespace Aetheris;

/// <summary>
/// Editable layered pixel-art scenery. No flattened backdrop texture.
/// The existing CanvasModulate grades the whole world for day/night and seasons.
/// </summary>
[Tool]
public partial class ForestBackdrop : Node2D
{
    [ExportGroup("Composition")]
    [Export] public int Seed { get; set; } = 8132;
    [Export(PropertyHint.Range, "2,12,1")] public int PixelStep { get; set; } = 4;
    [Export] public float HorizonY { get; set; } = 405f;
    [Export] public float ViewWidth { get; set; } = 1500f;
    [Export] public float ViewHeight { get; set; } = 1100f;

    [ExportGroup("Parallax")]
    [Export(PropertyHint.Range, "0,0.25,0.005")] public float FarMountainParallax { get; set; } = 0.006f;
    [Export(PropertyHint.Range, "0,0.35,0.005")] public float MidMountainParallax { get; set; } = 0.015f;
    [Export(PropertyHint.Range, "0,0.5,0.005")] public float FarForestParallax { get; set; } = 0.035f;
    [Export(PropertyHint.Range, "0,0.7,0.005")] public float NearForestParallax { get; set; } = 0.07f;

    [ExportGroup("Colors")]
    [Export] public Color Clouds { get; set; } = new("#8e8290");
    [Export] public Color FarMountain { get; set; } = new("#46536d");
    [Export] public Color MidMountain { get; set; } = new("#59677c");
    [Export] public Color Snow { get; set; } = new("#bcc7d3");
    [Export] public Color FarForest { get; set; } = new("#33535a");
    [Export] public Color NearForest { get; set; } = new("#243f43");
    [Export] public Color Fog { get; set; } = new(0.76f, 0.83f, 0.85f, 0.12f);

    [ExportGroup("Motion")]
    [Export] public float CloudSpeed { get; set; } = 1.2f;
    [Export] public float MistSpeed { get; set; } = 2f;

    private float _time;

    public override void _Ready()
    {
        ZIndex = -10;
        // Distant scenery is not lit by the player's torch.
        LightMask = 2;
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 cameraCenter = Engine.IsEditorHint() ? new Vector2(400, 400)
            : ToLocal(GetViewport().GetCanvasTransform().AffineInverse() * (GetViewportRect().Size * 0.5f));
        float cameraX = cameraCenter.X;
        float cameraY = cameraCenter.Y;
        float left = cameraX - ViewWidth * .5f;
        float top = cameraY - 660f;
        int step = Mathf.Max(2, PixelStep);

        // SkyBackdrop draws the sky on its own camera-independent canvas.
        DrawClouds(left, top, cameraX, step);
        DrawRidge(left, cameraX, cameraY, FarMountainParallax,
            FarMountain, .76f, 94, step);
        DrawRidge(left, cameraX, cameraY, MidMountainParallax,
            MidMountain, 1.00f, 281, step);
        DrawForest(left, cameraX, cameraY, FarForestParallax,
            FarForest, 420f, 83f, 31f, step);
        DrawForest(left, cameraX, cameraY, NearForestParallax,
            NearForest, 500f, 123f, 47f, step);
        DrawMist(left, cameraY, step);
    }

    private void DrawClouds(float left, float top, float cameraX, int step)
    {
        for (int band = 0; band < 3; band++)
        {
            float offset = cameraX * .004f - _time * CloudSpeed * (1 + band * .3f);
            int first = Mathf.FloorToInt(offset / 170f) - 1;
            int count = Mathf.CeilToInt(ViewWidth / 170f) + 3;
            for (int i = first; i < first + count; i++)
            {
                float x = left + i * 170f - offset;
                float y = top + 160f + band * 63f + Hash(i, band) * 31f;
                Color tint = new Color(Clouds.R, Clouds.G, Clouds.B, .11f + band * .025f);
                for (int j = 0; j < 6; j++)
                {
                    int w = 19 + (int)(Hash(i * 19 + j, band) * 36);
                    float yy = y - (j is 2 or 3 ? 12 : 0);
                    DrawRect(new Rect2(x + j * 16, yy, w, 8 + (j % 3) * 4), tint);
                }
            }
        }
    }

    private void DrawRidge(float left, float cameraX, float cameraY,
        float parallax, Color color, float scale, int salt, int step)
    {
        float floor = cameraY - 400f + HorizonY + 195f;
        // Camera-relative sampling: layer displacement equals camera travel * parallax.
        // 0 means fully anchored to the camera, 1 means world-locked foreground.
        float sampleOffset = cameraX * parallax;
        // Pixel-stepped ridge, varying mountain profiles with reproducible noise.
        int first = Mathf.FloorToInt(sampleOffset / step) - 1;
        int count = Mathf.CeilToInt(ViewWidth / step) + 3;
        for (int column = first; column < first + count; column++)
        {
            float sample = column * step;
            float x = left + sample - sampleOffset;
            float big = Mathf.Pow(Mathf.Abs(Mathf.Sin(sample * .0022f + salt)), 6f);
            float secondary = Mathf.Pow(Mathf.Abs(Mathf.Sin(sample * .0054f + salt)), 7f);
            float rough = Mathf.Abs(Mathf.Sin(sample * .024f + salt)) * 28f;
            float height = scale * (120f + big * 250f + secondary * 96f + rough);
            float peak = floor - height;
            DrawRect(new Rect2(x, peak, step + 1, ViewHeight), color);
            // Irregular snow line: cap only the upper, colder portions.
            float cap = Mathf.Max(0f, (height - 195f) * .35f
                + (Hash(column, salt) - .5f) * 13f);
            if (cap > 3f)
                DrawRect(new Rect2(x, peak, step + 1, cap), Snow);
            if (column % 7 == 0 && cap > 15f)
                DrawRect(new Rect2(x, peak + cap + 10, step, 17),
                    color.Darkened(.23f));
        }
    }

    private void DrawForest(float left, float cameraX, float cameraY,
        float parallax, Color baseColor, float baseline, float height,
        float spacing, int step)
    {
        float layerOffset = cameraX * parallax;
        int count = Mathf.CeilToInt(ViewWidth / spacing) + 12;
        int first = Mathf.FloorToInt(layerOffset / spacing) - 4;
        for (int n = 0; n < count; n++)
        {
            int i = first + n;
            float x = left + (i * spacing - layerOffset);
            float ground = cameraY - 400f + baseline + Mathf.Sin(i * .34f) * 34f;
            float h = height * (.67f + Hash(i, 13) * .68f);
            DrawPine(x, ground, h, baseColor, step, i);
        }
    }

    private void DrawPine(float x, float ground, float height,
        Color baseColor, int step, int id)
    {
        float trunkWidth = step * 2f;
        DrawRect(new Rect2(x - trunkWidth / 2, ground - height * .25f,
            trunkWidth, height * .25f), baseColor.Darkened(.25f));
        for (int tier = 0; tier < 4; tier++)
        {
            float top = ground - height + tier * height * .205f;
            int rows = Mathf.Max(3, Mathf.RoundToInt(height * .35f / step));
            float maxWidth = height * (.18f + tier * .055f);
            for (int row = 0; row < rows; row++)
            {
                float width = maxWidth * row / rows;
                float y = top + row * step;
                for (float dx = -width; dx <= width; dx += step)
                {
                    if (Hash((int)(dx / step) + id * 7, row + tier * 37) < .10f)
                        continue;
                    Color shade = Hash(id + row, (int)dx + tier) > .73f
                        ? baseColor.Lightened(.16f) : baseColor;
                    DrawRect(new Rect2(x + dx, y, step, step), shade);
                }
            }
        }
    }

    private void DrawMist(float left, float cameraY, int step)
    {
        for (int i = 0; i < 6; i++)
        {
            float y = cameraY - 30f + i * 38f
                + Mathf.Sin(_time * .18f + i) * 5f;
            float drift = Mathf.Sin(_time * MistSpeed * .07f + i) * 28f;
            DrawRect(new Rect2(left - 40 + drift, y, ViewWidth + 80, 19), Fog);
        }
    }

    private float Hash(int a, int b)
    {
        float value = Mathf.Sin((a * 127.1f + b * 311.7f + Seed * .1f) * .12f)
            * 43758.5453f;
        return Mathf.PosMod(value, 1f);
    }
}
