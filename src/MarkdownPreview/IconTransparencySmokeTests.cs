using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MarkdownPreview;

public partial class MainWindow
{
    /// <summary>Extracts the actual small and large Windows shell icons from the built executable.</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint ExtractIconEx(string file, int index, [Out] IntPtr[] large, [Out] IntPtr[] small, uint count);

    /// <summary>Releases an extracted native icon after its alpha channel has been checked.</summary>
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    /// <summary>Checks transparency in the shipped PE resources and the WPF window icon, not just source artwork.</summary>
    private void VerifyBuiltIconTransparency()
    {
        var large = new IntPtr[1]; var small = new IntPtr[1];
        try
        {
            if (ExtractIconEx(Environment.ProcessPath!, 0, large, small, 1) == 0)
                throw new InvalidOperationException("The executable has no extractable shell icon.");
            foreach (var handle in new[] { large[0], small[0] })
            {
                if (handle == IntPtr.Zero) throw new InvalidOperationException("A shell icon size is missing.");
                AssertTransparentIcon(Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()));
            }
            if (Icon is not BitmapSource windowIcon) throw new InvalidOperationException("The window icon is missing.");
            AssertTransparentIcon(windowIcon);
        }
        finally
        {
            if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
            if (small[0] != IntPtr.Zero) DestroyIcon(small[0]);
        }
    }

    /// <summary>Requires empty corners and visible nontransparent notebook pixels after Windows decodes an icon.</summary>
    private static void AssertTransparentIcon(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        foreach (var offset in new[] { 3, stride - 1, (converted.PixelHeight - 1) * stride + 3, pixels.Length - 1 })
            if (pixels[offset] != 0) throw new InvalidOperationException("The built icon has an opaque background corner.");
        var visible = false;
        for (var i = 3; i < pixels.Length; i += 4) visible |= pixels[i] > 0;
        if (!visible) throw new InvalidOperationException("The built icon is entirely transparent.");
    }
}
