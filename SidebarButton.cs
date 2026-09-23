using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NullEx
{
    public class SidebarButton : Control
    {
        public string Glyph { get; set; } = "●";
        public bool IsActive { get; set; } = false;
        public bool IsHover { get; private set; } = false;

        public SidebarButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            BackColor = Theme.SidebarBg;
            Cursor = Cursors.Hand;
            Size = new Size(72, 64);
        }

        protected override void OnMouseEnter(EventArgs e) { IsHover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { IsHover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            using (var bg = new SolidBrush(Parent?.BackColor ?? Theme.SidebarBg))
                e.Graphics.FillRectangle(bg, ClientRectangle);

            if (IsActive)
            {
                using (var brush = new SolidBrush(Theme.AccentDim))
                    e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            else if (IsHover)
            {
                using (var brush = new SolidBrush(Theme.CardHover))
                    e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            Color fg = IsActive ? Theme.TextPrimary : Theme.TextSecondary;
            using (var brush = new SolidBrush(fg))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                var font = new Font("Segoe MDL2 Assets", 16F);
                e.Graphics.DrawString(Glyph, font, brush, ClientRectangle, sf);
            }
        }
    }
}