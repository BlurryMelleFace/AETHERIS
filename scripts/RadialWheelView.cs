using Godot;
using System.Collections.Generic;

namespace Aetheris;

/// <summary>Draws the wheel. All state lives in RadialWheel; this only renders it.</summary>
public partial class RadialWheelView : Control
{
    private const float OuterRadius = 172f;
    private const float InnerRadius = 88f;
    private const float Gap = 0.045f;

    private static readonly Color Panel = new("#1b1511");
    private static readonly Color PanelHot = new("#4a3322");
    private static readonly Color Edge = new("#0b0806");
    private static readonly Color EdgeHot = new("#f0c070");
    private static readonly Color Ink = new("#f4e6c8");
    private static readonly Color InkDim = new("#a8977a");

    private RadialWheel _wheel = null!;
    private Font _font = null!;

    public void Setup(RadialWheel wheel) => _wheel = wheel;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _font = ThemeDB.FallbackFont;
    }

    public override void _Process(double delta)
    {
        if (Visible) QueueRedraw();
    }

    public override void _Draw()
    {
        if (_wheel == null) return;
        Vector2 center = _wheel.Center;
        float open = _wheel.OpenEase;
        float pop = Mathf.Lerp(0.82f, 1f, RadialWheel.OutBack(_wheel.OpenAmount));
        var category = _wheel.Category;
        int count = category.Items.Count;
        float switched = RadialWheel.OutBack(_wheel.SwitchProgress);
        float switchFade = Mathf.Clamp(_wheel.SwitchProgress * 3f, 0f, 1f);

        DrawSetTransform(center, 0f, new Vector2(pop, pop));
        DrawCircleFilled(Vector2.Zero, InnerRadius - 6f, new Color(Panel, 0.82f), Edge);

        for (int i = 0; i < count; i++)
            DrawSlot(i, count, category.Items[i], switched, switchFade);

        DrawCenterReadout();
        DrawPointer();
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        DrawCategoryTabs(center, open);
        DrawFooter(center, open);
    }

    private void DrawSlot(int index, int count, ItemDefinition item, float switched, float switchFade)
    {
        float step = Mathf.Tau / count;
        float mid = -Mathf.Pi / 2f + index * step;
        float hover = _wheel.HoverAt(index);
        float grow = Mathf.Max(0f, switched);
        Vector2 push = Vector2.FromAngle(mid) * (hover * 9f);

        var shape = Ring(mid - step / 2f + Gap, mid + step / 2f - Gap, InnerRadius * grow, OuterRadius * grow, push);
        Color fill = Panel.Lerp(PanelHot, hover);
        fill.A = Mathf.Lerp(0.78f, 0.92f, hover) * switchFade;
        DrawColoredPolygon(shape, fill);
        var outline = new List<Vector2>(shape) { shape[0] };
        DrawPolyline(outline.ToArray(), new Color(Edge, switchFade), 3f);
        if (hover > 0.02f)
            DrawPolyline(outline.ToArray(), new Color(EdgeHot, hover * switchFade), 2f);

        // Whole-number scale keeps the pixel art crisp.
        float radius = (InnerRadius + OuterRadius) / 2f * grow;
        int scale = hover > 0.5f ? 4 : 3;
        Vector2 at = Vector2.FromAngle(mid) * radius + push + new Vector2(0f, -Mathf.Round(hover * 2f));
        float size = PixelPen.Size * scale;
        Color tint = item.Available ? Colors.White : new Color(0.35f, 0.35f, 0.4f, 0.8f);
        tint.A *= switchFade;
        DrawTextureRect(item.Icon, new Rect2((at - new Vector2(size, size) / 2f).Round(), new Vector2(size, size)), false, tint);

        if (item == _wheel.Equipped)
        {
            // Small gold pip on the outer edge marks what is currently held.
            Vector2 pip = Vector2.FromAngle(mid) * (OuterRadius - 10f) * grow + push;
            DrawRect(new Rect2(pip - new Vector2(4, 4), new Vector2(8, 8)), new Color(Edge, switchFade));
            DrawRect(new Rect2(pip - new Vector2(3, 3), new Vector2(6, 6)), new Color("#f0c070") with { A = switchFade });
        }
    }

    private void DrawCenterReadout()
    {
        var item = _wheel.Highlighted;
        float bob = Mathf.Round(Mathf.Sin(Time.GetTicksMsec() / 1000f * 3f) * 2f);
        float size = PixelPen.Size * 5;
        Color tint = item.Available ? Colors.White : new Color(0.4f, 0.4f, 0.45f, 0.85f);
        DrawTextureRect(item.Icon, new Rect2(new Vector2(-size / 2f, -size / 2f + bob).Round(), new Vector2(size, size)), false, tint);
    }

    private void DrawPointer()
    {
        Vector2 offset = _wheel.Pointer - _wheel.Center;
        if (offset.Length() < RadialWheel.DeadZone) return;
        Vector2 dir = offset.Normalized();
        Vector2 tip = (dir * (InnerRadius - 14f)).Round();
        DrawLine(Vector2.Zero + dir * 52f, tip, new Color(EdgeHot, 0.55f), 2f);
        DrawRect(new Rect2(tip - new Vector2(4, 4), new Vector2(8, 8)), Edge);
        DrawRect(new Rect2(tip - new Vector2(3, 3), new Vector2(6, 6)), EdgeHot);
    }

    private void DrawCategoryTabs(Vector2 center, float open)
    {
        int count = ItemCatalog.Categories.Count;
        float slide = (1f - RadialWheel.OutBack(_wheel.SwitchProgress)) * -_wheel.SwitchDirection * 70f;
        float y = center.Y - OuterRadius - 44f;
        for (int offset = -1; offset <= 1; offset++)
        {
            int index = Mathf.PosMod(_wheel.CategoryIndex + offset, count);
            string name = ItemCatalog.Categories[index].Name.ToUpperInvariant();
            bool current = offset == 0;
            Color color = current ? Ink : InkDim;
            color.A = (current ? 1f : 0.55f) * open;
            int fontSize = current ? 26 : 17;
            float x = center.X + offset * 190f + slide;
            DrawStringOutline(_font, new Vector2(x - 100f, y + (current ? 0 : 4)), name,
                HorizontalAlignment.Center, 200f, fontSize, 6, new Color(Edge, open));
            DrawString(_font, new Vector2(x - 100f, y + (current ? 0 : 4)), name,
                HorizontalAlignment.Center, 200f, fontSize, color);
        }
        // One pip per category under the title.
        for (int i = 0; i < count; i++)
        {
            bool on = i == _wheel.CategoryIndex;
            var pip = new Rect2(center.X + (i - (count - 1) / 2f) * 16f - 4f, y + 12f, 8f, 8f);
            DrawRect(pip, new Color(on ? EdgeHot : InkDim, (on ? 1f : 0.5f) * open));
        }
    }

    private void DrawFooter(Vector2 center, float open)
    {
        var item = _wheel.Highlighted;
        float y = center.Y + OuterRadius + 46f;
        DrawStringOutline(_font, new Vector2(center.X - 200f, y), item.Name, HorizontalAlignment.Center, 400f, 28, 8, new Color(Edge, open));
        DrawString(_font, new Vector2(center.X - 200f, y), item.Name, HorizontalAlignment.Center, 400f, 28, new Color(Ink, open));

        string status = !item.Available ? "NOT AVAILABLE YET"
            : item == _wheel.Equipped ? "EQUIPPED" : item.Description;
        DrawString(_font, new Vector2(center.X - 200f, y + 26f), status, HorizontalAlignment.Center, 400f, 16,
            new Color(item.Available ? InkDim : new Color("#c47a6a"), open));
        DrawString(_font, new Vector2(center.X - 300f, y + 64f),
            "Move the mouse to aim    Scroll to change category    Release TAB to equip",
            HorizontalAlignment.Center, 600f, 13, new Color(InkDim, 0.7f * open));
    }

    private void DrawCircleFilled(Vector2 at, float radius, Color fill, Color edge)
    {
        DrawCircle(at, radius + 2f, edge);
        DrawCircle(at, radius, fill);
    }

    private static Vector2[] Ring(float from, float to, float inner, float outer, Vector2 offset)
    {
        const int steps = 28;
        var points = new Vector2[(steps + 1) * 2];
        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.Lerp(from, to, i / (float)steps);
            points[i] = Vector2.FromAngle(angle) * outer + offset;
            points[points.Length - 1 - i] = Vector2.FromAngle(angle) * inner + offset;
        }
        return points;
    }
}
