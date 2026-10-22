using Godot;

namespace Aetheris;

public partial class Player : CharacterBody2D
{
    /// <summary>Under 2 blocks wide so it fits a 2-block gap but not a 1-block one.</summary>
    public const float WidthTiles = 1.6f;

    /// <summary>A little under 3 blocks tall so the character fits through a 3-block-high opening.</summary>
    public const float HeightTiles = 2.8f;
    public static readonly Vector2 BodySize = new(World.TileSize * WidthTiles, World.TileSize * HeightTiles);

    /// <summary>Ledges up to this many pixels high are climbed automatically.</summary>
    private const float StepHeight = World.TileSize + 1f;

    private const float Acceleration = 2400f;
    private const float IconPixel = HeroSprite.PixelScale;

    [Export] public float WalkSpeed { get; set; } = 170f;
    [Export] public float SprintSpeed { get; set; } = 290f;
    [Export] public float JumpVelocity { get; set; } = -520f;
    [Export] public float Gravity { get; set; } = 1400f;

    private HeroSprite _hero = null!;
    private TorchLight _torch = null!;
    private float _facing = 1f;
    private float _time;
    private float _runCycle;
    private float _idleCycle;
    private float _airTime;
    private float _fallSpeed;
    private float _landTimer;
    private bool _wasOnFloor = true;

    public Equipment Equipment { get; } = new();

    public HeroSprite Hero => _hero;

    /// <summary>Light given off by the held item. The character itself never emits light.</summary>
    public float HeldLight => _torch.Strength;
    public float HeldLightFlicker => _torch.Flicker;

    /// <summary>Where the held light starts; kept inside the body so it is never inside a wall.</summary>
    public Vector2 HeldLightOrigin => GlobalPosition + new Vector2(0, -12);

    private bool Holding => Equipment.Current != ItemCatalog.Hand;

    // The icon's grip (handle) is at pixel (8, 11.5) and its flame at (8, 1.5).
    private Vector2 FlamePosition => _hero.HandPosition + new Vector2(0f, -10f * IconPixel);

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        GetNode<CollisionShape2D>("CollisionShape2D").Shape = new RectangleShape2D { Size = BodySize };
        var camera = GetNode<Camera2D>("Camera2D");
        camera.LimitLeft = 0;
        camera.LimitRight = World.Width * World.TileSize;
        camera.LimitBottom = World.Height * World.TileSize;
        camera.LimitTop = -360;
        _hero = new HeroSprite { Name = "Hero" };
        AddChild(_hero);
        _torch = new TorchLight { Name = "TorchLight" };
        AddChild(_torch);
        Equipment.Changed += item =>
        {
            _torch.Configure(item);
            QueueRedraw();
        };
        _hero.Play(HeroRow.Idle, 0, true, false);
        _torch.Position = FlamePosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        var velocity = Velocity;
        float dt = (float)delta;
        if (!IsOnFloor()) velocity.Y += Gravity * dt;
        if (IsOnFloor() && Input.IsKeyPressed(Key.Space)) velocity.Y = JumpVelocity;
        float axis = (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) ? 1 : 0)
                   - (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) ? 1 : 0);
        // Stops are instant so lining up with a gap stays precise; only the start eases in.
        if (axis == 0f || Mathf.Sign(velocity.X) == -axis) velocity.X = 0f;
        float topSpeed = Input.IsKeyPressed(Key.Shift) ? SprintSpeed : WalkSpeed;
        velocity.X = Mathf.MoveToward(velocity.X, axis * topSpeed, Acceleration * dt);
        if (axis != 0f) _facing = axis;
        Velocity = velocity;

        bool stepped = TryStepUp(velocity.X * dt);
        MoveAndSlide();
        if (stepped) MoveAndCollide(new Vector2(0, StepHeight));
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
        float dt = (float)delta;
        _time += dt;
        Animate(dt);
        _torch.Position = FlamePosition;
        if (Holding) QueueRedraw();
    }

    private void Animate(float dt)
    {
        bool onFloor = IsOnFloor();
        if (onFloor)
        {
            if (!_wasOnFloor && _fallSpeed > 320f) _landTimer = 0.14f;
            _fallSpeed = 0f;
            _airTime = 0f;
        }
        else
        {
            _airTime += dt;
            _fallSpeed = Mathf.Max(_fallSpeed, Velocity.Y);
        }
        _wasOnFloor = onFloor;
        _landTimer = Mathf.Max(0f, _landTimer - dt);

        HeroRow row;
        int column;
        float speed = Mathf.Abs(Velocity.X);
        if (!onFloor)
        {
            row = HeroRow.Jump;
            column = _airTime < 0.08f && Velocity.Y < 0f ? 1 : Velocity.Y < -140f ? 2 : Velocity.Y < 140f ? 3 : 4;
        }
        else if (_landTimer > 0f)
        {
            row = HeroRow.Jump;
            column = 5;
        }
        else if (speed > 5f)
        {
            bool run = speed > WalkSpeed * 1.15f;
            row = run ? HeroRow.Run : HeroRow.Walk;
            // Distance covered per animation cycle, matched to the stride so the feet do not skate.
            _runCycle = Mathf.PosMod(_runCycle + speed * dt / (run ? 78f : 48f), 1f);
            column = (int)(_runCycle * HeroSprite.Columns);
        }
        else
        {
            row = HeroRow.Idle;
            _idleCycle = Mathf.PosMod(_idleCycle + dt * 6f, HeroSprite.Columns);
            column = (int)_idleCycle;
        }
        _hero.Play(row, column, _facing > 0f, Holding);
    }

    public override void _Draw()
    {
        var held = Equipment.Current;
        if (!Holding) return;
        // Drawn under the hero so the fingers wrap the handle; the icon pixels match the hero's pixel size.
        Vector2 hand = _hero.HandPosition;
        float size = PixelPen.Size * IconPixel;
        Vector2 grip = new Vector2(8f, 11.5f) * IconPixel;
        var origin = new Vector2(_facing > 0f ? hand.X - grip.X : hand.X + grip.X, hand.Y - grip.Y);
        DrawTextureRect(held.Icon, new Rect2(origin, new Vector2(_facing > 0f ? size : -size, size)), false);
        if (held.LightStrength > 0f)
        {
            // Flame tongue that dances with the light.
            float lick = Mathf.Sin(_time * 14f) + (HeldLightFlicker - 1f) * 8f;
            DrawRect(new Rect2(hand.X - 1.5f + Mathf.Round(lick), hand.Y - 10f * IconPixel - 3f - Mathf.Round(Mathf.Abs(lick) * 1.5f), 3, 3),
                new Color("#ffd466"));
        }
    }
}
