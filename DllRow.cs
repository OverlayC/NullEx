using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NullEx
{
    public class DllRow : Panel
    {
        public string FileName { get; set; } = "mod.dll";
        public string FullPath { get; set; } = "";
        public int EntryPointCount { get; set; } = 0;
        public bool IsInjected { get; set; } = false;
        public bool IsChanged { get; set; } = false;
        public string SelectedEntryLabel { get; set; } = "";
        public string Folder { get; set; } = "";

        public bool Checked
        {
            get => _checked;
            set { if (_checked == value) return; _checked = value; Invalidate(); }
        }

        public event EventHandler CheckedChanged;

        private bool _hover;
        private bool _checked;
        private bool _checkHit;
        private static Rectangle CheckboxRect => new Rectangle(10, 12, 16, 16);

        public DllRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.StandardClick |
                     ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 40;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _checkHit = e.Button == MouseButtons.Left && CheckboxRect.Contains(e.Location);
            if (_checkHit)
            {
                _checked = !_checked;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
            base.OnMouseDown(e);
        }

        protected override void OnClick(EventArgs e)
        {
            if (_checkHit)
            {
                _checkHit = false;
                return;
            }
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var parentBg = Parent?.BackColor ?? Theme.CardBg;
            using (var bg = new SolidBrush(parentBg))
                e.Graphics.FillRectangle(bg, ClientRectangle);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, 6))
            {
                Color fill = _hover ? Theme.CardHover : Theme.InputBg;
                using (var brush = new SolidBrush(fill))
                    e.Graphics.FillPath(brush, path);
            }

            var cb = CheckboxRect;
            using (var cbPath = Theme.RoundedRect(cb, 3))
            {
                Color cbFill = _checked ? Theme.Accent : Theme.CardBg;
                using (var b = new SolidBrush(cbFill))
                    e.Graphics.FillPath(b, cbPath);
                using (var p = new Pen(_checked ? Theme.Accent : Theme.BorderLight, 1.2f))
                    e.Graphics.DrawPath(p, cbPath);
            }
            if (_checked)
            {
                using (var pen = new Pen(Color.White, 1.6f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    e.Graphics.DrawLines(pen, new[]
                    {
                        new Point(cb.X + 3, cb.Y + 8),
                        new Point(cb.X + 6, cb.Y + 12),
                        new Point(cb.X + 13, cb.Y + 4)
                    });
                }
            }

            using (var iconFont = new Font("Segoe MDL2 Assets", 10F))
            using (var iconBrush = new SolidBrush(Theme.Accent))
            {
                e.Graphics.DrawString("\uE7C3", iconFont, iconBrush, new PointF(32, 13));
            }

            using (var nameFont = new Font("Segoe UI", 9F))
            using (var nameBrush = new SolidBrush(Theme.TextPrimary))
            {
                e.Graphics.DrawString(FileName, nameFont, nameBrush, new PointF(54, 11));
            }

            string rightText;
            Color rightColor;
            if (IsInjected)
            {
                rightText = "✓ Injected";
                rightColor = Theme.Success;
            }
            else if (IsChanged)
            {
                rightText = "changed";
                rightColor = Theme.Warning;
            }
            else if (!string.IsNullOrEmpty(SelectedEntryLabel))
            {
                rightText = SelectedEntryLabel;
                rightColor = Theme.TextMuted;
            }
            else
            {
                rightText = $"{EntryPointCount} methods";
                rightColor = Theme.TextMuted;
            }

            using (var rightFont = new Font("Segoe UI", 8.5F))
            using (var rightBrush = new SolidBrush(rightColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                var rightRect = new Rectangle(0, 0, Width - 14, Height);
                e.Graphics.DrawString(rightText, rightFont, rightBrush, rightRect, sf);
            }
        }
    }
}