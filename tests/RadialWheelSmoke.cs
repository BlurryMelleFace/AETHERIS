using Godot;
using System;
using System.Threading.Tasks;

namespace Aetheris;

/// <summary>Drives the real scene through the wheel, slow motion, torch and glow rules; pass a screenshot folder after "--".</summary>
public partial class RadialWheelSmoke : Node
{
    private Node2D _main = null!;
    private Player _player = null!;
    private RadialWheel _wheel = null!;
    private string _out = ".";

    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Length > 0) _out = OS.GetCmdlineUserArgs()[0];
            _main = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Node2D>();
            AddChild(_main);
            _player = _main.GetNode<Player>("Player");
            _wheel = _main.GetNode<RadialWheel>("RadialWheel");
            // Without camera smoothing the canvas offset shows exactly what the physics interpolation renders.
            _player.GetNode<Camera2D>("Camera2D").PositionSmoothingEnabled = false;
            var cycle = _main.GetNode<DayNightCycle>("DayNightCycle");
            await RealSeconds(1.0);

            Require(Math.Abs(Engine.TimeScale - 1.0) < 1e-9 && !_wheel.IsOpen, "Game starts at normal speed with the wheel closed.");
            Require(_player.Equipment.Current == ItemCatalog.Hand && _player.HeldLight == 0f, "Player starts empty-handed without light.");

            // --- Slow motion: the same fall must cover far less distance with TAB held. ---
            float normalFall = await Fall(false);
            float slowFall = await Fall(true);
            GD.Print($"PERF fall in 0.5s real time: normal {normalFall:F1}px, slow {slowFall:F2}px");
            Require(normalFall > 40f, "Test fall should be substantial at normal speed.");
            Require(slowFall < normalFall * 0.1f, "Falling with TAB held must be dramatically slower.");
            Require(Math.Abs(Engine.TimeScale - RadialWheel.SlowFactor) < 0.001, "Time scale must settle at 0.1x while open.");
            Press(Key.Tab, false);
            Require(Math.Abs(Engine.TimeScale - 1.0) < 1e-9, "Releasing TAB must restore normal speed instantly.");
            await RealSeconds(0.4);

            // --- Smooth rendering of the slowed player (physics runs at ~6 ticks per second). ---
            Teleport(new Vector2(2880, 60));
            Press(Key.Tab, true);
            await RealSeconds(0.3);
            int frames = 0, moved = 0;
            float previous = RenderedY();
            ulong end = Time.GetTicksMsec() + 800;
            while (Time.GetTicksMsec() < end)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                float now = RenderedY();
                frames++;
                if (Math.Abs(now - previous) > 0.0005f) moved++;
                previous = now;
            }
            GD.Print($"PERF slow-motion frames with visible motion: {moved}/{frames}");
            Require(moved > frames * 0.6f, "The slowed player must glide smoothly, not step at the physics rate.");
            Press(Key.Tab, false);
            await RealSeconds(0.5);

            // --- Aim, scroll categories, equip the torch. ---
            Press(Key.Tab, true);
            await RealSeconds(0.4);
            Aim(new Vector2(110, 62));
            Require(_wheel.Category.Id == "tools" && _wheel.Highlighted == ItemCatalog.Torch, "Aiming lower right must highlight the torch.");
            Scroll(MouseButton.WheelDown);
            Require(_wheel.Category.Id == "weapons", "Scrolling down must move to the next category.");
            Scroll(MouseButton.WheelUp);
            Scroll(MouseButton.WheelUp);
            Require(_wheel.Category.Id == "consumables", "Scrolling wraps around the categories.");
            Scroll(MouseButton.WheelDown);
            Require(_wheel.Category.Id == "tools", "Scrolling back returns to tools.");
            Aim(new Vector2(110, 62));
            await RealSeconds(0.6);
            await Shoot("wheel_open");
            Image openImage = await Capture();
            Press(Key.Tab, false);
            await RealSeconds(0.5);
            Require(_player.Equipment.Current == ItemCatalog.Torch, "Releasing TAB equips the highlighted torch.");
            Require(_player.HeldLight > 0.9f, "A held torch emits light.");
            Require(!_wheel.IsOpen && _wheel.OpenAmount < 0.001f, $"The wheel fades out after release (open={_wheel.IsOpen}, amount={_wheel.OpenAmount}).");
            Image closedImage = await Capture();

            // --- Background blur: high-frequency detail must drop while the wheel is open. ---
            float sharpOpen = Sharpness(openImage), sharpClosed = Sharpness(closedImage);
            GD.Print($"PERF background sharpness open {sharpOpen:F4} vs closed {sharpClosed:F4}");
            Require(sharpOpen < sharpClosed * 0.7f, "The world behind the wheel must be blurred.");
            Require(Brightness(openImage) < Brightness(closedImage) * 0.8f, "The world behind the wheel must be darkened.");

            // --- Locked items cannot be equipped; empty hand removes the torch and its light. ---
            Press(Key.Tab, true);
            await RealSeconds(0.3);
            Scroll(MouseButton.WheelDown);
            Aim(new Vector2(110, 62));
            Require(_wheel.Highlighted == ItemCatalog.Categories[1].Items[1] && !_wheel.Highlighted.Available, "Sword is a locked placeholder.");
            Press(Key.Tab, false);
            await RealSeconds(0.1);
            Require(_player.Equipment.Current == ItemCatalog.Torch, "Releasing on a locked item keeps the current equipment.");
            Press(Key.Tab, true);
            await RealSeconds(0.3);
            Aim(new Vector2(0, -110));
            Require(_wheel.Highlighted == ItemCatalog.Hand, "Aiming up selects the empty hand.");
            Press(Key.Tab, false);
            await RealSeconds(0.1);
            Require(_player.Equipment.Current == ItemCatalog.Hand && _player.HeldLight == 0f, "Empty hand removes the torch light.");

            // --- Night: the character never glows; the torch lights its surroundings warmly. ---
            cycle.SetProcess(false);
            cycle._Process(90);
            _main.GetNode<CanvasLayer>("UI").Hide();
            await RealSeconds(0.6);
            await Shoot("night_hand");
            Image nightHand = await Capture();
            _player.Equipment.Equip(ItemCatalog.Torch);
            await RealSeconds(0.6);
            await Shoot("night_torch");
            Image nightTorch = await Capture();
            var (handLuma, handWarmth) = Region(nightHand);
            var (torchLuma, torchWarmth) = Region(nightTorch);
            GD.Print($"PERF night region brightness hand {handLuma:F3} torch {torchLuma:F3}, warmth {handWarmth:F3} -> {torchWarmth:F3}");
            Require(torchLuma > handLuma + 0.04f, "The torch must brighten its surroundings at night.");
            Require(torchWarmth > handWarmth + 0.03f, "Torchlight must be warm.");

            GD.Print("PASS: wheel input, categories, slow motion, blur, torch equip/unequip, no glow.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Teleport(Vector2 position)
    {
        _player.GlobalPosition = position;
        _player.Velocity = Vector2.Zero;
        _player.ResetPhysicsInterpolation();
    }

    private async Task<float> Fall(bool slow)
    {
        Teleport(new Vector2(2880, 60));
        if (slow) Press(Key.Tab, true);
        await RealSeconds(0.2);
        float start = _player.GlobalPosition.Y;
        await RealSeconds(0.5);
        float distance = _player.GlobalPosition.Y - start;
        if (!slow) await RealSeconds(0.3);
        return distance;
    }

    private float RenderedY() => GetViewport().GetCanvasTransform().Origin.Y;

    private static void Press(Key key, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
        Input.FlushBufferedEvents();
    }

    private void Aim(Vector2 fromCenter)
    {
        Vector2 position = _wheel.Center + fromCenter;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
        Input.FlushBufferedEvents();
    }

    private static void Scroll(MouseButton button)
    {
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true });
        Input.FlushBufferedEvents();
    }

    private async Task RealSeconds(double seconds)
    {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (Time.GetTicksMsec() < end) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task<Image> Capture()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        return GetViewport().GetTexture().GetImage();
    }

    private async Task Shoot(string name) => (await Capture()).SavePng($"{_out}/{name}.png");

    // Mean neighbour difference on the left side of the screen, away from the wheel and HUD text.
    private static float Sharpness(Image image)
    {
        float total = 0f; int samples = 0;
        for (int y = 160; y < 560; y += 2)
        for (int x = 20; x < 300; x += 2)
        {
            Color a = image.GetPixel(x, y), b = image.GetPixel(x + 1, y);
            total += Mathf.Abs(a.R - b.R) + Mathf.Abs(a.G - b.G) + Mathf.Abs(a.B - b.B);
            samples++;
        }
        return total / samples;
    }

    private static float Brightness(Image image)
    {
        float total = 0f; int samples = 0;
        for (int y = 160; y < 560; y += 4)
        for (int x = 20; x < 300; x += 4) { Color c = image.GetPixel(x, y); total += c.R + c.G + c.B; samples++; }
        return total / samples;
    }

    // Brightness and red-over-blue warmth within ~110px of the screen centre, where the player stands.
    private (float luma, float warmth) Region(Image image)
    {
        Vector2I c = new(image.GetWidth() / 2, image.GetHeight() / 2);
        float luma = 0f, warmth = 0f; int samples = 0;
        for (int y = c.Y - 110; y < c.Y + 110; y += 3)
        for (int x = c.X - 110; x < c.X + 110; x += 3)
        {
            Color p = image.GetPixel(x, y);
            luma += (p.R + p.G + p.B) / 3f;
            warmth += p.R - p.B;
            samples++;
        }
        return (luma / samples, warmth / samples);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
