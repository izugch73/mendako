namespace Mendako.Platform;

/// <summary>物理ピクセルの矩形。モニタごとに DPI が違うので、位置決めは DIP に直さずこれで行う。</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;

    internal static PixelRect From(NativeMethods.RECT rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);
}
