using Godot;

namespace Aetheris;

/// <summary>Draws one block of tiles from the 24px tile sheet; only redrawn when a tile near it changes.</summary>
public partial class TileChunk : Node2D
{
    public const int Size = 16;
    private const int Px = World.TileSize;
    private const int Variants = 4;

    // Sheet rows, matching tools/make_tiles.py.
    private const int Dirt = 0, Grass = 1, Stone = 2, MossStone = 3, OreStone = 4, Deep = 5, CrystalDeep = 6, WallDirt = 7, WallStone = 8, Overlay = 9;

    private static readonly Texture2D Sheet = GD.Load<Texture2D>("res://assets/tiles/tile_sheet.png");

    private World _world = null!;
    private Vector2I _chunk;

    public void Setup(World world, Vector2I chunk)
    {
        _world = world;
        _chunk = chunk;
    }

    public static int Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7fffffff;
        }
    }

    private void Blit(int row, int variant, Rect2 cell) =>
        DrawTextureRectRegion(Sheet, cell, new Rect2(variant * Px, row * Px, Px, Px));

    private bool Air(int x, int y) => x < 0 || x >= World.Width || y < 0 || y >= World.Height ? false : _world.GetTile(x, y) == 0;

    public override void _Draw()
    {
        int x0 = _chunk.X * Size, y0 = _chunk.Y * Size;
        for (int x = x0; x < System.Math.Min(x0 + Size, World.Width); x++)
        for (int y = y0; y < System.Math.Min(y0 + Size, World.Height); y++)
        {
            byte id = _world.GetTile(x, y);
            var cell = new Rect2(x * Px, y * Px, Px, Px);
            int h = Hash(x, y);
            int variant = h & (Variants - 1);
            if (id == 0)
            {
                // Enclosed air gets a cave wall instead of the forest backdrop.
                if (_world.IsEnclosed(x, y)) Blit(_world.DepthBelowSurface(x, y) < 14 ? WallDirt : WallStone, variant, cell);
                continue;
            }

            bool airAbove = Air(x, y - 1), airBelow = Air(x, y + 1), airLeft = Air(x - 1, y), airRight = Air(x + 1, y);
            bool grassTop = id == 1 && (y == 0 || airAbove);
            int row = id switch
            {
                1 => grassTop ? Grass : Dirt,
                2 => Dirt,
                3 => airAbove && h % 5 == 0 ? MossStone : h % 19 == 0 ? OreStone : Stone,
                _ => h % 13 == 0 ? CrystalDeep : Deep,
            };
            Blit(row, variant, cell);

            // Grass wraps round the corners of a cliff top.
            if (grassTop)
            {
                if (airLeft) Blit(Overlay, 0, cell);
                if (airRight) Blit(Overlay, 1, cell);
            }

            // Thin lit and shaded edges where the block meets open air give the terrain some depth.
            if (airAbove && !grassTop) DrawRect(new Rect2(cell.Position, new Vector2(Px, 1)), new Color(1f, 1f, 1f, 0.22f));
            if (airBelow) DrawRect(new Rect2(cell.Position + new Vector2(0, Px - 2), new Vector2(Px, 2)), new Color(0f, 0f, 0f, 0.38f));
            if (airLeft) DrawRect(new Rect2(cell.Position, new Vector2(1, Px)), new Color(0f, 0f, 0f, 0.22f));
            if (airRight) DrawRect(new Rect2(cell.Position + new Vector2(Px - 1, 0), new Vector2(1, Px)), new Color(0f, 0f, 0f, 0.3f));
        }
    }
}
