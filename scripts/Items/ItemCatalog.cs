using Godot;
using System.Collections.Generic;

namespace Aetheris;

public static class ItemCatalog
{
    public static readonly ItemDefinition Hand = new("hand", "Empty Hand",
        "Nothing equipped.", PaintHand);

    public static readonly ItemDefinition Torch = new("torch", "Torch",
        "A warm, flickering light.", PaintTorch)
    {
        LightStrength = 1f,
        LightColor = new Color(1f, 0.68f, 0.36f)
    };

    private static readonly ItemDefinition Pickaxe = new("pickaxe", "Pickaxe",
        "Coming soon.", PaintPickaxe) { Available = false };
    private static readonly ItemDefinition Sword = new("sword", "Sword",
        "Coming soon.", PaintSword) { Available = false };
    private static readonly ItemDefinition Bow = new("bow", "Bow",
        "Coming soon.", PaintBow) { Available = false };
    private static readonly ItemDefinition Potion = new("potion", "Healing Potion",
        "Coming soon.", PaintPotion) { Available = false };
    private static readonly ItemDefinition Bread = new("bread", "Bread",
        "Coming soon.", PaintBread) { Available = false };

    /// <summary>Wheel categories in scroll order; every category starts with the empty hand.</summary>
    public static readonly IReadOnlyList<ItemCategory> Categories = new[]
    {
        new ItemCategory("tools", "Tools", new[] { Hand, Torch, Pickaxe }),
        new ItemCategory("weapons", "Weapons", new[] { Hand, Sword, Bow }),
        new ItemCategory("consumables", "Consumables", new[] { Hand, Potion, Bread }),
    };

    private static void PaintHand(PixelPen p)
    {
        const string skin = "#f8d49c", shade = "#d19a62";
        p.Rect(3, 4, 2, 5, skin); p.Rect(6, 2, 2, 7, skin);
        p.Rect(9, 3, 2, 6, skin); p.Rect(12, 5, 2, 4, skin);
        p.Rect(3, 8, 11, 5, skin); p.Rect(1, 9, 3, 3, skin);
        p.Rect(3, 12, 11, 1, shade); p.Rect(5, 4, 1, 5, shade);
        p.Rect(8, 3, 1, 6, shade); p.Rect(11, 5, 1, 4, shade);
    }

    private static void PaintTorch(PixelPen p)
    {
        p.Rect(7, 7, 2, 8, "#8a5a36"); p.Rect(8, 7, 1, 8, "#5f3b24");
        p.Rect(6, 5, 4, 3, "#6b4a2f");
        p.Rect(6, 3, 4, 3, "#f08a2a"); p.Rect(7, 1, 2, 4, "#ffc84e");
        p.Rect(7, 3, 2, 2, "#fff1a8");
    }

    private static void PaintPickaxe(PixelPen p)
    {
        p.Rect(7, 4, 2, 11, "#8a5a36");
        p.Rect(4, 3, 8, 2, "#aeb7c2"); p.Rect(2, 5, 3, 2, "#aeb7c2");
        p.Rect(11, 5, 3, 2, "#aeb7c2"); p.Rect(4, 4, 8, 1, "#d7dde4");
    }

    private static void PaintSword(PixelPen p)
    {
        for (int i = 0; i < 8; i++) p.Rect(6 + i, 8 - i, 2, 2, i % 2 == 0 ? "#d7dde4" : "#aeb7c2");
        for (int j = -2; j <= 2; j++) p.Dot(6 + j, 9 + j, "#e0b84a");
        p.Rect(4, 11, 2, 2, "#8a5a36"); p.Rect(2, 13, 2, 1, "#8a5a36");
        p.Dot(1, 14, "#e0b84a");
    }

    private static void PaintBow(PixelPen p)
    {
        for (int y = 1; y < 15; y++)
        {
            float t = (y - 7.5f) / 6.5f;
            p.Dot(5 + Mathf.RoundToInt(t * t * 5f), y, "#8a5a36");
        }
        p.Rect(10, 1, 1, 14, "#e8e2cc"); p.Rect(4, 7, 9, 1, "#d8c9a0"); p.Dot(13, 7, "#aeb7c2");
    }

    private static void PaintPotion(PixelPen p)
    {
        p.Rect(6, 1, 4, 2, "#8a5a36"); p.Rect(7, 3, 2, 3, "#bfe3ee");
        p.Rect(5, 6, 6, 1, "#bfe3ee"); p.Rect(4, 7, 8, 7, "#c8384a");
        p.Rect(5, 8, 2, 3, "#ee7a86"); p.Rect(4, 13, 8, 1, "#8f2433");
    }

    private static void PaintBread(PixelPen p)
    {
        p.Rect(4, 6, 8, 1, "#d9a35a"); p.Rect(3, 7, 10, 5, "#c98b43");
        p.Rect(4, 12, 8, 1, "#9b6430"); p.Rect(5, 8, 1, 2, "#e8c27d");
        p.Rect(8, 8, 1, 2, "#e8c27d"); p.Rect(11, 8, 1, 2, "#e8c27d");
    }
}
