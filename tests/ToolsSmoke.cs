using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aetheris;

/// <summary>Mining and chopping in the real scene; pass a screenshot folder after "--".</summary>
public partial class ToolsSmoke : Node
{
    private Node2D _main = null!;
    private Player _player = null!;
    private World _world = null!;
    private TreeAtmosphere _atmosphere = null!;
    private string _out = ".";

    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Length > 0) _out = OS.GetCmdlineUserArgs()[0];
            _main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            AddChild(_main);
            _player = _main.GetNode<Player>("Player");
            _world = _main.GetNode<World>("World");
            _atmosphere = _world.GetNode<TreeAtmosphere>("TreeAtmosphere");
            _player.GetNode<Camera2D>("Camera2D").PositionSmoothingEnabled = false;
            await Seconds(1.5);

            // --- The wheel offers both tools. ---
            Require(ItemCatalog.Pickaxe.Available && ItemCatalog.Axe.Available, "Pickaxe and axe are real items now.");
            Require(ItemCatalog.Categories[0].Items.Contains(ItemCatalog.Pickaxe) && ItemCatalog.Categories[0].Items.Contains(ItemCatalog.Axe),
                "The Tools category lists the pickaxe and axe.");

            // --- Block hardness: dirt breaks at once, stone and deep stone need several swings, cracks show progress. ---
            var spot = new Vector2I(60, 12);
            _world.SetTile(spot, 1);
            Require(_world.HitTile(spot, 1f), "Dirt breaks in one swing.");
            _world.SetTile(spot, 3);
            Require(!_world.HitTile(spot, 1f) && _world.DamageStage(spot.X, spot.Y) == 1, "Stone cracks on the first swing.");
            Require(!_world.HitTile(spot, 1f) && _world.DamageStage(spot.X, spot.Y) == 2, "The cracks spread on the second swing.");
            Require(_world.HitTile(spot, 1f) && !_world.IsSolid(spot.X, spot.Y), "Stone breaks on the third swing.");
            _world.SetTile(spot, 4);
            for (int i = 0; i < 4; i++) Require(!_world.HitTile(spot, 1f), "Deep stone takes five swings.");
            Require(_world.HitTile(spot, 1f), "Deep stone breaks on the fifth swing.");
            _world.SetTile(spot, 3);
            _world.HitTile(spot, 1f);
            await Seconds(2.2);
            Require(_world.DamageStage(spot.X, spot.Y) == 0, "Cracks fade when the block is left alone.");
            _world.SetTile(spot, 0);

            // --- A held mouse mines with the pickaxe but not with an empty hand. ---
            await BuildArena();
            Vector2I target = new(104, 30);
            Teleport(100 * World.TileSize, 30 * World.TileSize - Player.BodySize.Y / 2f - 2f);
            await Seconds(0.5);
            await Press(WorldCenter(target), 0.9);
            Require(_world.IsSolid(target.X, target.Y), "An empty hand cannot mine.");
            _player.Equipment.Equip(ItemCatalog.Pickaxe);
            await Press(WorldCenter(target), 0.7);
            Require(!_world.IsSolid(target.X, target.Y), "Holding the mouse with the pickaxe mines the block.");
            Require(!_player.Hero.FlipH, "The player turns toward the block being mined.");
            // The axe does nothing to ground.
            _player.Equipment.Equip(ItemCatalog.Axe);
            await Press(WorldCenter(new Vector2I(104, 31)), 1.0);
            Require(_world.IsSolid(104, 31) && _world.DamageStage(104, 31) == 0, "The axe cannot mine blocks.");

            // --- Trees: pick three with a tall enough trunk. ---
            var candidates = new List<TreeInstance>();
            foreach (TreeInstance tree in _world.Trees)
                if (tree.Variant.TrunkRows >= 5 && tree.TrunkColumn > 12 && tree.TrunkColumn < World.Width - 12) candidates.Add(tree);
            Require(candidates.Count >= 3, "The forest has trees tall enough to chop.");
            TreeInstance low = candidates[0], high = candidates[1], held = candidates[2];

            // The pickaxe cannot chop and the axe misses the canopy above the trunk.
            Require(!_world.HitTree(low, low.BaseRow - 1 - low.Variant.TrunkRows, 1f, 1f), "Only trunk blocks can be chopped.");
            int falling = FallingCount();

            // Cutting the bottom block fells the whole tree and leaves a stump.
            for (int i = 0; i < 3; i++) Require(!_world.HitTree(low, low.BaseRow - 1, 1f, 1f), "A trunk block takes four swings.");
            Require(_world.HitTree(low, low.BaseRow - 1, 1f, 1f), "The fourth swing cuts through.");
            Require(low.Cut && low.IsStump && low.Remaining == 1, "Cutting the bottom leaves only a stump.");
            Require(FallingCount() == falling + 1, "The whole tree topples.");
            Require(_world.TreeAtTile(low.TrunkColumn, low.BaseRow - 1) == low && _world.TreeAtTile(low.TrunkColumn, low.BaseRow - 2) == null,
                "Only the stump block remains choppable.");
            await Seconds(3.4);
            Require(FallingCount() == falling, "The fallen tree clears away after landing.");
            for (int i = 0; i < 4; i++) _world.HitTree(low, low.BaseRow - 1, 1f, 1f);
            Require(low.Felled && _world.TreeAtTile(low.TrunkColumn, low.BaseRow - 1) == null, "Chopping the stump removes it.");

            // Cutting further up fells only the part above the cut; the lower trunk stays standing.
            int cutBlock = 3;
            int cutRow = high.BaseRow - 1 - cutBlock;
            for (int i = 0; i < 4; i++) _world.HitTree(high, cutRow, 1f, -1f);
            Require(high.Cut && !high.IsStump && high.Remaining == cutBlock, "A higher cut leaves the lower trunk.");
            Require(_world.TreeAtTile(high.TrunkColumn, high.BaseRow - cutBlock) == high && _world.TreeAtTile(high.TrunkColumn, cutRow) == null,
                "Blocks below the cut stay, the cut block and above are gone.");
            Require(FallingCount() == falling + 1, "The top part falls.");
            var visual = FindSprite(high);
            Require(visual != null && visual.RegionEnabled, "Only the lower trunk is still drawn.");
            Teleport(high.Root.X + 110f, high.Root.Y - Player.BodySize.Y / 2f - 2f);
            await Seconds(1.2);
            await Shoot("tree_cut_high");
            await Seconds(2.4);
            // The remaining trunk can still be cut lower, felling the rest.
            for (int i = 0; i < 4; i++) _world.HitTree(high, high.BaseRow - 1, 1f, 1f);
            Require(high.IsStump && high.Remaining == 1, "Cutting the bottom of a cut trunk leaves a stump.");
            Require(FallingCount() == falling + 1, "The rest of the trunk topples.");
            await Seconds(3.4);
            Require(FallingCount() == falling, "The felled trunk clears away.");

            // --- A held mouse with the axe chops a real tree; the top falls away from the player. ---
            Teleport(held.Root.X - 70f, held.Root.Y - Player.BodySize.Y / 2f - 2f);
            await Seconds(0.6);
            _player.Equipment.Equip(ItemCatalog.Axe);
            Vector2I trunkTile = new(held.TrunkColumn, held.BaseRow - 1);
            await Shoot("axe_ready");
            Task press = Press(WorldCenter(trunkTile), 3.0, keepHolding: true);
            await Seconds(0.5);
            await Shoot("axe_swing");
            ulong limit = Time.GetTicksMsec() + 4500;
            while (!held.Cut && Time.GetTicksMsec() < limit) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(held.Cut, "Holding the mouse with the axe chops the tree down.");
            Require(!_player.Hero.FlipH, "The player faces the tree while chopping.");
            await Seconds(0.9);
            await Shoot("tree_falling");
            FallingTree piece = FindFalling();
            Require(piece.Rotation > 0.15f, "The tree falls away from the player.");
            await press;
            await Seconds(1.8);
            await Shoot("tree_felled");

            GD.Print("PASS: pickaxe hardness and cracks, axe chopping, bottom and higher cuts, falling trees, held-mouse tools.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task BuildArena()
    {
        for (int x = 95; x < 145; x++)
        for (int y = 4; y < 52; y++) _world.SetTile(new Vector2I(x, y), (byte)(y >= 30 ? 2 : 0));
        await Seconds(0.3);
    }

    private int FallingCount()
    {
        int count = 0;
        foreach (Node child in _atmosphere.GetChildren()) if (child is FallingTree && !child.IsQueuedForDeletion()) count++;
        return count;
    }

    private FallingTree FindFalling()
    {
        foreach (Node child in _atmosphere.GetChildren()) if (child is FallingTree tree) return tree;
        throw new InvalidOperationException("No tree is falling.");
    }

    private Sprite2D? FindSprite(TreeInstance tree)
    {
        foreach (Node child in _atmosphere.GetChildren())
            if (child is Sprite2D sprite && sprite.Position.DistanceTo(tree.Root) < 1f) return sprite;
        return null;
    }

    private Vector2 WorldCenter(Vector2I tile) => new((tile.X + 0.5f) * World.TileSize, (tile.Y + 0.5f) * World.TileSize);

    private Vector2 ScreenOf(Vector2 world) => GetViewport().GetCanvasTransform() * world;

    private async Task Press(Vector2 worldPoint, double seconds, bool keepHolding = false)
    {
        Vector2 screen = ScreenOf(worldPoint);
        // The viewport reads the real pointer, so the test moves it (the window and viewport are the same size here).
        Input.WarpMouse(screen);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen });
        Input.FlushBufferedEvents();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Seconds(seconds);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = screen });
        Input.FlushBufferedEvents();
        await Seconds(0.15);
    }

    private void Teleport(float x, float y)
    {
        _player.GlobalPosition = new Vector2(x, y);
        _player.Velocity = Vector2.Zero;
        _player.ResetPhysicsInterpolation();
    }

    private async Task Seconds(double seconds)
    {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (Time.GetTicksMsec() < end) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Shoot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{_out}/{name}.png");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
