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
    public const int Width = 240;
    public const int Height = 80;
    private const int Seed = 10421;
    private const int GroundStart = 19;
    private const float ReachTiles = 6f;
    private const float TunnelWidth = 0.13f;
    private const float CavernThreshold = 0.3f;
    private static readonly RectangleShape2D TileShape = new() { Size = new Vector2(TileSize, TileSize) };
    private readonly byte[,] _tiles = new byte[Width, Height];
    private readonly int[] _surface = new int[Width];
    private readonly Dictionary<Vector2I, StaticBody2D> _colliders = new();
    private readonly List<TreeInstance> _trees = new();
    private readonly List<PropInstance> _props = new();
    private readonly FastNoiseLite _caveNoise = new();
    private readonly FastNoiseLite _cavernNoise = new();
    private readonly FastNoiseLite _pocketNoise = new();
    private readonly int[] _firstSolid = new int[Width];
    private readonly TileChunk[,] _chunks = new TileChunk[
        (Width + TileChunk.Size - 1) / TileChunk.Size, (Height + TileChunk.Size - 1) / TileChunk.Size];
    private Node2D? _player;

    public event Action? TilesChanged;
    public event Action<TreeInstance>? TreeFelled;

    public IReadOnlyList<TreeInstance> Trees => _trees;
    public IReadOnlyList<PropInstance> Props => _props;

    public TreeInstance? TreeAtTile(int x, int y)
    {
        foreach (TreeInstance tree in _trees)
            if (tree.OccupiesTrunk(x, y)) return tree;
        return null;
    }

    public void FellTree(TreeInstance tree)
    {
        if (tree.Felled) return;
        tree.Felled = true;
        TreeFelled?.Invoke(tree);
    }

    public bool IsSolid(int x, int y) => InBounds(new Vector2I(x, y)) && _tiles[x, y] != 0;

    public byte GetTile(int x, int y) => _tiles[x, y];

    public bool IsEnclosed(int x, int y) => y > _firstSolid[x];

    public int DepthBelowSurface(int x, int y) => y - _firstSolid[x];

    public override void _Ready()
    {
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _player = GetParent().GetNodeOrNull<Node2D>("Player");
        _caveNoise.Seed = Seed;
        _caveNoise.Frequency = 0.018f;
        _caveNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _caveNoise.FractalOctaves = 2;
        _cavernNoise.Seed = Seed + 1;
        _cavernNoise.Frequency = 0.03f;
        _cavernNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _cavernNoise.FractalOctaves = 2;
        _pocketNoise.Seed = Seed + 2;
        _pocketNoise.Frequency = 0.09f;
        _pocketNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _pocketNoise.FractalOctaves = 2;
        Generate();
        for (int x = 0; x < Width; x++) _firstSolid[x] = FindFirstSolid(x);
        BuildColliders();
        BuildChunks();
        var atmosphere = new TreeAtmosphere { Name = "TreeAtmosphere" };
        atmosphere.Setup(this, _trees);
        AddChild(atmosphere);
        var propLayer = new PropLayer { Name = "PropLayer" };
        propLayer.Setup(this);
        AddChild(propLayer);
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

    // Tile edges, grass corners and cave walls depend on the neighbours, so redraw all of them.
    private void RedrawAround(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++) RedrawChunkAt(x + dx, y + dy);
    }

    private void Generate()
    {
        var rng = new Random(Seed);
        for (int x = 0; x < Width; x++)
            _surface[x] = GroundStart + (int)Math.Round(
                4.5 * Math.Sin(x * 0.05) + 2.0 * Math.Sin(x * 0.17));

        for (int x = 0; x < Width; x++)
        {
            int surface = _surface[x];
            for (int y = surface; y < Height; y++)
            {
                int depth = y - surface;
                byte tile = depth < 2 ? (byte)1 : depth < 9 ? (byte)2 : depth < 30 ? (byte)3 : (byte)4;
                // Stone pockets in the soil and soil pockets in the rock keep the layers from looking flat.
                float pocket = _pocketNoise.GetNoise2D(x, y);
                if (depth >= 3 && depth < 9 && pocket > 0.32f) tile = 3;
                else if (depth >= 11 && depth < 30 && pocket < -0.36f) tile = 2;
                // Winding tunnels plus open caverns, sized for a 3x6 block character.
                bool cave = y > surface + 9 && y < Height - 3 &&
                    (Math.Abs(_caveNoise.GetNoise2D(x, y)) < TunnelWidth
                    || _cavernNoise.GetNoise2D(x, y) > CavernThreshold);
                _tiles[x, y] = cave ? (byte)0 : tile;
            }
        }

        // Trees stand on a single 1-block trunk column with some room from the previous tree.
        int lastTree = -100;
        for (int x = 8; x < Width - 10; x++)
        {
            if (x - lastTree < 6) continue;
            if (rng.NextDouble() > 0.22) continue;
            _trees.Add(new TreeInstance(_trees.Count, x, _surface[x], TreeCatalog.Pick(rng.NextDouble())));
            lastTree = x;
        }
        PlaceProps();
    }

    // Ground cover and rocks; large props need level ground and keep clear of trunks and each other.
    private void PlaceProps()
    {
        var rng = new Random(Seed + 77);
        var tufts = PropCatalog.OfKind("tuft");
        var flowers = PropCatalog.OfKind("flower");
        var ferns = PropCatalog.OfKind("fern");
        var bushes = PropCatalog.OfKind("bush");
        var rocks = PropCatalog.OfKind("rock");
        var mushrooms = PropCatalog.OfKind("mushroom");
        var stumps = PropCatalog.OfKind("stump");
        var logs = PropCatalog.OfKind("log");
        var taken = new List<(float lo, float hi)>();
        foreach (TreeInstance tree in _trees) taken.Add((tree.Root.X - 22f, tree.Root.X + 22f));

        bool Level(int column, int span)
        {
            int from = column - span / 2, to = from + span - 1;
            if (from < 2 || to >= Width - 2) return false;
            for (int c = from; c <= to; c++) if (_surface[c] != _surface[column]) return false;
            return true;
        }

        bool Free(float lo, float hi)
        {
            foreach (var (a, b) in taken) if (lo < b && hi > a) return false;
            return true;
        }

        T Pick<T>(List<T> list) => list[rng.Next(list.Count)];

        for (int x = 3; x < Width - 3; x++)
        {
            double roll = rng.NextDouble();
            float centerX = (x + 0.5f) * TileSize + (float)(rng.NextDouble() - 0.5) * 12f;
            PropArt? art = roll < 0.012 ? Pick(logs)
                : roll < 0.030 ? Pick(stumps)
                : roll < 0.085 ? Pick(bushes)
                : roll < 0.125 ? Pick(rocks)
                : roll < 0.155 ? Pick(mushrooms)
                : roll < 0.235 ? Pick(ferns)
                : roll < 0.42 ? Pick(flowers)
                : roll < 0.88 ? Pick(tufts) : null;
            if (art == null) continue;

            if (art.Big)
            {
                int span = Mathf.CeilToInt(art.Region.Size.X / TileSize);
                if (art.Kind == "log") { span = 4; centerX = (x + 0.5f) * TileSize; }
                if (!Level(x, span)) continue;
                float half = art.Region.Size.X / 2f + 2f;
                if (!Free(centerX - half, centerX + half)) continue;
                taken.Add((centerX - half, centerX + half));
            }
            else
            {
                float half = art.Region.Size.X / 2f;
                bool nearTrunk = false;
                foreach (TreeInstance tree in _trees) if (Math.Abs(tree.Root.X - centerX) < 14f) nearTrunk = true;
                if (nearTrunk) continue;
            }
            _props.Add(new PropInstance(art, x, _surface[x], centerX, _props.Count));
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
        body.AddChild(new CollisionShape2D { Shape = TileShape });
        AddChild(body);
        _colliders[tile] = body;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton click && click.Pressed)
        {
            Vector2 mouse = GetGlobalMousePosition();
            if (_player == null || mouse.DistanceTo(_player.GlobalPosition) > ReachTiles * TileSize) return;
            Vector2I tile = new(Mathf.FloorToInt(mouse.X / TileSize),
                Mathf.FloorToInt(mouse.Y / TileSize));
            if (!InBounds(tile)) return;
            if (click.ButtonIndex == MouseButton.Left && _tiles[tile.X, tile.Y] != 0)
                SetTile(tile, 0);
            else if (click.ButtonIndex == MouseButton.Right && _tiles[tile.X, tile.Y] == 0)
            {
                var tileRect = new Rect2(tile.X * TileSize, tile.Y * TileSize,
                    TileSize, TileSize);
                if (tileRect.Intersects(new Rect2(_player.GlobalPosition - Player.BodySize / 2f, Player.BodySize))) return;
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
        RedrawAround(tile.X, tile.Y);
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
