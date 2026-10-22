using Godot;
using System;

namespace Aetheris;

/// <summary>Exercises the actual game scene and clock, without waiting two minutes.</summary>
public partial class DayNightCycleSmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            var main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            AddChild(main);
            var cycle = main.GetNode<DayNightCycle>("DayNightCycle");
            cycle.SetProcess(false);
            Require(cycle.DayLengthSeconds == 120f, "Default cycle must last 120 seconds.");
            Require(Near(cycle.Phase, 0f), "Scene must start at sunrise.");
            Color dawn = cycle.Color;

            cycle._Process(30);
            Require(Near(cycle.Phase, 0.25f) && Near(cycle.Daylight, 1f), "Noon at 30 seconds.");
            Color noon = cycle.Color;
            cycle._Process(30);
            Require(Near(cycle.Phase, 0.5f), "Sunset at 60 seconds.");
            cycle._Process(30);
            Require(Near(cycle.Phase, 0.75f) && Near(cycle.Daylight, 0f), "Midnight at 90 seconds.");
            Require(cycle.Color.R < noon.R && cycle.Color.G < noon.G && cycle.Color.B < noon.B,
                "Night must dim all world channels.");
            Require(cycle.Color.R > 0.3f && cycle.Color.G > 0.3f, "Night must remain readable.");
            // Render the moon/stars path as well as the daylight path.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            cycle._Process(29.999);
            Color beforeWrap = cycle.Color;
            cycle._Process(0.001);
            Require(Near(cycle.Phase, 0f), "Cycle must wrap at 120 seconds.");
            Require(Math.Abs(beforeWrap.R - cycle.Color.R) < 0.001f
                && Math.Abs(beforeWrap.G - cycle.Color.G) < 0.001f
                && Math.Abs(beforeWrap.B - cycle.Color.B) < 0.001f, "No flash at sunrise boundary.");
            Require(Near(cycle.Color.R, dawn.R) && Near(cycle.Color.G, dawn.G)
                && Near(cycle.Color.B, dawn.B), "Next sunrise must match the initial lighting.");

            // Variable frame sizes and long frames must preserve elapsed time.
            for (int i = 0; i < 600; i++) cycle._Process(0.05);
            Require(Near(cycle.Phase, 0.25f), "Clock must be frame-rate independent.");
            cycle._Process(1200);
            Require(Near(cycle.Phase, 0.25f), "Clock must handle multiple cycles in one step.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var sky = main.GetNode<SkyBackdrop>("Sky/SkyBackdrop");
            sky.SetProcess(false);
            sky._Process(9);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            string[] output = OS.GetCmdlineUserArgs();
            if (output.Length > 0)
                GetViewport().GetTexture().GetImage().SavePng($"{output[0]}/sky_forest.png");

            var player = main.GetNode<Player>("Player");
            player.SetPhysicsProcess(false);
            player.GetNode<Camera2D>("Camera2D").Enabled = false;
            var forest = main.GetNode<ForestBackdrop>("ForestBackdrop");
            forest.SetProcess(false);
            main.GetNode<Node2D>("World").Hide();
            main.GetNode<Node2D>("LightMap").Hide();
            main.GetNode<CanvasLayer>("UI").Hide();
            player.Hide();
            forest.QueueRedraw();
            sky.QueueRedraw();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Image stationary = GetViewport().GetTexture().GetImage();
            player.Position += new Vector2(200, 80);
            forest.QueueRedraw();
            sky.QueueRedraw();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Require(CountChangedPixels(stationary, GetViewport().GetTexture().GetImage()) == 0,
                "Player motion must not move the backdrop independently of the rendered camera.");

            Transform2D originalCanvas = GetViewport().CanvasTransform;
            Transform2D scrolledCanvas = originalCanvas;
            scrolledCanvas.Origin -= new Vector2(160, 0);
            GetViewport().CanvasTransform = scrolledCanvas;
            forest.QueueRedraw();
            sky.QueueRedraw();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Image scrolled = GetViewport().GetTexture().GetImage();
            Require(CountChangedPixels(stationary, scrolled) > 100,
                "Rendered camera movement must scroll the parallax layers.");
            if (output.Length > 0) scrolled.SavePng($"{output[0]}/backdrop_scrolled.png");
            GetViewport().CanvasTransform = originalCanvas;

            foreach (Node child in main.GetChildren())
            {
                if (child is CanvasItem item) item.Hide();
                if (child is CanvasLayer layer && child.Name == "UI") layer.Hide();
            }
            sky._Process(23);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Image quiet = GetViewport().GetTexture().GetImage();
            Require(CountBirdPixels(quiet) == 0, "Birds must leave a quiet gap between flocks.");
            sky._Process(15);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Image flying = GetViewport().GetTexture().GetImage();
            Require(CountBirdPixels(flying) > 10, "A daylight flock must render visible bird silhouettes.");
            int changed = 0;
            for (int vertical = 0; vertical < quiet.GetHeight() / 3; vertical++)
            for (int horizontal = 0; horizontal < quiet.GetWidth(); horizontal++)
                if (quiet.GetPixel(horizontal, vertical) != flying.GetPixel(horizontal, vertical)) changed++;
            Require(changed > 100, "Clouds and birds must move across the rendered sky.");
            if (output.Length > 0) flying.SavePng($"{output[0]}/sky_birds.png");
            GD.Print("PASS: day/night, moving clouds, bird flocks and camera-synchronized parallax.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static int CountChangedPixels(Image before, Image after)
    {
        int count = 0;
        for (int vertical = 0; vertical < before.GetHeight(); vertical++)
        for (int horizontal = 0; horizontal < before.GetWidth(); horizontal++)
            if (before.GetPixel(horizontal, vertical) != after.GetPixel(horizontal, vertical)) count++;
        return count;
    }

    private static int CountBirdPixels(Image image)
    {
        int count = 0;
        for (int vertical = image.GetHeight() * 15 / 100; vertical < image.GetHeight() * 35 / 100; vertical++)
        for (int horizontal = 0; horizontal < image.GetWidth(); horizontal++)
        {
            Color pixel = image.GetPixel(horizontal, vertical);
            if (pixel.R < 0.3f && pixel.G < 0.4f && pixel.B < 0.45f) count++;
        }
        return count;
    }

    private static bool Near(float actual, float expected) => Math.Abs(actual - expected) < 0.0001f;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
