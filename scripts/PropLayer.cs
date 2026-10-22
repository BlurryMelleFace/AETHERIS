using Godot;
using System.Collections.Generic;

namespace Aetheris;

/// <summary>One sprite from the props atlas (assets/foliage/props.png, made by tools/make_props.py).</summary>
public sealed class PropArt
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public Rect2 Region { get; init; }

    /// <summary>Plants that bend in the wind; solid props such as rocks and logs stay still.</summary>
    public bool Sways => Kind is "tuft" or "flower" or "fern";

    /// <summary>Large props sit on flat ground only and keep their distance from each other.</summary>
    public bool Big => Kind is "bush" or "rock" or "stump" or "log";
}

public sealed class PropInstance
{
    public PropInstance(PropArt art, int column, int baseRow, float centerX, int id)
    {
        Art = art;
        Column = column;
        BaseRow = baseRow;
        CenterX = centerX;
        Id = id;
    }

    public PropArt Art { get; }
    public int Column { get; }
    public int BaseRow { get; }
    public float CenterX { get; }
    public int Id { get; }
}

public static class PropCatalog
{
    public static Texture2D Atlas { get; } = GD.Load<Texture2D>("res://assets/foliage/props.png");
    public static IReadOnlyList<PropArt> All { get; } = Load();

    private static List<PropArt> Load()
    {
        var list = new List<PropArt>();
        using var file = FileAccess.Open("res://assets/foliage/props.json", FileAccess.ModeFlags.Read);
        var data = Json.ParseString(file.GetAsText()).AsGodotDictionary();
        foreach (Variant item in data["props"].AsGodotArray())
        {
            var entry = item.AsGodotDictionary();
            list.Add(new PropArt
            {
                Name = entry["name"].AsString(),
                Kind = entry["kind"].AsString(),
                Region = new Rect2(entry["x"].AsInt32(), entry["y"].AsInt32(), entry["w"].AsInt32(), entry["h"].AsInt32()),
            });
        }
        return list;
    }

    public static List<PropArt> OfKind(string kind)
    {
        var list = new List<PropArt>();
        foreach (var art in All) if (art.Kind == kind) list.Add(art);
        return list;
    }
}

/// <summary>Draws the forest-floor props (grass, flowers, bushes, rocks...) standing on the terrain.</summary>
public partial class PropLayer : Node2D
{
    private World _world = null!;
    private DayNightCycle? _cycle;
    private double _seconds;
    private bool _dirty = true;

    public void Setup(World world) => _world = world;

    public override void _Ready()
    {
        _cycle = _world.GetParent().GetNodeOrNull<DayNightCycle>("DayNightCycle");
        _world.TilesChanged += () => _dirty = true;
    }

    public override void _Process(double delta)
    {
        _seconds += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Rect2 view = GetViewport().GetVisibleRect();
        Transform2D inverse = GetViewport().GetCanvasTransform().AffineInverse();
        float left = (inverse * view.Position).X - 120f, right = (inverse * view.End).X + 120f;
        foreach (PropInstance prop in _world.Props)
        {
            if (prop.CenterX < left || prop.CenterX > right) continue;
            // A prop needs its ground: skip it once the block under it is mined away or covered.
            if (!_world.IsSolid(prop.Column, prop.BaseRow) || _world.IsSolid(prop.Column, prop.BaseRow - 1)) continue;

            Rect2 region = prop.Art.Region;
            Vector2 foot = new(prop.CenterX, prop.BaseRow * World.TileSize + 1f);
            float lean = prop.Art.Sways ? TreeAtmosphere.WindAt(prop.CenterX, _seconds) * 0.16f
                + Mathf.Sin((float)_seconds * 1.7f + prop.Id) * 0.025f : 0f;
            // Shear about the foot so the plant bends but never leaves the ground.
            DrawSetTransformMatrix(new Transform2D(new Vector2(1, 0), new Vector2(lean, 1), foot));
            DrawTextureRectRegion(PropCatalog.Atlas, new Rect2(-region.Size.X / 2f, -region.Size.Y, region.Size.X, region.Size.Y), region);
        }
        DrawSetTransformMatrix(Transform2D.Identity);
    }
}
