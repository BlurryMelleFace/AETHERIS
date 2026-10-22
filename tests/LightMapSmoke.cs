using Godot;
using System;

namespace Aetheris;

/// <summary>Runs the real scene and saves screenshots; pass an output folder after "--".</summary>
public partial class LightMapSmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            string outDir = OS.GetCmdlineUserArgs().Length > 0 ? OS.GetCmdlineUserArgs()[0] : ".";
            var main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            AddChild(main);
            var world = main.GetNode<World>("World");
            var player = main.GetNode<Player>("Player");
            var light = main.GetNode<LightMap>("LightMap");

            for (int i = 0; i < 120; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Shoot(outDir, "1_surface");

            // Find a cave pocket well below the surface.
            Vector2I cave = new(-1, -1);
            for (int y = 32; y < World.Height - 2 && cave.X < 0; y++)
            for (int x = 10; x < World.Width - 10; x++)
                if (!world.IsSolid(x, y) && world.IsSolid(x, y + 1) && !world.IsSolid(x, y - 1))
                { cave = new Vector2I(x, y); break; }
            Require(cave.X >= 0, "Test world must contain a cave.");
            player.GlobalPosition = new Vector2((cave.X + .5f) * World.TileSize, (cave.Y + .5f) * World.TileSize);
            for (int i = 0; i < 90; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Shoot(outDir, "2_cave");

            Vector2I at = new(Mathf.FloorToInt(player.GlobalPosition.X / World.TileSize),
                Mathf.FloorToInt(player.GlobalPosition.Y / World.TileSize));
            Require(light.LightAt(at.X, at.Y) > 0.9f, "Player tile must be lit.");
            Require(light.LightAt(at.X + 12, at.Y) < 0.05f || light.LightAt(at.X - 12, at.Y) < 0.05f,
                "Tiles 12 away in a cave must be dark.");
            Require(light.LightAt(10, 0) > 0.99f, "Open sky must be lit.");

            // A built shelter must shade its interior but keep outer faces lit.
            const int left = 40, houseW = 9, houseH = 6;
            int ground = 0;
            while (!world.IsSolid(left + houseW / 2, ground)) ground++;
            for (int x = left; x < left + houseW; x++)
            for (int y = ground - houseH; y < ground; y++)
            {
                bool wall = x == left || x == left + houseW - 1;
                bool roof = y < ground - houseH + 2;
                if (wall || roof) world.SetTile(new Vector2I(x, y), 1);
            }
            int insideX = left + houseW / 2, insideY = ground - 2;
            player.GlobalPosition = new Vector2((left - 6) * World.TileSize, (ground - 3) * World.TileSize);
            for (int i = 0; i < 60; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Shoot(outDir, "3_building");
            GD.Print($"roof top {light.LightAt(insideX, ground - houseH):F2}, inside {light.LightAt(insideX, insideY):F2}, " +
                $"outer wall {light.LightAt(left, ground - 3):F2}, ground top {light.LightAt(left - 3, ground):F2}");
            GD.Print("PASS: light map smoke test.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Shoot(string dir, string name) =>
        GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
