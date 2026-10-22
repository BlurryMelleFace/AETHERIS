using Godot;
using System.Collections.Generic;

namespace Aetheris;

/// <summary>
/// Terraria-style limited visibility: sky light enters from open air and a small
/// player light spreads around the character; everything else stays dark.
/// </summary>
public partial class LightMap : Node2D
{
    // Per-step light multipliers; solid tiles absorb much more than air.
    [Export(PropertyHint.Range, "0.3,0.95,0.01")] public float AirFalloff { get; set; } = 0.75f;
    [Export(PropertyHint.Range, "0.1,0.9,0.01")] public float SolidFalloff { get; set; } = 0.55f;
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float PlayerLight { get; set; } = 1f;

    private const int W = World.Width;
    private const int H = World.Height;
    private const float Cutoff = 0.05f;
    private const float DarkMargin = 4000f;

    private static readonly Vector2I[] Dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private readonly float[,] _sky = new float[W, H];
    private readonly float[,] _torch = new float[W, H];
    private readonly byte[] _pixels = new byte[W * H * 4];
    private readonly Queue<Vector2I> _queue = new();
    private World? _world;
    private Node2D? _player;
    private Image? _image;
    private ImageTexture? _texture;
    private Vector2I _lastPlayerTile = new(int.MinValue, int.MinValue);
    private bool _skyDirty = true;

    public override void _Ready()
    {
        ZIndex = 10;
        TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        _world = GetParent().GetNodeOrNull<World>("World");
        _player = GetParent().GetNodeOrNull<Node2D>("Player");
        if (_world != null) _world.TilesChanged += () => _skyDirty = true;
    }

    public override void _Process(double delta)
    {
        if (_world == null || _player == null) return;
        Vector2I tile = new(Mathf.FloorToInt(_player.GlobalPosition.X / World.TileSize),
            Mathf.FloorToInt(_player.GlobalPosition.Y / World.TileSize));
        if (!_skyDirty && tile == _lastPlayerTile) return;

        if (_skyDirty) ComputeSky();
        _lastPlayerTile = tile;
        ComputeTorch(tile);
        UploadTexture();
        _skyDirty = false;
        QueueRedraw();
    }

    private void ComputeSky()
    {
        System.Array.Clear(_sky);
        _queue.Clear();
        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H && !_world!.IsSolid(x, y); y++)
            {
                _sky[x, y] = 1f;
                _queue.Enqueue(new Vector2I(x, y));
            }
        }
        Spread(_sky);
    }

    private void ComputeTorch(Vector2I origin)
    {
        System.Array.Clear(_torch);
        _queue.Clear();
        if (origin.X < 0 || origin.X >= W || origin.Y < 0 || origin.Y >= H) return;
        _torch[origin.X, origin.Y] = PlayerLight;
        _queue.Enqueue(origin);
        Spread(_torch);
    }

    private void Spread(float[,] map)
    {
        while (_queue.Count > 0)
        {
            Vector2I at = _queue.Dequeue();
            float current = map[at.X, at.Y];
            foreach (Vector2I dir in Dirs)
            {
                int nx = at.X + dir.X, ny = at.Y + dir.Y;
                if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                float next = current * (_world!.IsSolid(nx, ny) ? SolidFalloff : AirFalloff);
                if (next < Cutoff || next <= map[nx, ny] + 0.001f) continue;
                map[nx, ny] = next;
                _queue.Enqueue(new Vector2I(nx, ny));
            }
        }
    }

    private void UploadTexture()
    {
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float light = Mathf.Max(_sky[x, y], _torch[x, y]);
            float darkness = 1f - Mathf.Clamp((light - 0.06f) / 0.7f, 0f, 1f);
            _pixels[(y * W + x) * 4 + 3] = (byte)(darkness * 255f);
        }
        _image = Image.CreateFromData(W, H, false, Image.Format.Rgba8, _pixels);
        if (_texture == null) _texture = ImageTexture.CreateFromImage(_image);
        else _texture.Update(_image);
    }

    public override void _Draw()
    {
        if (_texture == null) return;
        float w = W * World.TileSize, h = H * World.TileSize;
        DrawTextureRect(_texture, new Rect2(0, 0, w, h), false);
        DrawRect(new Rect2(-DarkMargin, h, w + 2 * DarkMargin, DarkMargin), Colors.Black);
    }
}
