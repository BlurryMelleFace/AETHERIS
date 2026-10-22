using Godot;

namespace Aetheris;

/// <summary>
/// Gentle full-scene color modulation simulating dusk and night.
/// Not yet a physically based lighting system.
/// </summary>
public partial class DayNightCycle : CanvasModulate
{
    [Export] public float DayLengthSeconds { get; set; } = 180f;
    private float _clock = 0.25f;

    public override void _Process(double delta)
    {
        _clock = Mathf.PosMod(_clock + (float)delta / DayLengthSeconds, 1f);
        float daylight = Mathf.Clamp((Mathf.Sin(_clock * Mathf.Tau) + 0.32f)
            / 1.32f, 0.18f, 1f);
        Color = new Color(
            Mathf.Lerp(0.29f, 1f, daylight),
            Mathf.Lerp(0.36f, 1f, daylight),
            Mathf.Lerp(0.52f, 1f, daylight));
    }
}
