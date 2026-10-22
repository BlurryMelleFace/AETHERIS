using Godot;

namespace Aetheris;

public partial class Player : CharacterBody2D
{
    private static readonly Color Skin = Color.FromHtml("#f8d49c");
    private static readonly Color Shirt = Color.FromHtml("#4b6f8c");
    private static readonly Color Pants = Color.FromHtml("#3b3a4c");

    /// <summary>Under 2 blocks wide so it fits a 2-block gap but not a 1-block one.</summary>
    public const float WidthTiles = 1.6f;
    public const float HeightTiles = 3f;
    public static readonly Vector2 BodySize = new(World.TileSize * WidthTiles, World.TileSize * HeightTiles);

    /// <summary>Ledges up to this many pixels high are climbed automatically.</summary>
    private const float StepHeight = World.TileSize + 1f;

    [Export] public float WalkSpeed { get; set; } = 240f;
    [Export] public float JumpVelocity { get; set; } = -520f;
    [Export] public float Gravity { get; set; } = 1400f;

    private TorchLight _torch = null!;
    private float _facing = 1f;
    private float _time;

    public Equipment Equipment { get; } = new();

    /// <summary>Light given off by the held item. The character itself never emits light.</summary>
    public float HeldLight => _torch.Strength;
    public float HeldLightFlicker => _torch.Flicker;

    /// <summary>Where the held light starts; kept inside the body so it is never inside a wall.</summary>
    public Vector2 HeldLightOrigin => GlobalPosition + new Vector2(0, -12);

    private Vector2 TorchOffset => new(_facing * 26f, -34f);

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        GetNode<CollisionShape2D>("CollisionShape2D").Shape = new RectangleShape2D { Size = BodySize };
        var camera = GetNode<Camera2D>("Camera2D");
        camera.LimitLeft = 0;
        camera.LimitRight = World.Width * World.TileSize;
        camera.LimitBottom = World.Height * World.TileSize;
        camera.LimitTop = -360;
        _torch = new TorchLight { Name = "TorchLight", Position = TorchOffset };
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
        _torch.Position = TorchOffset;
        Velocity = velocity;

        bool stepped = TryStepUp(velocity.X * dt);
        MoveAndSlide();
        if (stepped) MoveAndCollide(new Vector2(0, StepHeight));
        QueueRedraw();
    }

    // Lifts the body over a one-block ledge so walking over bumps never needs a jump.
    private bool TryStepUp(float horizontal)
    {
        if (!IsOnFloor() || horizontal == 0f) return false;
        var motion = new Vector2(horizontal, 0f);
        if (!TestMove(GlobalTransform, motion)) return false;
        var lift = new Vector2(0f, -StepHeight);
        if (TestMove(GlobalTransform, lift)) return false;
        if (TestMove(GlobalTransform.Translated(lift), motion)) return false;
        GlobalPosition += lift;
        return true;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (HeldLight > 0f) QueueRedraw();
    }

    public override void _Draw()
    {
        float w = BodySize.X, h = BodySize.Y, top = -h / 2f;
        DrawRect(new Rect2(-w / 2f, top, w, 20f), Skin);
        DrawRect(new Rect2(-w / 2f, top, w, 7f), Color.FromHtml("#3f463e"));
        DrawRect(new Rect2(-w / 2f, top + 20f, w, 29f), Shirt);
        DrawRect(new Rect2(-w / 2f, top + 49f, w, 18f), Pants);
        DrawRect(new Rect2(-w / 2f, top + 67f, w, 5f), Color.FromHtml("#2a2623"));
        DrawRect(new Rect2(_facing * 7f - 1.5f, top + 10f, 3f, 3f), Colors.Black);

        var held = Equipment.Current;
        if (held == ItemCatalog.Hand) return;
        // Forearm reaching out to the item; icons are drawn at a whole-number scale to stay crisp.
        DrawRect(new Rect2(_facing > 0 ? 11f : -23f, -15f, 12f, 8f), Shirt);
        DrawRect(new Rect2(_facing > 0 ? 20f : -26f, -15f, 6f, 8f), Skin);
        float iconSize = PixelPen.Size * 2f;
        DrawTextureRect(held.Icon, new Rect2(_facing * 26f - iconSize / 2f, -37f, iconSize, iconSize), false);
        if (held.LightStrength > 0f)
        {
            // Flame tongue that dances with the light.
            float lick = Mathf.Sin(_time * 14f) + (HeldLightFlicker - 1f) * 8f;
            DrawRect(new Rect2(_facing * 26f - 2f + Mathf.Round(lick), -39f - Mathf.Round(Mathf.Abs(lick) * 2f), 4, 4),
                new Color("#ffd466"));
        }
    }
}
