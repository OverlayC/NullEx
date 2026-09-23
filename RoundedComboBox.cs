using System.Drawing;
using System.Windows.Forms;

namespace NullEx
{
    public class RoundedComboBox : ComboBox
    {
        public RoundedComboBox()
        {
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.InputBg;
            ForeColor = Theme.TextPrimary;
            Font = Theme.Body;
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 22;

            DrawItem += RoundedComboBox_DrawItem;
        }

        private void RoundedComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var bg = new SolidBrush(selected ? Theme.Accent : Theme.InputBg))
                e.Graphics.FillRectangle(bg, e.Bounds);

            string text = Items[e.Index].ToString();
            Color fg = selected ? Color.White : Theme.TextPrimary;

            using (var brush = new SolidBrush(fg))
                e.Graphics.DrawString(text, Theme.Body, brush,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 2, e.Bounds.Width - 6, e.Bounds.Height));
        }
    }
}