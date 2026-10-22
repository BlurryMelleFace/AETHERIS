using Godot;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Aetheris;

/// <summary>Checks the Terraria-style proportions in the real scene; pass a screenshot folder (and optionally "map") after "--".</summary>
public partial class TerrainScaleSmoke : Node
{
    private Node2D _main = null!;
    private Player _player = null!;
    private World _world = null!;
    private string _out = ".";

    public override async void _Ready()
    {
        try
        {
            string[] args = OS.GetCmdlineUserArgs();
            if (args.Length > 0) _out = args[0];
            _main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            AddChild(_main);
            _player = _main.GetNode<Player>("Player");
            _world = _main.GetNode<World>("World");
            _player.GetNode<Camera2D>("Camera2D").PositionSmoothingEnabled = false;

            // --- Proportions ---
            float widthTiles = Player.BodySize.X / World.TileSize, heightTiles = Player.BodySize.Y / World.TileSize;
            Require(World.TileSize == 24, "Block size must stay unchanged.");
            Require(widthTiles > 1.4f && widthTiles < 2f, $"Character must be slightly under 2 blocks wide ({widthTiles}).");
            Require(Mathf.Abs(heightTiles - 3f) < 0.01f, "Character must be 3 blocks tall.");
            var shape = (RectangleShape2D)_player.GetNode<CollisionShape2D>("CollisionShape2D").Shape;
            Require(shape.Size == Player.BodySize, "Collision must match the character size.");

            // --- Natural terrain: spawn, trees, camera framing ---
            await RealSeconds(2.0);
            Require(_player.IsOnFloor(), "Player must land on the generated terrain.");
            await Shoot("scale_surface");
            Vector2 view = GetViewport().GetVisibleRect().Size / _player.GetNode<Camera2D>("Camera2D").Zoom;
            GD.Print($"PERF view {view.X / World.TileSize:F1} x {view.Y / World.TileSize:F1} tiles, character {heightTiles / (view.Y / World.TileSize) * 100:F0}% of screen height");

            CheckTrees();
            CheckCaves(args.Length > 1 && args[1] == "map");

            // --- Physics arena: flat floor with narrow and wide shafts. ---
            for (int x = 95; x < 145; x++)
            for (int y = 4; y < 52; y++)
                _world.SetTile(new Vector2I(x, y), (byte)(y >= 30 ? 3 : 0));
            for (int y = 30; y < 45; y++)
            {
                foreach (int x in new[] { 130, 135, 136 }) _world.SetTile(new Vector2I(x, y), 0);
            }
            await RealSeconds(0.3);
            float standY = 30 * World.TileSize - Player.BodySize.Y / 2f;

            // Walking on a flat floor of separate blocks must not snag.
            Teleport(100 * World.TileSize, standY - 2f);
            await RealSeconds(0.3);
            float startX = _player.GlobalPosition.X;
            Hold(Key.D, true);
            await RealSeconds(1.0);
            Hold(Key.D, false);
            float walked = _player.GlobalPosition.X - startX;
            GD.Print($"PERF walked {walked:F0}px in 1s (speed {_player.WalkSpeed})");
            Require(walked > _player.WalkSpeed * 0.9f, "Walking along flat blocks must not snag on seams.");

            // One-block ledges are climbed automatically.
            for (int x = 112; x < 118; x++) _world.SetTile(new Vector2I(x, 29), 1);
            Teleport(108 * World.TileSize, standY - 2f);
            await RealSeconds(0.3);
            Hold(Key.D, true);
            ulong give = Time.GetTicksMsec() + 3000;
            while (_player.GlobalPosition.X < 114.5f * World.TileSize && Time.GetTicksMsec() < give)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Hold(Key.D, false);
            await RealSeconds(0.4);
            Require(_player.GlobalPosition.X > 114 * World.TileSize, "A one-block step must be climbed without jumping.");
            Require(Mathf.Abs(_player.GlobalPosition.Y - (standY - World.TileSize)) < 3f, "The player must stand on top of the step.");
            for (int x = 112; x < 118; x++) _world.SetTile(new Vector2I(x, 29), 0);

            // A two-block wall blocks walking but not jumping.
            _world.SetTile(new Vector2I(120, 29), 1);
            _world.SetTile(new Vector2I(120, 28), 1);
            Teleport(116 * World.TileSize, standY - 2f);
            await RealSeconds(0.3);
            Hold(Key.D, true);
            await RealSeconds(1.2);
            float wallStop = 120 * World.TileSize - Player.BodySize.X / 2f + 1.5f;
            Require(_player.GlobalPosition.X < wallStop, "A two-block wall must stop a walking player.");
            Hold(Key.Space, true);
            await RealSeconds(1.5);
            Hold(Key.Space, false);
            Hold(Key.D, false);
            Require(_player.GlobalPosition.X > 121 * World.TileSize, "The player must be able to jump over a two-block wall.");

            // Jump height.
            Teleport(100 * World.TileSize, standY - 2f);
            await RealSeconds(0.4);
            float peak = _player.GlobalPosition.Y;
            Hold(Key.Space, true);
            ulong end = Time.GetTicksMsec() + 700;
            while (Time.GetTicksMsec() < end)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                peak = Mathf.Min(peak, _player.GlobalPosition.Y);
            }
            Hold(Key.Space, false);
            float jumpTiles = (standY - peak) / World.TileSize;
            GD.Print($"PERF jump height {jumpTiles:F2} tiles");
            Require(jumpTiles > 3.4f && jumpTiles < 4.8f, "Jump should clear roughly four blocks.");

            // A 1-block gap cannot be fallen through; a 2-block gap can.
            Teleport(130.5f * World.TileSize, standY - 2f);
            await RealSeconds(1.5);
            Require(_player.GlobalPosition.Y < standY + 8f, "A 1-block gap must not let the player through.");
            Teleport(136f * World.TileSize, standY - 2f);
            await RealSeconds(2.0);
            Require(_player.GlobalPosition.Y > 44 * World.TileSize - Player.BodySize.Y / 2f - 10f,
                "A 2-block gap must let the player fall through.");
            await Shoot("scale_shaft");

            // Mining one block of ground is not enough; two adjacent blocks are.
            Teleport(100.5f * World.TileSize, standY - 2f);
            await RealSeconds(0.4);
            _world.SetTile(new Vector2I(100, 30), 0);
            await RealSeconds(1.0);
            Require(_player.GlobalPosition.Y < standY + 8f, "Mining a single block must not drop the player.");
            _world.SetTile(new Vector2I(101, 30), 0);
            Teleport(101f * World.TileSize, standY - 2f);
            await RealSeconds(1.0);
            Require(_player.GlobalPosition.Y > standY + 12f, "Mining two adjacent blocks must let the player drop in.");

            GD.Print("PASS: character scale, collision, step-up, jump, gap widths, trees and caves.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void CheckTrees()
    {
        Require(_world.Trees.Count >= 8, $"The forest needs trees ({_world.Trees.Count}).");
        var atmosphere = _world.GetNode<TreeAtmosphere>("TreeAtmosphere");
        int spriteCount = 0;
        foreach (Node child in atmosphere.GetChildren()) if (child is Sprite2D) spriteCount++;
        Require(spriteCount == _world.Trees.Count, "Every tree needs a sprite.");

        foreach (TreeInstance tree in _world.Trees)
        {
            Require(tree.HeightTiles >= 10 && tree.HeightTiles <= 14, "Trees are 10-14 blocks tall.");
            Require(_world.IsSolid(tree.TrunkColumn, tree.BaseRow), "A tree trunk must stand on ground.");
            Require(!_world.IsSolid(tree.TrunkColumn, tree.BaseRow - 1), "Trees must not be buried.");
        }

        TreeInstance sample = _world.Trees[0];
        Image art = TreeArt.Create(sample).GetImage();
        Require(art.GetWidth() == TreeArt.WidthPixels && art.GetHeight() == TreeArt.HeightPixels(sample),
            "Tree art must be authored at its drawn size so it is never stretched.");
        int row = art.GetHeight() - 3 * World.TileSize, first = -1, last = -1;
        for (int x = 0; x < art.GetWidth(); x++)
            if (art.GetPixel(x, row).A > 0.5f) { if (first < 0) first = x; last = x; }
        int trunkWidth = last - first + 1;
        Require(trunkWidth >= 16 && trunkWidth < World.TileSize, $"The trunk must be a little under one block wide ({trunkWidth}px).");

        Require(_world.TreeAtTile(sample.TrunkColumn, sample.BaseRow - 1) == sample, "The trunk footprint must be targetable.");
        Require(_world.TreeAtTile(sample.TrunkColumn + 1, sample.BaseRow - 1) != sample
            && _world.TreeAtTile(sample.TrunkColumn - 1, sample.BaseRow - 1) != sample, "The trunk footprint is exactly one block wide.");
        _world.FellTree(sample);
        Require(_world.TreeAtTile(sample.TrunkColumn, sample.BaseRow - 1) == null, "A felled tree has no trunk left.");
        int remaining = 0;
        foreach (Node child in atmosphere.GetChildren()) if (child is Sprite2D && !child.IsQueuedForDeletion()) remaining++;
        Require(remaining == _world.Trees.Count - 1, "Felling a tree removes its sprite.");
    }

    private void CheckCaves(bool printMap)
    {
        // A spot the character fits: a 2x3 block of air below the dirt layer.
        int roomy = 0, open = 0;
        for (int y = 30; y < World.Height - 3; y++)
        for (int x = 2; x < World.Width - 5; x++)
        {
            if (!_world.IsSolid(x, y)) open++;
            bool fits = true;
            for (int dx = 0; dx < 2 && fits; dx++)
            for (int dy = 0; dy < 3 && fits; dy++)
                if (_world.IsSolid(x + dx, y - dy)) fits = false;
            if (fits) roomy++;
        }
        GD.Print($"PERF caves: {open} open underground tiles, {roomy} spots that fit the character");
        if (printMap)
        {
            var map = new StringBuilder();
            for (int y = 12; y < World.Height; y += 2)
            {
                for (int x = 0; x < World.Width; x += 2) map.Append(_world.IsSolid(x, y) ? '#' : ' ');
                map.AppendLine();
            }
            GD.Print(map.ToString());
        }
        Require(roomy > 40, "Caves must be large enough for the character.");
    }

    private void Teleport(float x, float y)
    {
        _player.GlobalPosition = new Vector2(x, y);
        _player.Velocity = Vector2.Zero;
        _player.ResetPhysicsInterpolation();
    }

    private static void Hold(Key key, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
        Input.FlushBufferedEvents();
    }

    private async Task RealSeconds(double seconds)
    {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (Time.GetTicksMsec() < end) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
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
