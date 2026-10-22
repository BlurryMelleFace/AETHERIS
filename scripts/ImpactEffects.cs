using Godot;

namespace Aetheris;

/// <summary>Short-lived pixel particles for mining chips, wood splinters and falling leaves.</summary>
public static class ImpactEffects
{
    public static void Burst(Node parent, Vector2 position, Color[] colors, int amount, Vector2 direction,
        float speed, float spread = 75f, float lifetime = 0.55f, float gravity = 720f, float size = 3f)
    {
        var fade = new Gradient();
        fade.SetColor(0, Colors.White);
        fade.SetColor(1, new Color(1, 1, 1, 0));
        var particles = new CpuParticles2D
        {
            Position = position,
            ZIndex = 3,
            Amount = amount,
            Lifetime = lifetime,
            OneShot = true,
            Explosiveness = 1f,
            Direction = direction,
            Spread = spread,
            InitialVelocityMin = speed * 0.45f,
            InitialVelocityMax = speed,
            Gravity = new Vector2(0f, gravity),
            ScaleAmountMin = size * 0.7f,
            ScaleAmountMax = size,
            ColorRamp = fade,
            Color = colors[0],
            Emitting = true,
            LocalCoords = false,
        };
        // CPUParticles2D has no per-particle palette, so mix several emitters for variety.
        parent.AddChild(particles);
        particles.Finished += particles.QueueFree;
        for (int i = 1; i < colors.Length; i++)
        {
            var extra = (CpuParticles2D)particles.Duplicate();
            extra.Color = colors[i];
            extra.Amount = Mathf.Max(2, amount / 2);
            parent.AddChild(extra);
            extra.Emitting = true;
            extra.Finished += extra.QueueFree;
        }
    }

    public static Color[] ChipsFor(byte tile) => tile switch
    {
        1 => new[] { new Color("#6e4d34"), new Color("#4a8a3b"), new Color("#85603f") },
        2 => new[] { new Color("#6e4d34"), new Color("#85603f"), new Color("#4e3424") },
        3 => new[] { new Color("#717887"), new Color("#8a91a1"), new Color("#50545f") },
        _ => new[] { new Color("#4f5478"), new Color("#666c96"), new Color("#2f3247") },
    };

    public static readonly Color[] Wood = { new("#8a6248"), new("#69493a"), new("#b88a58") };

    public static Color[] LeavesFor(string species) => species switch
    {
        "birch" => new[] { new Color("#88b946"), new Color("#659a38"), new Color("#aed362") },
        "oak" => new[] { new Color("#3d7a30"), new Color("#58973c"), new Color("#2a5e28") },
        "autumn_oak" => new[] { new Color("#da792a"), new Color("#b9501d"), new Color("#f2a447") },
        _ => new[] { new Color("#2f6341"), new Color("#43804e"), new Color("#214b34") },
    };
}
