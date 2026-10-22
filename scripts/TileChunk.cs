using Godot;

namespace Aetheris;

/// <summary>Draws one block of tiles; only redrawn when a tile inside it changes.</summary>
public partial class TileChunk : Node2D
{
    public const int Size = 16;

    private static readonly Texture2D GrassArt = GD.Load<Texture2D>("res://assets/tiles/grass_top_32.svg");
    private static readonly Texture2D DirtArt = GD.Load<Texture2D>("res://assets/tiles/dirt_32.svg");
    private static readonly Texture2D StoneArt = GD.Load<Texture2D>("res://assets/tiles/stone_32.svg");
    private static readonly Texture2D FoliageArt = GD.Load<Texture2D>("res://assets/foliage/grass_clump_32.svg");

    private World _world = null!;
    private Vector2I _chunk;

    public void Setup(World world, Vector2I chunk)
    {
        _world = world;
        _chunk = chunk;
    }

    public override void _Draw()
    {
        int x0 = _chunk.X * Size, y0 = _chunk.Y * Size;
        for (int x = x0; x < System.Math.Min(x0 + Size, World.Width); x++)
        for (int y = y0; y < System.Math.Min(y0 + Size, World.Height); y++)
        {
            byte id = _world.GetTile(x, y);
            var cell = new Rect2(x * World.TileSize, y * World.TileSize, World.TileSize, World.TileSize);
            if (id == 0)
            {
                // Enclosed air gets a cave wall instead of the forest backdrop.
                if (_world.IsEnclosed(x, y)) DrawRect(cell, new Color(.17f, .13f, .11f));
                continue;
            }
            bool exposed = y == 0 || _world.GetTile(x, y - 1) == 0;
            Texture2D texture = id == 3 ? StoneArt : id == 1 && exposed ? GrassArt : DirtArt;
            DrawTextureRect(texture, cell, false);
            // Variation above ground uses small hand-authored pixel-grid foliage.
            if (id == 1 && exposed && (x * 17 + y * 11) % 6 == 0)
                DrawTextureRect(FoliageArt,
                    new Rect2(cell.Position.X - 4, cell.Position.Y - 14, 32, 22), false);
        }
    }
}
