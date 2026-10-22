using Godot;
using System;

namespace Aetheris;

/// <summary>Records the hero running across flat ground so the animation can be reviewed; pass an output folder after "--".</summary>
public partial class HeroWalkCapture : Node
{
    public override async void _Ready()
    {
        try
        {
            string folder = OS.GetCmdlineUserArgs()[0];
            var main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            AddChild(main);
            var player = main.GetNode<Player>("Player");
            var world = main.GetNode<World>("World");
            player.GetNode<Camera2D>("Camera2D").PositionSmoothingEnabled = false;
            for (int x = 95; x < 145; x++)
            for (int y = 4; y < 40; y++) world.SetTile(new Vector2I(x, y), (byte)(y >= 30 ? 3 : 0));
            player.GlobalPosition = new Vector2(98 * World.TileSize, 30 * World.TileSize - 40f);
            player.ResetPhysicsInterpolation();
            await Wait(0.6);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.D, PhysicalKeycode = Key.D, Pressed = true });
            Input.FlushBufferedEvents();
            for (int i = 0; i < 12; i++)
            {
                if (i == 6)
                {
                    Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = true });
                    Input.FlushBufferedEvents();
                }
                await Wait(0.07);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Image full = GetViewport().GetTexture().GetImage();
                full.GetRegion(new Rect2I((int)(full.GetWidth() / 2f) - 90, (int)(full.GetHeight() / 2f) - 100, 180, 200))
                    .SavePng($"{folder}/walk_{i:D2}.png");
            }
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task Wait(double seconds)
    {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (Time.GetTicksMsec() < end) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
}
