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
    private Player? _player;
    private RadialWheel? _wheel;
    private readonly Dictionary<Vector2I, (float damage, double time)> _damage = new();
    private double _clock;
    private float _toolCooldown;
    private float _pendingTimer = -1f;
    private Action? _pendingHit;

    /// <summary>Hits a tree trunk block takes before it is cut through.</summary>
    public const float TreeBlockHealth = 4f;
    private const float DamageMemorySeconds = 1.6f;

    public event Action? TilesChanged;
    public event Action<TreeInstance>? TreeFelled;

    /// <summary>A tree was cut: the tree, how many trunk blocks remain standing, and the direction the top falls (-1 or 1).</summary>
    public event Action<TreeInstance, int, float>? TreeCut;

    public event Action<TreeInstance, int>? TreeHit;

    public IReadOnlyList<TreeInstance> Trees => _trees;
    public IReadOnlyList<PropInstance> Props => _props;
    public double Clock => _clock;

    /// <summary>Lets tests aim tools without moving the real OS pointer.</summary>
    public Vector2? PointerOverride { get; set; }

    private Vector2 Pointer => PointerOverride ?? GetGlobalMousePosition();

    /// <summary>Hits a log or stump takes before it breaks apart.</summary>
    public const float WoodPropHealth = 3f;
    private const float PickSeconds = 0.3f;

    /// <summary>True while a prop still has its ground: not removed, standing on a block with open air above.</summary>
    public bool PropStands(PropInstance prop) =>
        prop.Alive && IsSolid(prop.Column, prop.BaseRow) && !IsSolid(prop.Column, prop.BaseRow - 1);

    /// <summary>The standing prop under a world point (nearest centre wins) whose kind passes the filter.</summary>
    public PropInstance? PropAt(Vector2 point, Func<PropArt, bool> accept)
    {
        PropInstance? best = null;
        float bestDistance = float.MaxValue;
        foreach (PropInstance prop in _props)
        {
            if (!accept(prop.Art) || !PropStands(prop)) continue;
            Vector2 size = prop.Art.Region.Size;
            var box = new Rect2(prop.CenterX - size.X / 2f - 3f, prop.BaseRow * TileSize + 1f - size.Y - 3f, size.X + 6f, size.Y + 6f);
            if (!box.HasPoint(point)) continue;
            float distance = Mathf.Abs(prop.CenterX - point.X);
            if (distance < bestDistance) { best = prop; bestDistance = distance; }
        }
        return best;
    }

    public static bool IsPickable(PropArt art) => art.Kind is "flower" or "mushroom";

    public static bool IsChoppable(PropArt art) => art.Kind is "log" or "stump";

    /// <summary>Picks a flower or mushroom by hand.</summary>
    public bool PickProp(PropInstance prop)
    {
        if (!prop.Alive || !IsPickable(prop.Art)) return false;
        prop.Alive = false;
        Vector2 size = prop.Art.Region.Size;
        ImpactEffects.Burst(this, new Vector2(prop.CenterX, prop.BaseRow * TileSize - size.Y * 0.5f),
            ImpactEffects.PlantColors(prop.Art.Name), 8, Vector2.Up, 120f, 60f, 0.5f, 520f, 2f);
        return true;
    }

    /// <summary>One axe swing at a log or stump; returns true when it breaks apart.</summary>
    public bool HitProp(PropInstance prop, float power)
    {
        if (!prop.Alive || !IsChoppable(prop.Art)) return false;
        Vector2 size = prop.Art.Region.Size;
        Vector2 center = new(prop.CenterX, prop.BaseRow * TileSize - size.Y * 0.5f);
        prop.Damage += power;
        prop.HitTime = _clock;
        if (prop.Damage < WoodPropHealth)
        {
            ImpactEffects.Burst(this, center, ImpactEffects.Wood, 4, Vector2.Up, 110f, 85f, 0.4f);
            return false;
        }
        prop.Alive = false;
        ImpactEffects.Burst(this, center, ImpactEffects.Wood, 14, Vector2.Up, 170f, 100f);
        return true;
    }

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
        _player = GetParent().GetNodeOrNull<Player>("Player");
        _wheel = GetParent().GetNodeOrNull<RadialWheel>("RadialWheel");
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
        // Mining and chopping are held-mouse tool actions; right-click places a block.
        if (@event is InputEventMouseButton click && click.Pressed && click.ButtonIndex == MouseButton.Right)
        {
            Vector2 mouse = Pointer;
            if (_player == null || mouse.DistanceTo(_player.GlobalPosition) > ReachTiles * TileSize) return;
            Vector2I tile = new(Mathf.FloorToInt(mouse.X / TileSize),
                Mathf.FloorToInt(mouse.Y / TileSize));
            if (!InBounds(tile) || _tiles[tile.X, tile.Y] != 0) return;
            var tileRect = new Rect2(tile.X * TileSize, tile.Y * TileSize, TileSize, TileSize);
            if (tileRect.Intersects(new Rect2(_player.GlobalPosition - Player.BodySize / 2f, Player.BodySize))) return;
            SetTile(tile, 1);
        }
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.R)
            GetTree().ReloadCurrentScene();
    }

    public static float Hardness(byte tile) => tile switch { 3 => 3f, 4 => 5f, _ => 1f };

    /// <summary>0 for an untouched block, then 1-3 as cracks spread across a block being mined.</summary>
    public int DamageStage(int x, int y)
    {
        if (!_damage.TryGetValue(new Vector2I(x, y), out var entry)) return 0;
        float ratio = entry.damage / Hardness(_tiles[x, y]);
        return ratio <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(ratio * 3f), 1, 3);
    }

    /// <summary>The standing tree (or stump) rooted in this block, if any; its ground cannot be mined away.</summary>
    public TreeInstance? TreeRootedAt(int x, int y)
    {
        foreach (TreeInstance tree in _trees)
            if (!tree.Felled && tree.TrunkColumn == x && tree.BaseRow == y) return tree;
        return null;
    }

    /// <summary>Applies one pickaxe swing to a block; returns true when the block breaks.</summary>
    public bool HitTile(Vector2I tile, float power)
    {
        if (!InBounds(tile) || _tiles[tile.X, tile.Y] == 0) return false;
        if (TreeRootedAt(tile.X, tile.Y) != null) return false;
        byte id = _tiles[tile.X, tile.Y];
        float total = (_damage.TryGetValue(tile, out var entry) ? entry.damage : 0f) + power;
        Vector2 center = new((tile.X + 0.5f) * TileSize, (tile.Y + 0.5f) * TileSize);
        if (total >= Hardness(id))
        {
            ImpactEffects.Burst(this, center, ImpactEffects.ChipsFor(id), 10, Vector2.Up, 150f);
            SetTile(tile, 0);
            return true;
        }
        _damage[tile] = (total, _clock);
        ImpactEffects.Burst(this, center, ImpactEffects.ChipsFor(id), 4, Vector2.Up, 110f, 85f, 0.4f);
        RedrawAround(tile.X, tile.Y);
        return false;
    }

    /// <summary>Applies one axe swing to the trunk block at a row. Cutting the bottom block fells the whole tree
    /// (leaving a stump); cutting higher up fells only the part above the cut.</summary>
    public bool HitTree(TreeInstance tree, int row, float power, float fallDirection)
    {
        int block = tree.BlockIndex(row);
        if (!tree.OccupiesTrunk(tree.TrunkColumn, row)) return false;
        Vector2 center = new((tree.TrunkColumn + 0.5f) * TileSize, (row + 0.5f) * TileSize);
        float total = tree.AddDamage(block, power);
        if (total < TreeBlockHealth)
        {
            ImpactEffects.Burst(this, center, ImpactEffects.Wood, 4, new Vector2(-fallDirection, -0.4f), 110f, 70f, 0.4f);
            TreeHit?.Invoke(tree, block);
            return false;
        }
        ImpactEffects.Burst(this, center, ImpactEffects.Wood, 12, new Vector2(-fallDirection, -0.5f), 160f);
        if (tree.IsStump && block == 0)
        {
            FellTree(tree);
            return true;
        }
        // Bottom block: everything above it falls and a one-block stump stays; otherwise the cut block and all above fall.
        int remaining = Math.Max(block, 1);
        float direction = fallDirection == 0f ? 1f : fallDirection;
        tree.CutTo(remaining);
        TreeCut?.Invoke(tree, remaining, direction);
        return true;
    }

    private void UpdateTools(float dt)
    {
        if (_pendingHit != null)
        {
            _pendingTimer -= dt;
            if (_pendingTimer <= 0f)
            {
                var hit = _pendingHit;
                _pendingHit = null;
                hit();
            }
        }

        _toolCooldown = Mathf.Max(0f, _toolCooldown - dt);
        if (_player == null) return;
        ItemDefinition tool = _player.Equipment.Current;
        bool bareHand = tool.Tool == ToolKind.None && tool == ItemCatalog.Hand;
        if ((tool.Tool == ToolKind.None && !bareHand) || _toolCooldown > 0f || _wheel?.IsOpen == true) return;
        if (!Input.IsMouseButtonPressed(MouseButton.Left)) return;

        Vector2 mouse = Pointer;
        if (bareHand)
        {
            if (mouse.DistanceTo(_player.GlobalPosition) > ReachTiles * TileSize) return;
            if (PropAt(mouse, IsPickable) is not { } plant) return;
            _toolCooldown = PickSeconds;
            _player.Swing(mouse.X, PickSeconds);
            PickProp(plant);
            return;
        }

        _toolCooldown = tool.UseSeconds;
        _player.Swing(mouse.X, tool.UseSeconds);
        if (mouse.DistanceTo(_player.GlobalPosition) > ReachTiles * TileSize) return;
        Vector2I tile = new(Mathf.FloorToInt(mouse.X / TileSize), Mathf.FloorToInt(mouse.Y / TileSize));
        if (!InBounds(tile)) return;

        // The blow lands partway through the swing, as the tool comes down.
        _pendingTimer = tool.UseSeconds * 0.5f;
        Vector2 playerAt = _player.GlobalPosition;
        if (tool.Tool == ToolKind.Pickaxe)
            _pendingHit = () => HitTile(tile, tool.Power);
        else if (tool.Tool == ToolKind.Axe && TreeAtTile(tile.X, tile.Y) is { } tree)
            _pendingHit = () => HitTree(tree, tile.Y, tool.Power, Math.Sign(tree.Root.X - playerAt.X));
        else if (tool.Tool == ToolKind.Axe && PropAt(mouse, IsChoppable) is { } wood)
            _pendingHit = () => HitProp(wood, tool.Power);
    }

    private void ForgetOldDamage()
    {
        List<Vector2I>? stale = null;
        foreach (var (tile, entry) in _damage)
            if (_clock - entry.time > DamageMemorySeconds) (stale ??= new()).Add(tile);
        if (stale == null) return;
        foreach (Vector2I tile in stale)
        {
            _damage.Remove(tile);
            RedrawAround(tile.X, tile.Y);
        }
    }

    private static bool InBounds(Vector2I t) =>
        t.X >= 0 && t.X < Width && t.Y >= 0 && t.Y < Height;

    public void SetTile(Vector2I tile, byte value)
    {
        _tiles[tile.X, tile.Y] = value;
        _damage.Remove(tile);
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
        _clock += delta;
        UpdateTools((float)delta);
        ForgetOldDamage();
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
