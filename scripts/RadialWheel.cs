using Godot;

namespace Aetheris;

/// <summary>
/// Hold TAB for a GTA-style item wheel: aim with the mouse, scroll to change category,
/// release to equip. While open the game runs at 0.1x via Engine.TimeScale; the wheel
/// animates on real time so it stays responsive.
/// </summary>
public partial class RadialWheel : CanvasLayer
{
    public const float SlowFactor = 0.1f;
    private const float SlowRampSeconds = 0.1f;
    private const float FadeSeconds = 0.16f;
    private const float SwitchSeconds = 0.24f;
    public const float DeadZone = 34f;

    private ShaderMaterial _blurMaterial = null!;
    private ColorRect _blur = null!;
    private RadialWheelView _view = null!;
    private Player _player = null!;
    private float[] _hover = new float[1];
    private int _category;
    private int _selected;
    private float _open;
    private float _switchTime = SwitchSeconds;
    private int _switchDirection;
    private ulong _lastTicks;

    public bool IsOpen { get; private set; }
    public Player Player => _player;
    public ItemCategory Category => ItemCatalog.Categories[_category];
    public int CategoryIndex => _category;
    public int SelectedIndex => _selected;
    public ItemDefinition Highlighted => Category.Items[_selected];
    public ItemDefinition Equipped => _player.Equipment.Current;
    public Vector2 Pointer { get; private set; }
    public float OpenAmount => _open;
    public float OpenEase => OutCubic(_open);
    public float SwitchProgress => Mathf.Clamp(_switchTime / SwitchSeconds, 0f, 1f);
    public int SwitchDirection => _switchDirection;
    public float HoverAt(int index) => index < _hover.Length ? _hover[index] : 0f;
    public Vector2 Center => GetViewport().GetVisibleRect().Size / 2f;

    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp(t, 0f, 1f), 3f);

    public static float OutBack(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f) - 1f;
        return 1f + 2.2f * t * t * t + 1.2f * t * t;
    }

    public override void _Ready()
    {
        Layer = 10;
        _player = GetParent().GetNode<Player>("Player");
        _blurMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/ui/radial_blur.gdshader") };
        _blur = new ColorRect { Material = _blurMaterial, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _blur.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_blur);
        _view = new RadialWheelView { Visible = false };
        _view.Setup(this);
        AddChild(_view);
        _lastTicks = Time.GetTicksUsec();
        ResetHover();
    }

    public override void _ExitTree() => Engine.TimeScale = 1.0;

    public override void _Notification(int what)
    {
        // A missed key-release (alt-tab) must never leave the game stuck in slow motion.
        if (what == NotificationApplicationFocusOut || what == NotificationWMWindowFocusOut) Close(false);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Keycode: Key.Tab, Echo: false } key)
        {
            if (key.Pressed) Open();
            else Close(true);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!IsOpen) return;

        if (@event is InputEventMouseMotion motion)
        {
            Pointer = motion.Position;
            UpdateSelectionFromPointer();
        }
        else if (@event is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp) ChangeCategory(-1);
            else if (button.ButtonIndex == MouseButton.WheelDown) ChangeCategory(1);
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (IndexOf(Category, Equipped) < 0)
            for (int i = 0; i < ItemCatalog.Categories.Count; i++)
                if (IndexOf(ItemCatalog.Categories[i], Equipped) >= 0) { _category = i; break; }
        _selected = Mathf.Max(0, IndexOf(Category, Equipped));
        ResetHover();
        _switchTime = SwitchSeconds;
        // Start aiming from the middle of the wheel.
        Pointer = Center;
        Input.WarpMouse(new Vector2(DisplayServer.WindowGetSize().X, DisplayServer.WindowGetSize().Y) / 2f);
    }

    public void Close(bool equip)
    {
        if (!IsOpen) return;
        IsOpen = false;
        Engine.TimeScale = 1.0;
        if (equip) _player.Equipment.Equip(Highlighted);
    }

    public void ChangeCategory(int direction)
    {
        int count = ItemCatalog.Categories.Count;
        _category = Mathf.PosMod(_category + direction, count);
        _switchDirection = direction;
        _switchTime = 0f;
        _selected = Mathf.Max(0, IndexOf(Category, Equipped));
        ResetHover();
        UpdateSelectionFromPointer();
    }

    private static int IndexOf(ItemCategory category, ItemDefinition item)
    {
        for (int i = 0; i < category.Items.Count; i++)
            if (category.Items[i] == item) return i;
        return -1;
    }

    private void ResetHover()
    {
        _hover = new float[Category.Items.Count];
        _hover[_selected] = 1f;
    }

    private void UpdateSelectionFromPointer()
    {
        Vector2 offset = Pointer - Center;
        if (offset.Length() < DeadZone) return;
        float step = Mathf.Tau / Category.Items.Count;
        float turns = Mathf.PosMod(offset.Angle() + Mathf.Pi / 2f + step / 2f, Mathf.Tau);
        _selected = Mathf.Clamp((int)(turns / step), 0, Category.Items.Count - 1);
    }

    public override void _Process(double delta)
    {
        // Everything here uses wall-clock time because `delta` shrinks with Engine.TimeScale.
        ulong now = Time.GetTicksUsec();
        float real = Mathf.Min((now - _lastTicks) / 1_000_000f, 0.1f);
        _lastTicks = now;

        if (IsOpen && !Input.IsKeyPressed(Key.Tab)) Close(true);
        if (IsOpen)
            Engine.TimeScale = Mathf.MoveToward((float)Engine.TimeScale, SlowFactor,
                real * (1f - SlowFactor) / SlowRampSeconds);

        _open = Mathf.MoveToward(_open, IsOpen ? 1f : 0f, real / FadeSeconds);
        _switchTime = Mathf.Min(_switchTime + real, SwitchSeconds);
        float follow = 1f - Mathf.Exp(-real * 16f);
        for (int i = 0; i < _hover.Length; i++)
            _hover[i] += ((i == _selected && IsOpen ? 1f : 0f) - _hover[i]) * follow;

        _blur.Visible = _open > 0.001f;
        _blurMaterial.SetShaderParameter("amount", OpenEase);
        _view.Visible = _open > 0.001f;
        _view.Modulate = new Color(1, 1, 1, Mathf.Clamp(_open * 1.6f, 0f, 1f));
    }
}
