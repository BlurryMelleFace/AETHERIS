using Godot;
using System;
using System.Collections.Generic;

namespace Aetheris;

/// <summary>Data describing one equippable item. Add new items in ItemCatalog; no other code changes needed.</summary>
public sealed class ItemDefinition
{
    private readonly Action<PixelPen> _paintIcon;
    private ImageTexture? _icon;

    public ItemDefinition(string id, string name, string description, Action<PixelPen> paintIcon)
    {
        Id = id;
        Name = name;
        Description = description;
        _paintIcon = paintIcon;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }

    /// <summary>False for placeholders that appear in the wheel but cannot be equipped yet.</summary>
    public bool Available { get; init; } = true;

    /// <summary>0 means the item emits no light; 1 is a full torch.</summary>
    public float LightStrength { get; init; }
    public Color LightColor { get; init; } = Colors.White;

    public Texture2D Icon => _icon ??= PixelIcon.Create(_paintIcon);
}

public sealed class ItemCategory
{
    public ItemCategory(string id, string name, IEnumerable<ItemDefinition> items)
    {
        Id = id;
        Name = name;
        Items = new List<ItemDefinition>(items);
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<ItemDefinition> Items { get; }
}
