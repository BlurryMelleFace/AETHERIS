using Godot;

namespace Aetheris;

/// <summary>Ambient 2D color grading for a gradual twilight / nighttime cycle.</summary>
public partial class DayNightCycle : CanvasModulate
{
    [Export] public float DayLengthSeconds { get; set; } = 180f;
    private float _clock = 0.25f;

    public override void _Process(double delta)
    {
        _clock = Mathf.PosMod(_clock + (float)delta / DayLengthSeconds, 1f);
        float sun = (Mathf.Sin(_clock * Mathf.Tau - Mathf.Pi / 2f) + 1f) * 0.5f;
        float daylight = Mathf.Lerp(0.16f, 1f, sun);
        Color = new Color(
            Mathf.Lerp(0.32f, 1f, daylight),
            Mathf.Lerp(0.38f, 1f, daylight),
            Mathf.Lerp(0.55f, 1f, daylight));
    }
}
