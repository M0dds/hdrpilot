using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace HdrAutoSwitch.UI;

/// <summary>
/// Erzeugt das Tray-Icon zur Laufzeit, damit keine .ico-Datei mitgeliefert werden muss.
/// Ein schlichtes rundes Badge mit den Buchstaben "HDR".
/// </summary>
internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create()
    {
        using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            using var bg = new LinearGradientBrush(
                new Rectangle(0, 0, 32, 32),
                Color.FromArgb(0, 120, 215),   // Windows-Blau
                Color.FromArgb(0, 90, 158),
                LinearGradientMode.ForwardDiagonal);
            g.FillEllipse(bg, 1, 1, 30, 30);

            using var pen = new Pen(Color.FromArgb(180, 255, 255, 255), 1.5f);
            g.DrawEllipse(pen, 1, 1, 30, 30);

            using var font = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(Color.White);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("HDR", font, text, new RectangleF(0, 0, 32, 32), sf);
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
