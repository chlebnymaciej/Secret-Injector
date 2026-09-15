using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VaultInjector.App.Native;
using Icon = System.Drawing.Icon;
using Bitmap = System.Drawing.Bitmap;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;
using Brushes = System.Windows.Media.Brushes;

namespace VaultInjector.App.Services;

/// <summary>
/// Renders the app's "key" glyph (authored in Google's Material Symbols path syntax) at runtime, either as
/// a GDI+ <see cref="Icon"/> for the tray or a WPF <see cref="BitmapSource"/> for a window's title bar,
/// optionally with a small logged-in/out status dot badge in the corner.
/// </summary>
internal static class TrayIconRenderer
{
    // Material Symbols "key" glyph, viewBox="0 -960 960 960" (a 960x960 box, y running from -960 to 0).
    private const string KeyPathData =
        "M443.5-736.5Q467-760 500-760t56.5 23.5Q580-713 580-680t-23.5 56.5Q533-600 500-600t-56.5-23.5Q420-647 420-680t23.5-56.5ZM500 0 320-180l60-80-60-80 60-85v-47q-54-32-87-86.5T260-680q0-100 70-170t170-70q100 0 170 70t70 170q0 67-33 121.5T620-472v352L500 0ZM340-680q0 56 34 98.5t86 56.5v125l-41 58 61 82-55 71 75 75 40-40v-371q52-14 86-56.5t34-98.5q0-66-47-113t-113-47q-66 0-113 47t-47 113Z";

    private static readonly Color KeyFillColor = Colors.White;
    private static readonly Color KeyStrokeColor = Colors.Black;
    private const double KeyStrokeThickness = 56; // in path-space (960-unit) units, so the border scales with output size

    private static readonly Color LoggedInColor = Color.FromRgb(0x48, 0x75, 0x2C);
    private static readonly Color LoggedOutColor = Color.FromRgb(0xEA, 0x33, 0x23);

    private const double ViewBoxSize = 960.0;

    /// <param name="isLoggedIn">
    /// True/false draws a green/red status dot in the bottom-right corner; null (status not yet known,
    /// e.g. right at startup before the first check completes) draws the plain key with no badge.
    /// </param>
    public static Icon RenderKeyIcon(int pixelSize, bool? isLoggedIn) => ToGdiIcon(RenderBitmap(pixelSize, isLoggedIn));

    /// <summary>
    /// Renders the same key glyph as a frozen WPF <see cref="BitmapSource"/>, suitable for a
    /// <see cref="Window.Icon"/> (title bar / Alt+Tab / taskbar), so the window uses the same icon as the tray.
    /// </summary>
    public static BitmapSource RenderKeyIconImageSource(int pixelSize, bool? isLoggedIn = null) => RenderBitmap(pixelSize, isLoggedIn);

    private static RenderTargetBitmap RenderBitmap(int pixelSize, bool? isLoggedIn)
    {
        var geometry = Geometry.Parse(KeyPathData);
        var scale = pixelSize / ViewBoxSize;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var transform = new TransformGroup();
            transform.Children.Add(new TranslateTransform(0, ViewBoxSize)); // shift y from [-960,0] to [0,960]
            transform.Children.Add(new ScaleTransform(scale, scale));

            dc.PushTransform(transform);
            dc.DrawGeometry(new SolidColorBrush(KeyFillColor), new Pen(new SolidColorBrush(KeyStrokeColor), KeyStrokeThickness), geometry);
            dc.Pop();

            if (isLoggedIn is { } loggedIn)
            {
                var badgeColor = loggedIn ? LoggedInColor : LoggedOutColor;
                var radius = pixelSize * 0.24;
                var center = new Point(pixelSize - radius, pixelSize - radius);
                dc.DrawEllipse(new SolidColorBrush(badgeColor), new Pen(Brushes.White, Math.Max(1, pixelSize * 0.08)), center, radius, radius);
            }
        }

        var bitmap = new RenderTargetBitmap(pixelSize, pixelSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static Icon ToGdiIcon(BitmapSource bitmapSource)
    {
        using var pngStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        encoder.Save(pngStream);
        pngStream.Position = 0;

        using var bitmap = new Bitmap(pngStream);
        var hIcon = bitmap.GetHicon();
        try
        {
            using var handleIcon = Icon.FromHandle(hIcon);
            // Icon.FromHandle doesn't own hIcon - Clone() copies the actual icon data so we can
            // safely destroy the native handle below instead of leaking it.
            return (Icon)handleIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
