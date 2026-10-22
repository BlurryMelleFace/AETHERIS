using System;

namespace Aetheris;

/// <summary>What the player currently holds; everything else reacts to Changed.</summary>
public sealed class Equipment
{
    public ItemDefinition Current { get; private set; } = ItemCatalog.Hand;

    public event Action<ItemDefinition>? Changed;

    public bool Equip(ItemDefinition item)
    {
        if (!item.Available) return false;
        if (item == Current) return true;
        Current = item;
        Changed?.Invoke(item);
        return true;
    }
}
