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
