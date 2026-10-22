using Godot;

namespace Aetheris;

public partial class Player : CharacterBody2D
{
    [Export] public float WalkSpeed { get; set; } = 235f;
    [Export] public float JumpVelocity { get; set; } = -365f;
    [Export] public float Gravity { get; set; } = 980f;

    public override void _Ready()
    {
        var capsule = GetNode<CollisionShape2D>("CollisionShape2D");
        capsule.Shape = new CapsuleShape2D { Radius = 9, Height = 30 };
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
        Velocity = velocity;
        MoveAndSlide();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(-9, -15, 18, 30), Color.FromHtml("#f8d49c"));
        DrawRect(new Rect2(-9, -15, 18, 7), Color.FromHtml("#3f463e"));
        DrawCircle(new Vector2(3, -6), 1.5f, Colors.Black);
    }
}
