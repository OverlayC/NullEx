using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NullEx
{
    public class IconCard : Panel
    {
        public string Title { get; set; } = "Title";
        public string Subtitle { get; set; } = "";
        public string IconGlyph { get; set; } = "\uE7C3";
        public Color IconColor { get; set; } = Theme.Accent;
        public string Badge { get; set; } = "";
        public bool Selected { get; set; } = false;
        public bool Expanded { get; set; } = false;
        public bool ShowChevron { get; set; } = false;

        public object Tag2 { get; set; }

        private bool _hover;

        public IconCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.StandardClick |
                     ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 72;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var parentBg = Parent?.BackColor ?? Theme.WindowBg;
            using (var bg = new SolidBrush(parentBg))
                e.Graphics.FillRectangle(bg, ClientRectangle);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, 10))
            {
                Color fill = Selected ? Theme.AccentDim
                            : _hover ? Theme.CardHover
                                      : Theme.CardBg;
                using (var brush = new SolidBrush(fill))
                    e.Graphics.FillPath(brush, path);

                if (Selected || Expanded)
                {
                    using (var pen = new Pen(Theme.Accent, 1.5f))
                        e.Graphics.DrawPath(pen, path);
                }
            }

            var iconRect = new Rectangle(20, (Height - 36) / 2, 36, 36);
            using (var iconPath = Theme.RoundedRect(iconRect, 8))
            using (var iconBrush = new SolidBrush(IconColor))
            {
                e.Graphics.FillPath(iconBrush, iconPath);
            }

            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var iconFont = new Font("Segoe MDL2 Assets", 14F))
            using (var iconFg = new SolidBrush(Color.White))
            {
                e.Graphics.DrawString(IconGlyph, iconFont, iconFg, iconRect, sf);
            }

            using (var titleFont = new Font("Segoe UI Semibold", 10F))
            using (var titleBrush = new SolidBrush(Theme.TextPrimary))
            {
                var titleRect = new Rectangle(72, Subtitle.Length == 0 ? (Height - 20) / 2 : 14, Width - 220, 22);
                e.Graphics.DrawString(Title, titleFont, titleBrush, titleRect);
            }

            if (!string.IsNullOrEmpty(Subtitle))
            {
                using (var subFont = new Font("Segoe UI", 8.5F))
                using (var subBrush = new SolidBrush(Theme.TextSecondary))
                {
                    var subRect = new Rectangle(72, 36, Width - 220, 20);
                    e.Graphics.DrawString(Subtitle, subFont, subBrush, subRect);
                }
            }

            int badgeRightEdge = Width - 16;
            if (ShowChevron) badgeRightEdge -= 28;

            if (!string.IsNullOrEmpty(Badge))
            {
                using (var badgeFont = new Font("Segoe UI", 8F))
                {
                    var size = e.Graphics.MeasureString(Badge, badgeFont);
                    int padX = 10, padY = 4;
                    var badgeRect = new Rectangle(
                        badgeRightEdge - (int)size.Width - padX * 2,
                        (Height - (int)size.Height - padY * 2) / 2,
                        (int)size.Width + padX * 2,
                        (int)size.Height + padY * 2);

                    using (var badgePath = Theme.RoundedRect(badgeRect, badgeRect.Height / 2))
                    using (var badgeBrush = new SolidBrush(Theme.Badge))
                    {
                        e.Graphics.FillPath(badgeBrush, badgePath);
                    }
                    using (var badgeBrush = new SolidBrush(Theme.BadgeText))
                    {
                        e.Graphics.DrawString(Badge, badgeFont, badgeBrush,
                            badgeRect.X + padX, badgeRect.Y + padY);
                    }
                }
            }

            if (ShowChevron)
            {
                string glyph = Expanded ? "\uE70E" : "\uE70D";
                using (var chevFont = new Font("Segoe MDL2 Assets", 10F))
                using (var chevBrush = new SolidBrush(Theme.TextSecondary))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    var chevRect = new Rectangle(Width - 36, 0, 24, Height);
                    e.Graphics.DrawString(glyph, chevFont, chevBrush, chevRect, sf);
                }
            }
        }
    }
}