using Godot;

namespace Aetheris;

/// <summary>Warm flickering point light that follows the held item; strength 0 means no light at all.</summary>
public partial class TorchLight : Node2D
{
    private const float RadiusPixels = 230f;
    private readonly PointLight2D _light = new();
    private readonly FastNoiseLite _noise = new() { Frequency = 1f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
    private DayNightCycle? _cycle;
    private float _time;

    public float Strength { get; private set; }

    /// <summary>Brightness multiplier around 1; also scales the tile light map.</summary>
    public float Flicker { get; private set; } = 1f;

    public override void _Ready()
    {
        _cycle = GetNode("../..").GetNodeOrNull<DayNightCycle>("DayNightCycle");
        var falloff = new Gradient
        {
            Offsets = new[] { 0f, 0.12f, 0.35f, 0.65f, 1f },
            Colors = new[] { new Color(.6f, .6f, .6f), new Color(.8f, .8f, .8f), new Color(.62f, .62f, .62f), new Color(.22f, .22f, .22f), new Color(0, 0, 0) }
        };
        _light.Texture = new GradientTexture2D
        {
            Gradient = falloff, Width = 256, Height = 256,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1f, .5f)
        };
        _light.Visible = false;
        AddChild(_light);
    }

    public void Configure(ItemDefinition item)
    {
        Strength = item.LightStrength;
        _light.Color = item.LightColor;
        _light.Visible = Strength > 0f;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (Strength <= 0f) { Flicker = 1f; return; }

        float dip = Mathf.Max(0f, _noise.GetNoise1D(_time * 0.9f + 50f) - 0.55f) * 0.45f;
        Flicker = Mathf.Clamp(1f + 0.045f * Mathf.Sin(_time * 9.1f) + 0.03f * Mathf.Sin(_time * 15.7f + 1.3f)
            + 0.07f * _noise.GetNoise1D(_time * 4f) - dip, 0.74f, 1.15f);

        // Torchlight is strongest at night; in daylight it only adds a faint warm tint.
        float daylight = _cycle?.Daylight ?? 1f;
        _light.Energy = Strength * Flicker * Mathf.Lerp(1.0f, 0.1f, daylight);
        _light.TextureScale = RadiusPixels / 128f * (0.96f + 0.08f * Flicker);
        _light.Position = new Vector2(_noise.GetNoise1D(_time * 6f + 9f), _noise.GetNoise1D(_time * 6f + 19f)) * 1.5f;
    }
}
