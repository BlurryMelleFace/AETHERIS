using Godot;

namespace Aetheris;

/// <summary>A background tree. The trunk footprint is tile-aligned so chopping can target it later.</summary>
public sealed class TreeInstance
{
    public const int TrunkTiles = 1;

    public TreeInstance(int id, int trunkColumn, int baseRow, TreeVariant variant)
    {
        Id = id;
        TrunkColumn = trunkColumn;
        BaseRow = baseRow;
        Variant = variant;
    }

    public int Id { get; }

    /// <summary>The column of the 1-block-wide trunk.</summary>
    public int TrunkColumn { get; }

    /// <summary>First ground row under the trunk.</summary>
    public int BaseRow { get; }

    public TreeVariant Variant { get; }
    public int HeightTiles => Variant.HeightTiles;
    public string Species => Variant.Species;
    public bool Felled { get; set; }

    /// <summary>Rows of bare trunk above the ground before the leaves start.</summary>
    public int BareTrunkRows => Mathf.Max(3, Variant.BareRows);

    /// <summary>Ground contact point at the centre of the trunk.</summary>
    public Vector2 Root => new((TrunkColumn + TrunkTiles / 2f) * World.TileSize, BaseRow * World.TileSize);

    public bool OccupiesTrunk(int x, int y) =>
        !Felled && x >= TrunkColumn && x < TrunkColumn + TrunkTiles
        && y >= BaseRow - BareTrunkRows && y < BaseRow;
}
