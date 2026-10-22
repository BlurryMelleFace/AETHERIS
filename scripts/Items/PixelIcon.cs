using Godot;
using System;

namespace Aetheris;

/// <summary>Paints one 16x16 logical-pixel icon (scaled 2x to 32px) with an automatic dark outline.</summary>
public sealed class PixelPen
{
    public const int Size = 16;
    private readonly Image _image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);

    public void Rect(int x, int y, int w, int h, string hex)
    {
        var color = new Color(hex);
        for (int py = y; py < y + h; py++)
        for (int px = x; px < x + w; px++)
            if (px >= 0 && px < Size && py >= 0 && py < Size) _image.SetPixel(px, py, color);
    }

    public void Dot(int x, int y, string hex) => Rect(x, y, 1, 1, hex);

    public Image Finish(Color outline)
    {
        var result = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            Color pixel = _image.GetPixel(x, y);
            if (pixel.A > 0f) { result.SetPixel(x, y, pixel); continue; }
            bool touching = (x > 0 && _image.GetPixel(x - 1, y).A > 0f)
                || (x < Size - 1 && _image.GetPixel(x + 1, y).A > 0f)
                || (y > 0 && _image.GetPixel(x, y - 1).A > 0f)
                || (y < Size - 1 && _image.GetPixel(x, y + 1).A > 0f);
            if (touching) result.SetPixel(x, y, outline);
        }
        return result;
    }
}

public static class PixelIcon
{
    public static ImageTexture Create(Action<PixelPen> paint)
    {
        var pen = new PixelPen();
        paint(pen);
        return ImageTexture.CreateFromImage(pen.Finish(new Color("#1c1713")));
    }
}
