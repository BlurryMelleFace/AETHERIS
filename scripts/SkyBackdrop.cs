using Godot;

namespace Aetheris;

/// <summary>
/// Screen-anchored pixel sky behind the landscape. Its separate canvas keeps
/// the sun, moon and stars luminous while DayNightCycle dims the world.
/// </summary>
[Tool]
public partial class SkyBackdrop : Node2D
{
    [ExportGroup("Day palette")]
    [Export] public Color DayTop { get; set; } = new("#5188ab");
    [Export] public Color DayMid { get; set; } = new("#9abfcb");
    [Export] public Color DayHorizon { get; set; } = new("#d6ddc8");
    [ExportGroup("Night palette")]
    [Export] public Color NightTop { get; set; } = new("#10192f");
    [Export] public Color NightMid { get; set; } = new("#25374f");
    [Export] public Color NightHorizon { get; set; } = new("#48566e");
    [ExportGroup("Twilight palette")]
    [Export] public Color TwilightTop { get; set; } = new("#535577");
    [Export] public Color TwilightMid { get; set; } = new("#bd858e");
    [Export] public Color TwilightHorizon { get; set; } = new("#efbb8f");

    private DayNightCycle? _cycle;
    private float _time;

    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
            _cycle = GetNode<DayNightCycle>("../../DayNightCycle");
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        float phase = _cycle?.Phase ?? 0.25f;
        float daylight = _cycle?.Daylight ?? 1f;
        float twilight = _cycle?.Twilight ?? 0f;
        Color top = NightTop.Lerp(DayTop, daylight).Lerp(TwilightTop, twilight);
        Color mid = NightMid.Lerp(DayMid, daylight).Lerp(TwilightMid, twilight);
        Color horizon = NightHorizon.Lerp(DayHorizon, daylight).Lerp(TwilightHorizon, twilight);

        const int bands = 64;
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            Color color = t < 0.6f ? top.Lerp(mid, t / 0.6f)
                : mid.Lerp(horizon, (t - 0.6f) / 0.4f);
            DrawRect(new Rect2(0, i * size.Y / bands, size.X, size.Y / bands + 1), color);
        }

        DrawStars(size, Mathf.Pow(1f - daylight, 2f) * (1f - twilight * 0.8f));
        DrawCelestialBody(size, phase, false);
        DrawCelestialBody(size, Mathf.PosMod(phase + 0.5f, 1f), true);
        DrawClouds(size, daylight, twilight);
        DrawBirds(size, daylight);
    }

    private void DrawClouds(Vector2 size, float daylight, float twilight)
    {
        Vector2 cameraCenter = Engine.IsEditorHint() ? Vector2.Zero
            : GetViewport().GetCanvasTransform().AffineInverse() * (size * 0.5f);
        Color shade = new Color("#53647e").Lerp(new Color("#aec6cd"), daylight);
        Color highlight = new Color("#8293ac").Lerp(new Color("#eef0df"), daylight)
            .Lerp(new Color("#e9b7a2"), twilight * 0.5f);
        for (int cloud = 0; cloud < 6; cloud++)
        {
            float scale = cloud % 2 == 0 ? 2f : 3f;
            float span = size.X + 256f;
            float left = Mathf.PosMod(cloud * span / 6f + _time * (3f + cloud % 3)
                - cameraCenter.X * (0.008f + cloud % 2 * 0.004f), span) - 128f;
            float top = size.Y * (0.09f + (cloud * 7 % 5) * 0.047f);
            Vector2 origin = new(left, top);
            DrawRect(new Rect2(origin + new Vector2(0, 8) * scale,
                new Vector2(32, 4) * scale), shade);
            DrawRect(new Rect2(origin + new Vector2(3, 5) * scale,
                new Vector2(26, 5) * scale), highlight);
            DrawRect(new Rect2(origin + new Vector2(8, 2) * scale,
                new Vector2(10, 5) * scale), highlight);
            DrawRect(new Rect2(origin + new Vector2(17, 4) * scale,
                new Vector2(7, 4) * scale), highlight);
        }
    }

    private void DrawBirds(Vector2 size, float daylight)
    {
        if (daylight < 0.15f) return;
        const float interval = 38f;
        int flock = Mathf.FloorToInt(_time / interval);
        float elapsed = Mathf.PosMod(_time, interval) - 7f;
        float duration = (size.X + 160f) / 65f;
        if (elapsed < 0f || elapsed > duration) return;

        bool headingRight = flock % 2 == 0;
        float lead = elapsed * 65f - 80f;
        Color tint = new(new Color("#324751"), Mathf.SmoothStep(0.15f, 0.6f, daylight));
        for (int bird = 0; bird < 3 + flock % 3; bird++)
        {
            float horizontal = lead - bird * 17f;
            float vertical = size.Y * (0.18f + flock % 3 * 0.035f)
                + Mathf.Abs(bird - 2) * 7f + Mathf.Sin(elapsed * 1.4f + bird) * 3f;
            Vector2 center = new(headingRight ? horizontal : size.X - horizontal, vertical);
            float wing = Mathf.Round(Mathf.Sin(elapsed * 7f + bird * 1.7f) * 3f);
            DrawRect(new Rect2(center, new Vector2(3, 2)), tint);
            DrawLine(center, center + new Vector2(-4, wing), tint, 2f);
            DrawLine(center + new Vector2(2, 0), center + new Vector2(6, wing), tint, 2f);
        }
    }

    private void DrawStars(Vector2 size, float visibility)
    {
        if (visibility < 0.005f) return;
        for (int i = 0; i < 90; i++)
        {
            // Fixed positions: no random changes per frame and no camera parallax.
            float x = ((i * 127 + 43) % 997) / 997f * size.X;
            float y = ((i * 89 + 71) % 503) / 503f * size.Y * 0.65f;
            float pulse = 0.7f + 0.15f * Mathf.Sin(_time * 1.3f + i * 2.7f);
            float width = i % 7 == 0 ? 3f : 2f;
            DrawRect(new Rect2(Mathf.Floor(x), Mathf.Floor(y), width, width),
                new Color(0.83f, 0.90f, 1f, visibility * pulse));
        }
    }

    private void DrawCelestialBody(Vector2 size, float phase, bool moon)
    {
        if (phase >= 0.5f) return; // Below the horizon for the other half of the cycle.
        float progress = phase * 2f;
        float elevation = Mathf.Sin(progress * Mathf.Pi);
        float opacity = Mathf.SmoothStep(0f, 0.08f, progress)
            * (1f - Mathf.SmoothStep(0.92f, 1f, progress));
        if (opacity < 0.001f) return;
        Vector2 center = new(size.X * Mathf.Lerp(0.04f, 0.96f, progress),
            size.Y * (0.72f - elevation * 0.58f));
        // Keep the sprite on a 2px grid as it moves along its arc.
        center = new Vector2(Mathf.Round(center.X / 2f) * 2f, Mathf.Round(center.Y / 2f) * 2f);
        Color tint = moon ? new Color("#dfeaf0")
            : new Color("#ffa867").Lerp(new Color("#fff1b6"), elevation);
        int radius = moon ? 14 : 18;
        DrawPixelDisc(center, radius + 18, new Color(tint, opacity * 0.035f));
        DrawPixelDisc(center, radius + 10, new Color(tint, opacity * 0.07f));
        DrawPixelDisc(center, radius + 4, new Color(tint, opacity * 0.15f));
        DrawPixelDisc(center, radius, new Color(tint, opacity));
        if (moon)
        {
            Color crater = new(0.58f, 0.68f, 0.77f, opacity * 0.65f);
            DrawPixelDisc(center + new Vector2(-4, -4), 4, crater);
            DrawPixelDisc(center + new Vector2(6, 2), 3, crater);
            DrawPixelDisc(center + new Vector2(-2, 7), 2, crater);
        }
    }

    private void DrawPixelDisc(Vector2 center, int radius, Color color)
    {
        // One horizontal rectangle per pixel row keeps the silhouette crisp and cheap.
        for (int y = -radius; y < radius; y += 2)
        {
            float row = y + 1;
            float halfWidth = Mathf.Floor(Mathf.Sqrt(radius * radius - row * row) / 2f) * 2f;
            if (halfWidth > 0)
                DrawRect(new Rect2(center.X - halfWidth, center.Y + y, halfWidth * 2, 2), color);
        }
    }
}
