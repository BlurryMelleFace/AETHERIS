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
            GD.Print("PASS: day/night scene, timing, wrap, brightness and frame-rate checks.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static bool Near(float actual, float expected) => Math.Abs(actual - expected) < 0.0001f;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
