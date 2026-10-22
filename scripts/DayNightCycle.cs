using Godot;

namespace Aetheris;

/// <summary>
/// Shared environment grade. The background panorama and foreground terrain
/// inherit the same canvas color. This is the foundation for future seasons.
/// Press T to cycle through seasonal tones during development.
/// </summary>
public partial class DayNightCycle : CanvasModulate
{
    [Export] public float DayLengthSeconds { get; set; } = 180f;
    [Export(PropertyHint.Range, "0,3,1")] public int SeasonIndex { get; set; } = 0;

    private float _clock = 0.25f;
    private Color _seasonCurrent = Colors.White;
    private static readonly Color[] SeasonalGrades = {
        new Color(0.92f, 1f, 0.90f),   // Spring
        new Color(1f, 0.98f, 0.89f),   // Summer
        new Color(1f, 0.85f, 0.72f),   // Autumn
        new Color(0.77f, 0.88f, 1f)    // Winter
    };

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo
            && key.Keycode == Key.T)
        {
            SeasonIndex = (SeasonIndex + 1) % SeasonalGrades.Length;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        _clock = Mathf.PosMod(_clock + (float)delta / Mathf.Max(10f, DayLengthSeconds), 1f);
        float sun = (Mathf.Sin(_clock * Mathf.Tau - Mathf.Pi / 2f) + 1f) * 0.5f;
        float daylight = Mathf.Lerp(0.16f, 1f, sun);
        Color daylightGrade = new Color(
            Mathf.Lerp(0.32f, 1f, daylight),
            Mathf.Lerp(0.38f, 1f, daylight),
            Mathf.Lerp(0.55f, 1f, daylight));
        _seasonCurrent = _seasonCurrent.Lerp(SeasonalGrades[SeasonIndex],
            Mathf.Clamp((float)delta * 1.5f, 0f, 1f));
        Color = new Color(daylightGrade.R * _seasonCurrent.R,
            daylightGrade.G * _seasonCurrent.G,
            daylightGrade.B * _seasonCurrent.B);
    }
}
