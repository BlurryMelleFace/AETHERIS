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
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
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

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    private static readonly Texture2D GrassArt = GD.Load<Texture2D>("res://assets/tiles/grass_top_32.svg");
    private static readonly Texture2D DirtArt = GD.Load<Texture2D>("res://assets/tiles/dirt_32.svg");
    private static readonly Texture2D StoneArt = GD.Load<Texture2D>("res://assets/tiles/stone_32.svg");
    private static readonly Texture2D PineArt = GD.Load<Texture2D>("res://assets/foliage/pine_64x128.svg");
    private static readonly Texture2D FoliageArt = GD.Load<Texture2D>("res://assets/foliage/grass_clump_32.svg");

    private static readonly Texture2D SnowyBackdrop =
        GD.Load<Texture2D>("res://assets/backgrounds/moody_snowy_forest_96.png");

    private float _time;

    public override void _Draw()
    {
        float worldWidth = Width * TileSize;
        // The landscape is part of the SAME world canvas as the foreground.
        // CanvasModulate (DayNightCycle) grades both in one shared pass.
        // Repeat the mountain panorama in horizontal segments for this prototype.
        const float backdropWidth = 1450f;
        const float backdropTop = -370f;
        const float backdropHeight = 1010f;
        for (float x = -backdropWidth; x < worldWidth + backdropWidth; x += backdropWidth)
            DrawTextureRect(SnowyBackdrop,
                new Rect2(x, backdropTop, backdropWidth, backdropHeight), false);
        // Atmospheric haze connects mountain distance to the playable foreground.
        for (int i = 0; i < 5; i++)
        {
            float y = 315 + i * 43 + Mathf.Sin(_time * .28f + i) * 6;
            DrawRect(new Rect2(0, y, worldWidth, 28),
                new Color(.73f, .81f, .84f, .032f));
        }

        // Pixel-art pine sprites; trees remain decorative, not collidable.
        foreach (int tx in _trees)
        {
            float x = (tx + .5f) * TileSize;
            float ground = _surface[tx] * TileSize;
            float h = 115 + (tx % 7) * 10;
            float w = 72 + (tx % 3) * 8;
            float sway = Mathf.Sin(_time * .7f + tx) * 1.4f;
            DrawTextureRect(PineArt,
                new Rect2(x - w / 2 + sway, ground - h, w, h), false);
        }

        // Mineable ground, with stone flecks and animated grass.
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            byte id = _tiles[x, y];
            if (id == 0) continue;
            Vector2 at = new(x * TileSize, y * TileSize);
            bool exposed = y == 0 || _tiles[x, y - 1] == 0;
            Texture2D texture = id == 3 ? StoneArt :
                id == 1 && exposed ? GrassArt : DirtArt;
            DrawTextureRect(texture, new Rect2(at, new Vector2(TileSize, TileSize)),
                false);
            // Variation above ground uses small hand-authored pixel-grid foliage.
            if (id == 1 && exposed && (x * 17 + y * 11) % 6 == 0)
                DrawTextureRect(FoliageArt,
                    new Rect2(at.X - 4, at.Y - 14, 32, 22), false);
        }

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
