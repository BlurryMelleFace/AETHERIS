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

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    private float _time;

    public override void _Draw()
    {
        float worldWidth = Width * TileSize;
        // Atmospheric sky gradient.
        for (int i = 0; i < 24; i++)
        {
            float t = i / 23f;
            DrawRect(new Rect2(0, i * 55, worldWidth, 57),
                new Color(Mathf.Lerp(.12f, .40f, t), Mathf.Lerp(.19f, .56f, t),
                    Mathf.Lerp(.30f, .63f, t), 1));
        }
        // Sun glow (drawn as nested, gently translucent circles).
        Vector2 sun = new(worldWidth * .76f, 155);
        for (int i = 7; i >= 0; i--)
            DrawCircle(sun, 28 + i * 13, new Color(1f, .78f, .46f, .017f));
        DrawCircle(sun, 26, new Color(1f, .87f, .63f, .84f));

        // Rolling background ridges and distant evergreen canopy.
        for (int x = 0; x < worldWidth; x += 8)
        {
            float far = 265 + 38 * Mathf.Sin(x * .0014f) + 13 * Mathf.Sin(x * .0042f);
            float near = 345 + 31 * Mathf.Sin(x * .0022f) + 17 * Mathf.Sin(x * .006f);
            DrawRect(new Rect2(x, far, 8, 1200), Color.FromHtml("#71878a"));
            DrawRect(new Rect2(x, near, 8, 1200), Color.FromHtml("#4e7274"));
        }

        for (int x = 12; x < worldWidth; x += 29)
        {
            float y = 374 + 19 * Mathf.Sin(x * .007f);
            float h = 35 + (x % 5) * 5;
            DrawLine(new Vector2(x, y), new Vector2(x, y - h), Color.FromHtml("#314d50"), 3);
            DrawColoredPolygon(new Vector2[] {
                new(x, y-h-15), new(x-16, y-6), new(x+16, y-6)
            }, Color.FromHtml("#34575a"));
        }
        // Warm, floating mist bands.
        for (int i = 0; i < 5; i++)
        {
            float y = 310 + i * 43 + Mathf.Sin(_time * .3f + i) * 5;
            DrawRect(new Rect2(0, y, worldWidth, 25),
                new Color(.85f, .91f, .91f, .025f));
        }

        // Foreground trees, now rendered with several detailed foliage layers.
        foreach (int tx in _trees)
        {
            float x = (tx + .5f) * TileSize;
            float ground = _surface[tx] * TileSize;
            float h = 96 + (tx % 7) * 9;
            float sway = Mathf.Sin(_time * .7f + tx) * 2f;
            DrawLine(new Vector2(x, ground), new Vector2(x + sway, ground-h),
                Color.FromHtml("#463b38"), 8);
            for (int layer = 0; layer < 4; layer++)
            {
                float y = ground - h + layer * 24;
                float w = 20 + layer * 9;
                DrawColoredPolygon(new Vector2[] {
                    new(x + sway, y - 30), new(x-w, y+20), new(x+w, y+20)
                }, Color.FromHtml(layer % 2 == 0 ? "#244b43" : "#315e4c"));
                DrawLine(new Vector2(x-w*.6f,y+8),new Vector2(x+sway,y-24),
                    new Color(.54f,.72f,.57f,.20f),1f);
            }
        }

        // Mineable ground, with stone flecks and animated grass.
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            byte id = _tiles[x, y];
            if (id == 0) continue;
            Vector2 at = new(x * TileSize, y * TileSize);
            Color baseColor = id switch {
                1 => Color.FromHtml("#4d6743"),
                2 => Color.FromHtml("#655348"),
                _ => Color.FromHtml("#333a49")
            };
            DrawRect(new Rect2(at,new Vector2(TileSize,TileSize)),baseColor);
            if ((x * 17 + y * 29) % 7 == 0)
                DrawRect(new Rect2(at+new Vector2(4,6),new Vector2(5,3)),
                    new Color(.9f,.9f,1f,.08f));
            if (id == 3 && (x * 11+y*7)%13 == 0)
                DrawCircle(at+new Vector2(9,12),2,Color.FromHtml("#616f7b"));
            if (id == 1 && (y == 0 || _tiles[x,y-1] == 0))
            {
                DrawRect(new Rect2(at.X,at.Y,TileSize,5),Color.FromHtml("#99ba76"));
                for (int blade = 0; blade < 4; blade++)
                {
                    float gx = at.X + 4 + blade * 5;
                    float bend = Mathf.Sin(_time*1.2f + gx*.05f) * 1.2f;
                    DrawLine(new Vector2(gx,at.Y+2),new Vector2(gx+bend,at.Y-5),
                        Color.FromHtml("#98c47f"),1.5f);
                }
                if ((x * 19 + y * 11) % 21 == 0)
                    DrawCircle(at+new Vector2(12,-3),2,Color.FromHtml("#e5cba8"));
            }
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
