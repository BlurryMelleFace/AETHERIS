using Godot;
using System;
using System.Collections.Generic;

namespace Aetheris;

/// <summary>
/// Living Forest prototype: seeded terrain, cave pockets, editable ground and
/// procedural decorative layers. Finite map with prototype tile collisions.
/// </summary>
public partial class World : Node2D
{
    public const int TileSize = 24;
    public const int Width = 160;
    public const int Height = 60;
    private const int Seed = 10421;
    private const int GroundStart = 19;
    private readonly byte[,] _tiles = new byte[Width, Height];
    private readonly int[] _surface = new int[Width];
    private readonly Dictionary<Vector2I, StaticBody2D> _colliders = new();
    private readonly List<int> _trees = new();
    private readonly FastNoiseLite _caveNoise = new();
    private Node2D? _player;

    public override void _Ready()
    {
        _player = GetParent().GetNodeOrNull<Node2D>("Player");
        _caveNoise.Seed = Seed;
        _caveNoise.Frequency = 0.072f;
        _caveNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _caveNoise.FractalOctaves = 3;
        Generate();
        BuildColliders();
        QueueRedraw();
    }

    private void Generate()
    {
        var rng = new Random(Seed);
        for (int x = 0; x < Width; x++)
        {
            int surface = GroundStart + (int)Math.Round(
                3.0 * Math.Sin(x * 0.075) + 1.8 * Math.Sin(x * 0.23));
            _surface[x] = surface;
            for (int y = surface; y < Height; y++)
            {
                byte tile = y < surface + 2 ? (byte)1 :
                    y < surface + 9 ? (byte)2 : (byte)3;
                bool cave = y > surface + 5 &&
                    Math.Abs(_caveNoise.GetNoise2D(x, y)) > 0.36f;
                _tiles[x, y] = cave ? (byte)0 : tile;
            }
            if (x > 5 && x < Width - 5 && rng.NextDouble() < 0.12)
                _trees.Add(x);
        }
    }

    private void BuildColliders()
    {
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
            if (_tiles[x, y] != 0) AddCollision(new Vector2I(x, y));
    }

    private void AddCollision(Vector2I tile)
    {
        var body = new StaticBody2D();
        body.Position = new Vector2((tile.X + 0.5f) * TileSize,
            (tile.Y + 0.5f) * TileSize);
        body.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(TileSize, TileSize) }
        });
        AddChild(body);
        _colliders[tile] = body;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton click && click.Pressed)
        {
            Vector2 mouse = GetGlobalMousePosition();
            if (_player == null || mouse.DistanceTo(_player.GlobalPosition) > 150f) return;
            Vector2I tile = new(Mathf.FloorToInt(mouse.X / TileSize),
                Mathf.FloorToInt(mouse.Y / TileSize));
            if (!InBounds(tile)) return;
            if (click.ButtonIndex == MouseButton.Left && _tiles[tile.X, tile.Y] != 0)
                SetTile(tile, 0);
            else if (click.ButtonIndex == MouseButton.Right && _tiles[tile.X, tile.Y] == 0)
            {
                var tileRect = new Rect2(tile.X * TileSize, tile.Y * TileSize,
                    TileSize, TileSize);
                if (tileRect.Intersects(new Rect2(_player.GlobalPosition - new Vector2(9, 15),
                    new Vector2(18, 30)))) return;
                SetTile(tile, 1);
            }
        }
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.R)
            GetTree().ReloadCurrentScene();
    }

    private static bool InBounds(Vector2I t) =>
        t.X >= 0 && t.X < Width && t.Y >= 0 && t.Y < Height;

    private void SetTile(Vector2I tile, byte value)
    {
        _tiles[tile.X, tile.Y] = value;
        if (_colliders.Remove(tile, out var old)) old.QueueFree();
        if (value != 0) AddCollision(tile);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(-10000, -10000, 30000, 30000),
            Color.FromHtml("#7898a0"));
        // Distant rolling hills provide visual depth without external art assets.
        for (int x = 0; x < Width * TileSize; x += 12)
        {
            float distant = 260 + 26 * Mathf.Sin(x * 0.0014f)
                                + 12 * Mathf.Sin(x * 0.0035f);
            DrawRect(new Rect2(x, distant, 12, 650),
                Color.FromHtml("#68868a"));
            float near = 350 + 34 * Mathf.Sin(x * 0.0025f)
                             + 15 * Mathf.Sin(x * 0.006f);
            DrawRect(new Rect2(x, near, 12, 500),
                Color.FromHtml("#55777a"));
        }

        foreach (int tx in _trees)
        {
            float px = (tx + 0.5f) * TileSize;
            float ground = _surface[tx] * TileSize;
            float tall = 80 + (tx % 6) * 9;
            DrawLine(new Vector2(px, ground), new Vector2(px, ground - tall),
                Color.FromHtml("#483b3a"), 8);
            for (int layer = 0; layer < 3; layer++)
            {
                float top = ground - tall + layer * 24;
                float radius = 21 + layer * 8;
                DrawColoredPolygon(new Vector2[] {
                    new(px, top - 25), new(px - radius, top + 20),
                    new(px + radius, top + 20)
                }, Color.FromHtml(layer % 2 == 0 ? "#294b43" : "#365e4b"));
            }
        }

        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            byte id = _tiles[x, y];
            if (id == 0) continue;
            Color color = id switch {
                1 => Color.FromHtml("#647c4d"),
                2 => Color.FromHtml("#746355"),
                _ => Color.FromHtml("#454753")
            };
            DrawRect(new Rect2(x * TileSize, y * TileSize, TileSize, TileSize), color);
            if (id == 1 && (y == 0 || _tiles[x, y - 1] == 0))
                DrawRect(new Rect2(x * TileSize, y * TileSize, TileSize, 5),
                    Color.FromHtml("#93b879"));
            if (id == 3 && (x * 11 + y * 7) % 19 == 0)
                DrawCircle(new Vector2(x * TileSize + 8, y * TileSize + 12),
                    2, Color.FromHtml("#6b717f"));
        }
    }
}
