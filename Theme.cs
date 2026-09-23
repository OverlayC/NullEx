using System.Drawing;
using System.Drawing.Drawing2D;

namespace NullEx
{
    internal static class Theme
    {
        public static readonly Color WindowBg = Color.FromArgb(30, 30, 30);
        public static readonly Color SidebarBg = Color.FromArgb(37, 37, 38);
        public static readonly Color SurfaceBg = Color.FromArgb(37, 37, 38);
        public static readonly Color CardBg = Color.FromArgb(45, 45, 48);
        public static readonly Color CardHover = Color.FromArgb(55, 55, 58);
        public static readonly Color InputBg = Color.FromArgb(51, 51, 55);
        public static readonly Color Border = Color.FromArgb(63, 63, 80);
        public static readonly Color BorderLight = Color.FromArgb(80, 80, 88);
        public static readonly Color LogBg = Color.FromArgb(20, 20, 20);

        public static readonly Color TextPrimary = Color.FromArgb(230, 230, 230);
        public static readonly Color TextSecondary = Color.FromArgb(160, 160, 165);
        public static readonly Color TextMuted = Color.FromArgb(110, 110, 115);

        public static readonly Color Accent = Color.FromArgb(107, 127, 219);
        public static readonly Color AccentHover = Color.FromArgb(125, 145, 235);
        public static readonly Color AccentPressed = Color.FromArgb(85, 105, 200);
        public static readonly Color AccentDim = Color.FromArgb(55, 65, 110);

        public static readonly Color Success = Color.FromArgb(106, 176, 120);
        public static readonly Color Warning = Color.FromArgb(220, 190, 120);
        public static readonly Color Error = Color.FromArgb(220, 100, 100);

        public static readonly Color Badge = Color.FromArgb(60, 70, 100);
        public static readonly Color BadgeText = Color.FromArgb(180, 195, 240);

        public static readonly Font Body = new Font("Segoe UI", 9F);
        public static readonly Font BodyBold = new Font("Segoe UI Semibold", 9F);
        public static readonly Font Small = new Font("Segoe UI", 8.25F);
        public static readonly Font Title = new Font("Segoe UI Semibold", 16F);
        public static readonly Font Subtitle = new Font("Segoe UI", 9F);
        public static readonly Font Mono = new Font("Consolas", 9F);

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;

            if (radius <= 0 || d > bounds.Width || d > bounds.Height)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}