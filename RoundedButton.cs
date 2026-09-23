using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NullEx
{
    public class RoundedButton : Button
    {
        public int CornerRadius { get; set; } = 8;
        public Color NormalColor { get; set; } = Theme.Accent;
        public Color HoverColor { get; set; } = Theme.AccentHover;
        public Color PressedColor { get; set; } = Theme.AccentPressed;
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderThickness { get; set; } = 0;

        private bool _hover;
        private bool _pressed;

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(System.EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(System.EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

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
                Color fill = _pressed ? PressedColor : _hover ? HoverColor : NormalColor;
                using (var brush = new SolidBrush(fill))
                    e.Graphics.FillPath(brush, path);

                if (BorderThickness > 0 && BorderColor != Color.Transparent)
                {
                    using (var pen = new Pen(BorderColor, BorderThickness))
                        e.Graphics.DrawPath(pen, path);
                }
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, rect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}