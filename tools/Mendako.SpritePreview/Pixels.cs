using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mendako.SpritePreview;

internal static class Pixels
{
    /// <summary>
    /// 整数倍に拡大する。WPF の拡大に任せると NearestNeighbor を指定しても
    /// 描画経路によっては縁が補間されることがあり、ドットの確認に使えない。
    /// </summary>
    public static BitmapSource Upscale(BitmapSource source, int factor)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0d);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var input = new byte[width * height * 4];
        converted.CopyPixels(input, width * 4, 0);

        var outWidth = width * factor;
        var outHeight = height * factor;
        var output = new byte[outWidth * outHeight * 4];

        for (var y = 0; y < outHeight; y++)
        {
            var sy = y / factor;
            for (var x = 0; x < outWidth; x++)
            {
                var si = ((sy * width) + (x / factor)) * 4;
                var di = ((y * outWidth) + x) * 4;
                output[di] = input[si];
                output[di + 1] = input[si + 1];
                output[di + 2] = input[si + 2];
                output[di + 3] = input[si + 3];
            }
        }

        var result = BitmapSource.Create(outWidth, outHeight, 96d, 96d, PixelFormats.Pbgra32, null, output, outWidth * 4);
        result.Freeze();
        return result;
    }
}
