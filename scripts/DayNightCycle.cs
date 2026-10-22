using Godot;
using System;

namespace Aetheris;

/// <summary>
/// One clock drives celestial motion, sky colors and world illumination.
/// Phase 0 = sunrise, .25 = noon, .5 = sunset, .75 = midnight.
/// </summary>
public partial class DayNightCycle : CanvasModulate
{
    // A complete day AND night, not just the daylight portion.
    [Export(PropertyHint.Range, "10,1200,1")] public float DayLengthSeconds { get; set; } = 120f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float StartPhase { get; set; } = 0f;
    [Export(PropertyHint.Range, "0,3,1")] public int SeasonIndex { get; set; } = 0;

    public float Phase => (float)_clock;
    public float Daylight { get; private set; }
    public float Twilight { get; private set; }

    private double _clock;
    private Color _seasonCurrent = Colors.White;
    private static readonly Color[] SeasonalGrades = {
        new Color(0.92f, 1f, 0.90f),   // Spring
        new Color(1f, 0.98f, 0.89f),   // Summer
        new Color(1f, 0.85f, 0.72f),   // Autumn
        new Color(0.77f, 0.88f, 1f)    // Winter
    };

    public override void _Ready()
    {
        ProcessPriority = -10;
        _clock = Mathf.PosMod(StartPhase, 1f);
        _seasonCurrent = SeasonalGrades[Math.Clamp(SeasonIndex, 0, SeasonalGrades.Length - 1)];
        UpdateLighting();
    }

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
        _clock = (_clock + delta / Math.Max(10.0, DayLengthSeconds)) % 1.0;
        SeasonIndex = Math.Clamp(SeasonIndex, 0, SeasonalGrades.Length - 1);
        _seasonCurrent = _seasonCurrent.Lerp(SeasonalGrades[SeasonIndex],
            1f - Mathf.Exp(-(float)delta * 1.5f));
        UpdateLighting();
    }

    private void UpdateLighting()
    {
        float sunHeight = Mathf.Sin(Phase * Mathf.Tau);
        Daylight = Mathf.SmoothStep(-0.18f, 0.45f, sunHeight);
        Twilight = 1f - Mathf.SmoothStep(0f, 0.5f, Mathf.Abs(sunHeight));

        // Blue moonlight keeps terrain readable; noon uses the full asset colors.
        Color daylightGrade = new Color(0.40f, 0.49f, 0.67f).Lerp(Colors.White, Daylight);
        daylightGrade = daylightGrade.Lerp(new Color(0.95f, 0.73f, 0.60f), Twilight * 0.55f);
        Color = new Color(daylightGrade.R * _seasonCurrent.R,
            daylightGrade.G * _seasonCurrent.G,
            daylightGrade.B * _seasonCurrent.B);
    }
}
