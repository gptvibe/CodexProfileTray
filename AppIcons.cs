using System.Drawing.Drawing2D;

namespace CodexProfileTray;

internal static class AppIcons
{
    public static Icon GetAppIcon()
    {
        var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (extracted is not null)
        {
            return extracted;
        }

        return CreateFallbackIcon();
    }

    private static Icon CreateFallbackIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var background = new LinearGradientBrush(
            new Rectangle(0, 0, 64, 64),
            Color.FromArgb(38, 99, 235),
            Color.FromArgb(20, 184, 166),
            45F);
        graphics.FillRoundedRectangle(background, new Rectangle(6, 6, 52, 52), 14);

        using var pen = new Pen(Color.White, 5)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        graphics.DrawArc(pen, 19, 18, 25, 25, 35, 285);
        graphics.DrawLine(pen, 39, 17, 48, 17);
        graphics.DrawLine(pen, 48, 17, 48, 26);
        graphics.DrawLine(pen, 25, 47, 16, 47);
        graphics.DrawLine(pen, 16, 47, 16, 38);

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
