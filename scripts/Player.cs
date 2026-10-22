using Godot;

namespace Aetheris;

public partial class Player : CharacterBody2D
{
    private static readonly Color Skin = Color.FromHtml("#f8d49c");

    [Export] public float WalkSpeed { get; set; } = 235f;
    [Export] public float JumpVelocity { get; set; } = -365f;
    [Export] public float Gravity { get; set; } = 980f;

    private TorchLight _torch = null!;
    private float _facing = 1f;
    private float _time;

    public Equipment Equipment { get; } = new();

    /// <summary>Light given off by the held item. The character itself never emits light.</summary>
    public float HeldLight => _torch.Strength;
    public float HeldLightFlicker => _torch.Flicker;

    /// <summary>Where the held light starts; kept inside the body so it is never inside a wall.</summary>
    public Vector2 HeldLightOrigin => GlobalPosition + new Vector2(0, -8);

    public override void _Ready()
    {
        var capsule = GetNode<CollisionShape2D>("CollisionShape2D");
        capsule.Shape = new CapsuleShape2D { Radius = 9, Height = 30 };
        _torch = new TorchLight { Name = "TorchLight", Position = new Vector2(_facing * 13f, -14f) };
        AddChild(_torch);
        Equipment.Changed += item =>
        {
            _torch.Configure(item);
            QueueRedraw();
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        var velocity = Velocity;
        float dt = (float)delta;
        if (!IsOnFloor()) velocity.Y += Gravity * dt;
        if (IsOnFloor() && Input.IsKeyPressed(Key.Space)) velocity.Y = JumpVelocity;
        float axis = (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) ? 1 : 0)
                   - (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) ? 1 : 0);
        velocity.X = axis * WalkSpeed;
        if (axis != 0) _facing = axis;
        _torch.Position = new Vector2(_facing * 13f, -14f);
        Velocity = velocity;
        MoveAndSlide();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (HeldLight > 0f) QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(-9, -15, 18, 30), Skin);
        DrawRect(new Rect2(-9, -15, 18, 7), Color.FromHtml("#3f463e"));
        DrawCircle(new Vector2(_facing * 3, -6), 1.5f, Colors.Black);

        var held = Equipment.Current;
        if (held == ItemCatalog.Hand) return;
        // Forearm reaching out to the item, drawn at the icon's native 16px size.
        DrawRect(new Rect2(_facing > 0 ? 7 : -13, -2, 6, 4), Skin.Darkened(0.12f));
        DrawTextureRect(held.Icon, new Rect2(_facing * 13f - 8f, -14f, 16f, 16f), false);
        if (held.LightStrength > 0f)
        {
            // Flame tongue that dances with the light.
            float lick = Mathf.Sin(_time * 14f) + (HeldLightFlicker - 1f) * 8f;
            DrawRect(new Rect2(_facing * 13f - 1f + Mathf.Round(lick * 0.5f), -15f - Mathf.Round(Mathf.Abs(lick)), 2, 2),
                new Color("#ffd466"));
        }
    }
}
