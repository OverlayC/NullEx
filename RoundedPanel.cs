using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NullEx
{
    public class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 10;
        public Color BorderColor { get; set; } = Theme.Border;
        public int BorderThickness { get; set; } = 1;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            BackColor = Theme.CardBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var parentBg = Parent?.BackColor ?? Theme.WindowBg;
            using (var parentBrush = new SolidBrush(parentBg))
                e.Graphics.FillRectangle(parentBrush, ClientRectangle);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, CornerRadius))
            {
                using (var brush = new SolidBrush(BackColor))
                    e.Graphics.FillPath(brush, path);

                if (BorderThickness > 0)
                {
                    using (var pen = new Pen(BorderColor, BorderThickness))
                        e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }
}