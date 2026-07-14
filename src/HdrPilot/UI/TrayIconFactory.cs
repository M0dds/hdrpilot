using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace HdrPilot.UI;

/// <summary>
/// Zeichnet das Tray-Icon zur Laufzeit im HDR-Pilot-Markendesign:
/// Split-Circle - linke Hälfte gedimmt (SDR), rechte Hälfte strahlend (HDR),
/// schmaler Schlitz dazwischen. Identisch zum App-Icon (Assets\app.ico).
/// </summary>
internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create()
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float pad = size * 0.04f;
            float d = size - 2 * pad;
            float cx = size / 2f, cy = size / 2f, r = d / 2f;

            using var circle = new GraphicsPath();
            circle.AddEllipse(pad, pad, d, d);
            g.SetClip(circle);

            g.TranslateTransform(cx, cy);
            g.RotateTransform(12f);
            float ext = r + 2;

            // Linke Hälfte gedimmt - im Tray etwas heller als im App-Icon,
            // damit sie auf der dunklen Taskleiste nicht absäuft.
            using (var dim = new SolidBrush(Color.FromArgb(0x6E, 0x77, 0x82)))
                g.FillRectangle(dim, -ext, -ext, ext, 2 * ext);

            using (var bright = new LinearGradientBrush(
                new RectangleF(0, -ext, ext, 2 * ext),
                Color.FromArgb(0x9B, 0xDD, 0xFF), Color.FromArgb(0x2C, 0xA6, 0xF2),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bright, 0, -ext, ext, 2 * ext);
            }

            float slit = Math.Max(1.5f, size * 0.055f);
            g.CompositingMode = CompositingMode.SourceCopy;
            using (var clear = new SolidBrush(Color.Transparent))
                g.FillRectangle(clear, -slit / 2f, -ext, slit, 2 * ext);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            // Kopie erzeugen, damit wir das GDI-Handle sofort freigeben können.
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
