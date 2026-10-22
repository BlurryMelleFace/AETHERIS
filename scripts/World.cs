using Godot;
using System;
using System.Collections.Generic;

namespace Aetheris;

/// <summary>
/// Small editable-world prototype. Not yet a streaming, optimized chunk engine.
/// </summary>
public partial class World : Node2D
{
    public const int TileSize = 24;
    public const int Width = 160;
    public const int Height = 48;
    private const int Seed = 10421;
    private const int GroundStart = 16;
    private const int SoilDepth = 10;

    private readonly byte[,] _tiles = new byte[Width, Height];
    private readonly Dictionary<Vector2I, StaticBody2D> _colliders = new();

    public override void _Ready()
    {
        Generate();
        BuildColliders();
        QueueRedraw();
    }

    private void Generate()
    {
        for (int x = 0; x < Width; x++)
        {
            int surface = GroundStart + (int)Math.Round(
                3 * Math.Sin(x * 0.085) + 1.4 * Math.Sin(x * 0.24));
            for (int y = surface; y < Height; y++)
                _tiles[x, y] = y < surface + 3 ? (byte)1 :
                    y < surface + SoilDepth ? (byte)2 : (byte)3;
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
        var body = new StaticBody2D { Name = $"Tile_{tile.X}_{tile.Y}" };
        var shape = new CollisionShape2D { Shape = new RectangleShape2D
            { Size = new Vector2(TileSize, TileSize) } };
        body.Position = new Vector2((tile.X + 0.5f) * TileSize,
            (tile.Y + 0.5f) * TileSize);
        body.AddChild(shape);
        AddChild(body);
        _colliders[tile] = body;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton click && click.Pressed)
        {
            Vector2I tile = ToTile(GetGlobalMousePosition());
            if (!InBounds(tile)) return;
            if (click.ButtonIndex == MouseButton.Left && _tiles[tile.X, tile.Y] is 1 or 2)
                SetTile(tile, 0);
            else if (click.ButtonIndex == MouseButton.Right && _tiles[tile.X, tile.Y] == 0)
                SetTile(tile, 1);
        }
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.R)
            GetTree().ReloadCurrentScene();
    }

    private static Vector2I ToTile(Vector2 position) =>
        new(Mathf.FloorToInt(position.X / TileSize), Mathf.FloorToInt(position.Y / TileSize));

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
        DrawRect(new Rect2(-10000, -10000, 30000, 30000), Color.FromHtml("#182e42"));
        DrawRect(new Rect2(0, 0, Width * TileSize, GroundStart * TileSize),
            Color.FromHtml("#567d89"));
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            byte id = _tiles[x, y];
            if (id == 0) continue;
            string hex = id switch { 1 => "#66844c", 2 => "#535463", _ => "#2a2d3a" };
            DrawRect(new Rect2(x * TileSize, y * TileSize, TileSize, TileSize),
                Color.FromHtml(hex));
            if (id == 1 && (y == 0 || _tiles[x, y - 1] == 0))
                DrawRect(new Rect2(x * TileSize, y * TileSize, TileSize, 4),
                    Color.FromHtml("#9fcf73"));
        }
    }
}
