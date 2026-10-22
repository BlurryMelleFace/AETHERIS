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
    private readonly int[] _firstSolid = new int[Width];
    private readonly TileChunk[,] _chunks = new TileChunk[
        (Width + TileChunk.Size - 1) / TileChunk.Size, (Height + TileChunk.Size - 1) / TileChunk.Size];
    private Node2D? _player;

    public event Action? TilesChanged;

    public bool IsSolid(int x, int y) => InBounds(new Vector2I(x, y)) && _tiles[x, y] != 0;

    public byte GetTile(int x, int y) => _tiles[x, y];

    public bool IsEnclosed(int x, int y) => y > _firstSolid[x];

    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _player = GetParent().GetNodeOrNull<Node2D>("Player");
        _caveNoise.Seed = Seed;
        _caveNoise.Frequency = 0.072f;
        _caveNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _caveNoise.FractalOctaves = 3;
        Generate();
        for (int x = 0; x < Width; x++) _firstSolid[x] = FindFirstSolid(x);
        BuildColliders();
        BuildChunks();
        var roots = new List<Vector2>();
        foreach (int column in _trees)
            roots.Add(new Vector2((column + 0.5f) * TileSize, _surface[column] * TileSize));
        var atmosphere = new TreeAtmosphere { Name = "TreeAtmosphere" };
        atmosphere.Setup(this, roots);
        AddChild(atmosphere);
        QueueRedraw();
    }

    private int FindFirstSolid(int x)
    {
        int y = 0;
        while (y < Height && _tiles[x, y] == 0) y++;
        return y;
    }

    private void BuildChunks()
    {
        for (int cx = 0; cx < _chunks.GetLength(0); cx++)
        for (int cy = 0; cy < _chunks.GetLength(1); cy++)
        {
            var chunk = new TileChunk();
            chunk.Setup(this, new Vector2I(cx, cy));
            AddChild(chunk);
            _chunks[cx, cy] = chunk;
        }
    }

    private void RedrawChunkAt(int x, int y)
    {
        if (InBounds(new Vector2I(x, y)))
            _chunks[x / TileChunk.Size, y / TileChunk.Size].QueueRedraw();
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

    public void SetTile(Vector2I tile, byte value)
    {
        _tiles[tile.X, tile.Y] = value;
        if (_colliders.Remove(tile, out var old)) old.QueueFree();
        if (value != 0) AddCollision(tile);

        // The tile below depends on this one for its look; cave walls depend on the column's top.
        int top = FindFirstSolid(tile.X);
        if (top != _firstSolid[tile.X])
        {
            _firstSolid[tile.X] = top;
            for (int y = 0; y < Height; y += TileChunk.Size) RedrawChunkAt(tile.X, y);
        }
        RedrawChunkAt(tile.X, tile.Y);
        RedrawChunkAt(tile.X, tile.Y + 1);
        TilesChanged?.Invoke();
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    private float _time;

    public override void _Draw()
    {
        float worldWidth = Width * TileSize;
        // Editable procedural backdrop is drawn by the separate ForestBackdrop node.
        // Atmospheric haze connects mountain distance to the playable foreground.
        for (int i = 0; i < 5; i++)
        {
            float y = 315 + i * 43 + Mathf.Sin(_time * .28f + i) * 6;
            DrawRect(new Rect2(0, y, worldWidth, 28),
                new Color(.73f, .81f, .84f, .032f));
        }

        // Ground tiles are drawn by TileChunk children so edits redraw only one chunk.

        // Gentle drifting fireflies.
        for (int i = 0; i < 24; i++)
        {
            float x = 85 + (i * 181) % (Width * TileSize - 170)
                + 4 * Mathf.Sin(_time * .6f + i);
            float y = 275 + 47 * Mathf.Sin(i * 1.1f + _time * .4f);
            float intensity = .32f + .20f * Mathf.Sin(_time * 2 + i);
            DrawCircle(new Vector2(x,y),3,
                new Color(1f,.90f,.60f,intensity*.23f));
            DrawCircle(new Vector2(x,y),1.1f,
                new Color(1f,.96f,.70f,intensity));
        }
    }
}
