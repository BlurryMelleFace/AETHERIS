using Godot;

namespace Aetheris;

/// <summary>The felled top of a tree (or a loose length of trunk) toppling over, sliding off its stump and landing.</summary>
public partial class FallingTree : Node2D
{
    private const float CreakSeconds = 0.4f;
    private const float FallSeconds = 0.95f;
    private const float FadeSeconds = 0.7f;

    private Sprite2D _sprite = null!;
    private Vector2 _pivot;
    private float _direction;
    private float _drop;
    private float _elapsed;
    private bool _landed;
    private TreeVariant _variant = null!;

    /// <summary>Builds the falling piece from a horizontal slice of the tree texture.</summary>
    public static FallingTree Create(TreeVariant variant, Rect2 region, Vector2 pivot, float direction, float dropToGround, ShaderMaterial? material)
    {
        var tree = new FallingTree { _variant = variant, _pivot = pivot, _direction = direction, _drop = dropToGround, ZIndex = -1 };
        tree._sprite = new Sprite2D
        {
            Texture = variant.Texture,
            RegionEnabled = true,
            RegionRect = region,
            Centered = true,
            Offset = new Vector2(0f, -region.Size.Y / 2f),
            Material = material,
        };
        tree.AddChild(tree._sprite);
        tree.Position = pivot;
        return tree;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        float fall = Mathf.Clamp((_elapsed - CreakSeconds) / FallSeconds, 0f, 1f);

        if (_elapsed < CreakSeconds)
        {
            // The trunk groans and leans a little before it goes.
            float c = _elapsed / CreakSeconds;
            Rotation = _direction * 0.06f * c * Mathf.Sin(_elapsed * 38f);
            return;
        }

        if (!_landed)
        {
            Rotation = _direction * 1.5f * Mathf.Pow(fall, 2.1f);
            // After the lean passes the tipping point the piece slides off and drops to the ground.
            float slide = Mathf.Clamp((fall - 0.4f) / 0.6f, 0f, 1f);
            Position = _pivot + new Vector2(_direction * _drop * 0.22f * slide, _drop * slide * slide);
            if (fall >= 1f) Land();
            return;
        }

        float settle = _elapsed - CreakSeconds - FallSeconds;
        Rotation = _direction * (1.5f - 0.06f * Mathf.Sin(settle * 14f) * Mathf.Exp(-settle * 5f));
        _sprite.Modulate = new Color(1, 1, 1, Mathf.Clamp(1f - (settle - 0.15f) / FadeSeconds, 0f, 1f));
        if (settle > FadeSeconds + 0.15f) QueueFree();
    }

    private void Land()
    {
        _landed = true;
        Vector2 top = Position + new Vector2(_direction * _sprite.RegionRect.Size.Y * 0.85f, -6f);
        var host = GetParent();
        ImpactEffects.Burst(host, top, ImpactEffects.LeavesFor(_variant.Species), 22, new Vector2(0, -1), 170f, 100f, 0.9f, 520f, 4f);
        ImpactEffects.Burst(host, Position + new Vector2(_direction * 18f, -4f), ImpactEffects.Wood, 7, new Vector2(0, -1), 110f);
    }
}
