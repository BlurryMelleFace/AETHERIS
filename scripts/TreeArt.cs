using Godot;
using System;

namespace Aetheris;

/// <summary>Procedural pixel-art pine drawn 1:1 on 2px cells, so it is never stretched or resampled.</summary>
public static class TreeArt
{
    public const int Cell = 2;
    public const int Embed = 8; // pixels buried under the ground so no gap shows on slopes
    public const int WidthPixels = 208;

    private static readonly Color[] Needles =
    {
        new("#16302f"), new("#1b3936"), new("#254940"), new("#325b47"), new("#446b51"), new("#61815a")
    };

    private static readonly Color BarkDark = new("#2c221d");
    private static readonly Color BarkShade = new("#3a2d26");
    private static readonly Color Bark = new("#45352e");
    private static readonly Color BarkMid = new("#5d4838");
    private static readonly Color BarkLight = new("#856248");

    public static int HeightPixels(TreeInstance tree) => tree.HeightTiles * World.TileSize + Embed;

    public static ImageTexture Create(TreeInstance tree)
    {
        int cellsX = WidthPixels / Cell, cellsY = HeightPixels(tree) / Cell;
        var cells = new Color?[cellsX, cellsY];
        int cx = cellsX / 2;

        DrawTrunk(cells, tree, cx, cellsX, cellsY);
        DrawCanopy(cells, tree, cx, cellsY);

        var bytes = new byte[WidthPixels * cellsY * Cell * 4];
        for (int y = 0; y < cellsY; y++)
        for (int x = 0; x < cellsX; x++)
        {
            if (cells[x, y] is not Color color) continue;
            for (int dy = 0; dy < Cell; dy++)
            for (int dx = 0; dx < Cell; dx++)
            {
                int index = ((y * Cell + dy) * WidthPixels + x * Cell + dx) * 4;
                bytes[index] = (byte)(color.R * 255f);
                bytes[index + 1] = (byte)(color.G * 255f);
                bytes[index + 2] = (byte)(color.B * 255f);
                bytes[index + 3] = 255;
            }
        }
        return ImageTexture.CreateFromImage(Image.CreateFromData(WidthPixels, cellsY * Cell, false, Image.Format.Rgba8, bytes));
    }

    private static void DrawTrunk(Color?[,] cells, TreeInstance tree, int cx, int cellsX, int cellsY)
    {
        // Two blocks wide: 48px = 24 cells.
        int half = TreeInstance.TrunkTiles * World.TileSize / Cell / 2;
        int top = cellsY - (HeightPixels(tree) / Cell) * 55 / 100;
        for (int y = top; y < cellsY; y++)
        {
            int flare = Math.Max(0, y - (cellsY - 9)) / 3;
            for (int x = cx - half - flare; x < cx + half + flare; x++)
            {
                if (x < 0 || x >= cellsX) continue;
                float u = (x - (cx - half)) / (half * 2f);
                Color color = u < 0.14f ? BarkMid : u < 0.6f ? Bark : u < 0.82f ? BarkShade : BarkDark;
                if (Noise(x / 2, tree.Id) > 0.72f) color = u < 0.5f ? BarkLight : BarkDark;
                else if (Noise(x, y / 3 + tree.Id * 5) > 0.93f) color = BarkDark;
                cells[x, y] = color;
            }
        }
    }

    private static void DrawCanopy(Color?[,] cells, TreeInstance tree, int cx, int cellsY)
    {
        int tiers = tree.HeightTiles >= 14 ? 7 : 6;
        int bottom = cellsY - (HeightPixels(tree) / Cell) * 28 / 100;
        int span = bottom - 2;
        int tierHeight = (int)(span / (tiers - 1f) * 1.3f);
        for (int tier = 0; tier < tiers; tier++)
        {
            float t = tier / (tiers - 1f);
            int top = 2 + (int)((span - tierHeight) * t);
            int maxHalf = (int)Mathf.Lerp(9f, 46f, t * 0.75f + t * t * 0.25f);
            for (int row = 0; row < tierHeight; row++)
            {
                int y = top + row;
                if (y >= cellsY) break;
                float progress = row / (tierHeight - 1f);
                // Each tier starts about half as wide as it ends, so the silhouette steps outwards.
                float profile = tier == 0 ? 0.1f + 0.9f * progress : 0.5f + 0.5f * progress;
                int half = (int)(maxHalf * profile);
                half += (int)(Noise(row, tier + tree.Id) * 4f) - 1;
                if (row % 3 == 0 && Noise(row + 40, tier * 3 + tree.Id) > 0.55f) half += 2; // branch tips
                if (row >= tierHeight - 3) half -= (row - (tierHeight - 3)) * 3; // drooping lower edge
                half = Math.Max(1, half);
                for (int x = cx - half; x <= cx + half; x++)
                {
                    if (x < 0 || x >= cells.GetLength(0)) continue;
                    float nx = (x - cx) / (maxHalf + 1f);
                    float light = -0.55f * nx + (0.4f - progress) * 0.75f + (Noise(x, y) - 0.5f) * 0.95f;
                    if (row < 3 && tier > 0) light -= 0.65f;               // shadow cast by the tier above
                    if (progress > 0.72f) light -= (progress - 0.72f) * 2.6f; // underside of the boughs
                    if (x - (cx - half) < 2 && nx < 0f) light -= 0.2f;
                    if ((cx + half) - x < 3) light -= 0.5f;
                    int index = Mathf.Clamp(Mathf.RoundToInt(2.4f + light * 2.2f), 0, Needles.Length - 1);
                    cells[x, y] = Needles[index];
                }
            }
        }
    }

    private static float Noise(int a, int b) =>
        Mathf.PosMod(Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f, 1f);
}
