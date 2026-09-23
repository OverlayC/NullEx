using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NullEx
{
    public class RoundedTextBox : UserControl
    {
        public int CornerRadius { get; set; } = 8;
        public TextBox Inner { get; private set; }
        private bool _focused;

        public string PlaceholderText
        {
            get => Inner.PlaceholderText;
            set => Inner.PlaceholderText = value;
        }

        public override string Text
        {
            get => Inner.Text;
            set => Inner.Text = value;
        }

        public RoundedTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            BackColor = Theme.InputBg;
            Padding = new Padding(10, 6, 10, 6);
            Height = 32;

            Inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBg,
                ForeColor = Theme.TextPrimary,
                Font = Theme.Body,
                Dock = DockStyle.Fill
            };

            Inner.GotFocus += (s, e) => { _focused = true; Invalidate(); };
            Inner.LostFocus += (s, e) => { _focused = false; Invalidate(); };

            Controls.Add(Inner);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var parentBg = Parent?.BackColor ?? Theme.CardBg;
            using (var parentBrush = new SolidBrush(parentBg))
                e.Graphics.FillRectangle(parentBrush, ClientRectangle);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, CornerRadius))
            {
                using (var brush = new SolidBrush(BackColor))
                    e.Graphics.FillPath(brush, path);

                var borderColor = _focused ? Theme.Accent : Theme.Border;
                using (var pen = new Pen(borderColor, _focused ? 1.5f : 1f))
                    e.Graphics.DrawPath(pen, path);
            }
        }
    }
}