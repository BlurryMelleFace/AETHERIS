using Godot;

namespace Aetheris;

public enum HeroRow { Idle, Walk, Run, Jump, Attack }

/// <summary>Plays the hero sprite sheet; one art pixel is one world pixel, matching the tiles and trees.</summary>
public partial class HeroSprite : Sprite2D
{
    public const int FrameSize = 84;
    public const int Columns = 6;
    public const int Rows = 5;
    public const float PixelScale = 1f;

    private static readonly Texture2D Sheet = GD.Load<Texture2D>("res://assets/characters/hero_sheet.png");
    private static readonly Texture2D HoldSheet = GD.Load<Texture2D>("res://assets/characters/hero_sheet_hold.png");
    private static readonly Vector2?[][] Hands = LoadHandAnchors();

    private bool _facingRight = true;
    private HeroRow _row;
    private int _column;

    /// <summary>Frame position of the hand in the current frame, in the owner's local space.</summary>
    public Vector2 HandPosition
    {
        get
        {
            Vector2 anchor = Hands[(int)_row][_column] ?? new Vector2(FrameSize * 0.7f, FrameSize * 0.4f);
            float x = _facingRight ? anchor.X : FrameSize - anchor.X;
            return Position + new Vector2(x, anchor.Y) * PixelScale;
        }
    }

    public int CurrentFrame => (int)_row * Columns + _column;

    public override void _Ready()
    {
        Centered = false;
        Hframes = Columns;
        Vframes = Rows;
        Scale = new Vector2(PixelScale, PixelScale);
        TextureFilter = TextureFilterEnum.Nearest;
        Texture = Sheet;
        // The bottom of the frame is the sole of the boot, so it sits on the collision floor.
        Position = new Vector2(-FrameSize * PixelScale / 2f, Player.BodySize.Y / 2f - FrameSize * PixelScale);
    }

    public void Play(HeroRow row, int column, bool facingRight, bool holding)
    {
        _row = row;
        _column = Mathf.Clamp(column, 0, Columns - 1);
        _facingRight = facingRight;
        FlipH = !facingRight;
        Texture = holding ? HoldSheet : Sheet;
        Frame = CurrentFrame;
    }

    private static Vector2?[][] LoadHandAnchors()
    {
        using var file = FileAccess.Open("res://assets/characters/hero_anchors.json", FileAccess.ModeFlags.Read);
        var data = Json.ParseString(file.GetAsText()).AsGodotDictionary();
        var hand = data["hand"].AsGodotDictionary();
        var result = new Vector2?[Rows][];
        string[] names = { "idle", "walk", "run", "jump", "attack" };
        for (int row = 0; row < Rows; row++)
        {
            var entries = hand[names[row]].AsGodotArray();
            result[row] = new Vector2?[Columns];
            for (int column = 0; column < Columns; column++)
            {
                if (entries[column].VariantType == Variant.Type.Nil) continue;
                var point = entries[column].AsGodotArray();
                result[row][column] = new Vector2(point[0].AsSingle(), point[1].AsSingle());
            }
        }
        return result;
    }
}
