using Project.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Project
{
    public class TextBoxTool : RichTextBox
    {
        int resizingEdge = 8;

        bool dragTextBox;
        bool transformTextBox;

        public bool raiseEvents = true;

        Point initialPosition = Point.Empty;
        Point finalPosition = Point.Empty;
        Point moveNewPosition = Point.Empty;

        Size initialSize = Size.Empty;
        Size finalSize = Size.Empty;

        Stopwatch timerMove = new Stopwatch();
        Thread draggingText;

        public Color activeColor = Color.FromArgb(62, 94, 94);
        public Color hoverColor = Color.FromArgb(92, 139, 139);
        public Color idleColor = Color.FromArgb(102, 153, 153);

        public TextBoxTool(Point location, Size size, Control parent)
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.Opaque, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            this.TextChanged += TextBox_TextChanged;
            this.BackColor = Color.Transparent;
            this.ScrollBars = RichTextBoxScrollBars.None;
            parent.Controls.Add(this);
            this.Location = location;
            this.Size = size;
            this.BringToFront();
            this.MouseDown += TextBox_MouseDown;
            this.MouseMove += TextBox_MouseMove;
            this.MouseClick += TextBox_Click;
            this.MouseUp += TextBox_MouseUp;
            this.GotFocus += TextBox_GotFocus;
        }

        public TextBoxTool(Rectangle box, Control parent)
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.Opaque, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            this.TextChanged += TextBox_TextChanged;
            this.BackColor = Color.Transparent;
            this.ScrollBars = RichTextBoxScrollBars.None;
            parent.Controls.Add(this);
            this.Location = box.Location;
            this.Size = box.Size;
            this.BringToFront();
            this.MouseDown += TextBox_MouseDown;
            this.MouseMove += TextBox_MouseMove;
            this.MouseClick += TextBox_Click;
            this.MouseUp += TextBox_MouseUp;
            this.GotFocus += TextBox_GotFocus;
            this.LostFocus += TextBox_LostFocus;
        }

        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            this.ForceRefresh();
        }

        public void LoseFocus(Control target)
        {
            target.Focus();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parms = base.CreateParams;
                parms.ExStyle |= 0x20;
                return parms;
            }
        }

        public void ForceRefresh()
        {
            this.UpdateStyles();
        }

        private void TextBox_MouseDown(object sender, MouseEventArgs e)
        {
            dragTextBox = true;
            initialPosition = e.Location;
            draggingText = new Thread(DragText);
            draggingText.Start();
            initialSize = ((TextBoxTool)sender).Size;
            ((TextBoxTool)sender).BringToFront();
        }

        private void TextBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragTextBox)
            {
                finalPosition = e.Location;
                //if (((PictureBox)sender).Equals(copy))
                ChangeTransformCursors((TextBoxTool)sender);
                //else
                //    ((PictureBox)sender).Cursor = Cursors.Default;
            }
            if (dragTextBox && e.Button == MouseButtons.Left)
            {
                if (transformTextBox)
                {
                    TransformSelection(e.Location, ((TextBoxTool)sender));
                }
                else
                {
                    moveNewPosition = Point.Empty;
                    moveNewPosition.X = e.X - initialPosition.X;
                    moveNewPosition.Y = e.Y - initialPosition.Y;
                    if (timerMove.ElapsedMilliseconds > 30)
                    {
                        ((TextBoxTool)sender).Left += moveNewPosition.X;
                        ((TextBoxTool)sender).Top += moveNewPosition.Y;
                    }
                }

            }
        }

        private void TextBox_MouseUp(object sender, MouseEventArgs e)
        {
            dragTextBox = false;
            transformTextBox = false;
            initialPosition = Point.Empty;
            moveNewPosition = Point.Empty;
        }

        public void RaiseEventsStatus(bool newStatus)
        {
            raiseEvents = newStatus;
        }

        private void TextBox_Click(object sender, MouseEventArgs e)
        {
            this.ForceRefresh();
        }

        private void TransformSelection(Point location, TextBoxTool sender)
        {
            moveNewPosition = Point.Empty;
            moveNewPosition.X = location.X - initialPosition.X;
            moveNewPosition.Y = location.Y - initialPosition.Y;

            //top left
            if (finalPosition.X < resizingEdge && finalPosition.Y < resizingEdge)
            {
                sender.Left += moveNewPosition.X;
                sender.Top += moveNewPosition.Y;
                sender.Height -= moveNewPosition.Y;
                sender.Width -= moveNewPosition.X;
            }
            //bottom right
            else if (sender.Cursor == Cursors.SizeNWSE)
            {
                sender.Height = moveNewPosition.Y + initialSize.Height;
                sender.Width = moveNewPosition.X + initialSize.Width;
            }
            //top right
            else if (sender.Cursor == Cursors.SizeNESW && location.X > sender.Width / 2)
            {
                sender.Top += moveNewPosition.Y;
                sender.Height -= moveNewPosition.Y;
                sender.Width = moveNewPosition.X + initialSize.Width;
            }
            //bottom left
            else if (sender.Cursor == Cursors.SizeNESW)
            {
                sender.Left += moveNewPosition.X;
                sender.Height = moveNewPosition.Y + initialSize.Height;
                sender.Width -= moveNewPosition.X;
            }
            //left
            else if (finalPosition.X < resizingEdge)
            {
                sender.Left += moveNewPosition.X;
                sender.Width -= moveNewPosition.X;
            }
            //right
            else if (sender.Cursor == Cursors.SizeWE)
            {
                sender.Width = moveNewPosition.X + initialSize.Width;
            }
            //top
            else if (finalPosition.Y < resizingEdge)
            {
                sender.Top += moveNewPosition.Y;
                sender.Height -= moveNewPosition.Y;
            }
            //bottom
            else if (sender.Cursor == Cursors.SizeNS)
            {
                sender.Height = moveNewPosition.Y + initialSize.Height;
            }
        }

        private void ChangeTransformCursors(TextBoxTool sender)
        {
            transformTextBox = true;
            if ((finalPosition.X < resizingEdge && finalPosition.Y < resizingEdge) || (finalPosition.X > sender.Width - resizingEdge && finalPosition.Y > sender.Height - resizingEdge))
            {
                if (sender.Cursor != Cursors.SizeNWSE)
                    sender.Cursor = Cursors.SizeNWSE;
            }
            else if ((finalPosition.X > sender.Width - resizingEdge && finalPosition.Y < resizingEdge) || (finalPosition.X < resizingEdge && finalPosition.Y > sender.Height - resizingEdge))
            {
                if (sender.Cursor != Cursors.SizeNESW)
                    sender.Cursor = Cursors.SizeNESW;
            }
            else if (finalPosition.X < resizingEdge || finalPosition.X > sender.Width - resizingEdge)
            {
                if (sender.Cursor != Cursors.SizeWE)
                    sender.Cursor = Cursors.SizeWE;
            }
            else if (finalPosition.Y < resizingEdge || finalPosition.Y > sender.Height - resizingEdge)
            {
                if (sender.Cursor != Cursors.SizeNS)
                    sender.Cursor = Cursors.SizeNS;
            }
            else
            {
                if (sender.Cursor != Cursors.SizeAll)
                    sender.Cursor = Cursors.SizeAll;
                transformTextBox = false;
            }
        }

        private void DragText()
        {
            timerMove.Start();
            do
            {
                if (timerMove.ElapsedMilliseconds > 50)
                    timerMove.Restart();


            } while (dragTextBox);
            timerMove.Reset();
        }

        private void TextBox_GotFocus(object sender, EventArgs e)
        {
            if (raiseEvents)
            {
                Control parent = ((TextBoxTool)sender).FindForm().Controls[4];
                parent.Controls.Clear();
                TextBox_Properties(parent, (Control)sender);
            }
        }

        private int Get_fontIndex(Control box)
        {
            List<string> fontlist = new List<string>();
            foreach (FontFamily font in FontFamily.Families)
            {
                if (font.IsStyleAvailable(FontStyle.Regular))
                {
                    fontlist.Add(font.Name);
                }
            }
            for (int i = 0; i < fontlist.Count; i++)
                if (fontlist[i] == box.Font.FontFamily.Name.ToString())
                    return i;
            return -1;
        }

        private void TextBox_Properties(Control parent, Control sender)
        {
            List<string> fontlist = new List<string>();
            foreach (FontFamily font in FontFamily.Families)
            {
                if (font.IsStyleAvailable(FontStyle.Regular))
                    fontlist.Add(font.Name);
            }

            ComboBox fonts = new ComboBox();
            fonts.Size = new Size(fonts.Width + 80, fonts.Height);
            fonts.DataSource = fontlist;
            parent.Controls.Add(fonts);
            fonts.SelectedIndex = Get_fontIndex(sender);
            fonts.SelectedIndexChanged += new EventHandler(Fonts_SelectedIndexChanged);

            TextBox size = new System.Windows.Forms.TextBox
            {
                Size = new Size(50, 50)
            };
            parent.Controls.Add(size);
            size.Location = new Point(fonts.Location.X + fonts.Width + 5, fonts.Location.Y);
            size.Visible = true;
            size.Text = sender.Font.Size + "";
            size.LostFocus += new EventHandler(Size_LostFocus);
            size.KeyDown += new KeyEventHandler(Size_KeyDown);

            NoFocusBorderButton bold = new NoFocusBorderButton();
            parent.Controls.Add(bold);
            bold.Size = new Size(30, 30);
            bold.FlatStyle = FlatStyle.Flat;
            bold.FlatAppearance.BorderSize = 0;
            bold.BackgroundImage = Resources.Bold;
            bold.Location = new Point(fonts.Location.X, fonts.Location.Y + fonts.Height + 5);
            bold.Click += new EventHandler(Style_Clicked);
            bold.BackColor = idleColor;

            NoFocusBorderButton italic = new NoFocusBorderButton();
            parent.Controls.Add(italic);
            italic.Size = new Size(30, 30);
            italic.FlatStyle = FlatStyle.Flat;
            italic.FlatAppearance.BorderSize = 0;
            italic.BackgroundImage = Resources.Italic;
            italic.Location = new Point(bold.Location.X + bold.Width + 5, bold.Location.Y);
            italic.Click += new EventHandler(Style_Clicked);
            italic.BackColor = idleColor;

            NoFocusBorderButton underline = new NoFocusBorderButton();
            parent.Controls.Add(underline);
            underline.Size = new Size(30, 30);
            underline.FlatStyle = FlatStyle.Flat;
            underline.FlatAppearance.BorderSize = 0;
            underline.BackgroundImage = Resources.Underline;
            underline.Location = new Point(italic.Location.X + italic.Width + 5, italic.Location.Y);
            underline.Click += new EventHandler(Style_Clicked);
            underline.BackColor = idleColor;

            NoFocusBorderButton colorSwitch = new NoFocusBorderButton();
            parent.Controls.Add(colorSwitch);
            colorSwitch.Size = new Size(30, 30);
            colorSwitch.Location = new Point(size.Location.X, size.Location.Y + size.Height + 5);
            colorSwitch.Click += new EventHandler(Color_Clicked);
            colorSwitch.BackColor = this.ForeColor;
        }

        private void Color_Clicked(object sender, EventArgs e)
        {
            ColorDialog colorPicker = new ColorDialog();
            raiseEvents = false;
            if (colorPicker.ShowDialog() == DialogResult.OK)
            {
                ((Button)sender).BackColor = this.ForeColor = colorPicker.Color;
            }
            raiseEvents = true;
        }

        private void Update_FontStyle(object sender)
        {
            Control caller = (Control)sender;

            Font currentFont = this.Font;
            Font newFont = null;
            if (caller.Parent.Controls[2].BackColor == activeColor)
            {
                if (caller.Parent.Controls[3].BackColor == activeColor)
                {
                    if (caller.Parent.Controls[4].BackColor == activeColor)
                        newFont = new Font(currentFont, FontStyle.Bold | FontStyle.Italic | FontStyle.Underline);
                    else
                        newFont = new Font(currentFont, FontStyle.Bold | FontStyle.Italic);
                }
                else
                {
                    if (caller.Parent.Controls[4].BackColor == activeColor)
                        newFont = new Font(currentFont, FontStyle.Bold | FontStyle.Underline);
                    else
                        newFont = new Font(currentFont, FontStyle.Bold);
                }
            }
            else
            {
                if (caller.Parent.Controls[3].BackColor == activeColor)
                {
                    if (caller.Parent.Controls[4].BackColor == activeColor)
                        newFont = new Font(currentFont, FontStyle.Italic | FontStyle.Underline);
                    else
                        newFont = new Font(currentFont, FontStyle.Italic);
                }
                else
                {
                    if (caller.Parent.Controls[4].BackColor == activeColor)
                        newFont = new Font(currentFont, FontStyle.Underline);
                    else
                        newFont = new Font(currentFont, FontStyle.Regular);
                }
            }
            this.Font = newFont;

        }

        private void Style_Clicked(object sender, EventArgs e)
        {
            Control caller = (Control)sender;

            if (caller.BackColor == activeColor)
                caller.BackColor = idleColor;
            else
                caller.BackColor = activeColor;

            Update_FontStyle(sender);
        }

        private void Size_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt || e.KeyCode == Keys.Enter)
                this.Font = new Font(Font.FontFamily.Name, float.Parse(((System.Windows.Forms.TextBox)sender).Text));
            Update_FontStyle(sender);
        }

        private void Size_LostFocus(object sender, EventArgs e)
        {
            this.Font = new Font(Font.FontFamily.Name, float.Parse(((System.Windows.Forms.TextBox)sender).Text));
            Update_FontStyle(sender);
        }

        private void Fonts_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.Font = new Font(((ComboBox)sender).SelectedItem.ToString(), this.Font.Size);
            Update_FontStyle(sender);
        }

        private void TextBox_LostFocus(object sender, EventArgs e)
        {
            if (raiseEvents)
            {
                Control parent = ((TextBoxTool)sender).FindForm().Controls[4];
                parent.Controls.Clear();
            }
        }
    }
}
