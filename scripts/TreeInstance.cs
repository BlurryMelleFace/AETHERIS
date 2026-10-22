using Godot;

namespace Aetheris;

/// <summary>A background tree. The trunk footprint is tile-aligned so it can be chopped block by block.</summary>
public sealed class TreeInstance
{
    public const int TrunkTiles = 1;

    private readonly float[] _damage;

    public TreeInstance(int id, int trunkColumn, int baseRow, TreeVariant variant)
    {
        Id = id;
        TrunkColumn = trunkColumn;
        BaseRow = baseRow;
        Variant = variant;
        Remaining = variant.TrunkRows;
        _damage = new float[variant.TrunkRows + 1];
    }

    public int Id { get; }

    /// <summary>The column of the 1-block-wide trunk.</summary>
    public int TrunkColumn { get; }

    /// <summary>First ground row under the trunk.</summary>
    public int BaseRow { get; }

    public TreeVariant Variant { get; }
    public int HeightTiles => Variant.HeightTiles;
    public string Species => Variant.Species;

    /// <summary>True once the whole tree is gone, stump included.</summary>
    public bool Felled { get; set; }

    /// <summary>Trunk blocks still standing above the ground (counted from the bottom).</summary>
    public int Remaining { get; private set; }

    /// <summary>True once the top of the tree has been cut off.</summary>
    public bool Cut { get; private set; }

    public bool IsStump => Cut && Remaining <= 1;

    /// <summary>Top edge, in texture pixels, of the part of the sprite that is still standing.</summary>
    public float VisibleTopPixels { get; set; }

    /// <summary>Ground contact point at the centre of the trunk.</summary>
    public Vector2 Root => new((TrunkColumn + TrunkTiles / 2f) * World.TileSize, BaseRow * World.TileSize);

    /// <summary>Index of a trunk block counted up from the ground: 0 is the bottom block.</summary>
    public int BlockIndex(int row) => BaseRow - 1 - row;

    public bool OccupiesTrunk(int x, int y)
    {
        int k = BlockIndex(y);
        return !Felled && x >= TrunkColumn && x < TrunkColumn + TrunkTiles && k >= 0 && k < Remaining;
    }

    public float DamageAt(int block) => block >= 0 && block < _damage.Length ? _damage[block] : 0f;

    public float AddDamage(int block, float amount) => _damage[block] += amount;

    public void CutTo(int remaining)
    {
        Cut = true;
        Remaining = remaining;
        for (int i = remaining; i < _damage.Length; i++) _damage[i] = 0f;
    }
}
