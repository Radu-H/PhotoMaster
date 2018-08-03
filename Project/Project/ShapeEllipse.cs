using System;
using System.Drawing;
using System.Windows.Forms;

namespace Project
{
    public class DrawEllipse : Shape
    {
        Color color = Color.Black;
        public DrawEllipse(Point start, Point end, Control parent) : base(start, end, parent)
        {
            SetEllipseColor(Color.Black, this);
            EllipseProperties(this.FindForm().Controls[4], this);

            this.MouseDown += new MouseEventHandler(Rectangle_MouseClick);
            this.SizeChanged += new EventHandler(Rectangle_SizeChanged);
        }

        private void Rectangle_SizeChanged(object sender, EventArgs e)
        {
            //change height
            this.FindForm().Controls[4].Controls[1].Text = this.Height.ToString();
            //change width
            this.FindForm().Controls[4].Controls[3].Text = this.Width.ToString();
        }

        private void Rectangle_MouseClick(object sender, MouseEventArgs e)
        {
            ((Control)sender).Focus();
            Control parent = ((DrawEllipse)sender).FindForm().Controls[4];
            parent.Controls.Clear();
            EllipseProperties(parent, (Control)sender);
        }

        private void EllipseProperties(Control parent, Control sender)
        {
            Label height = new Label
            {
                Size = new Size(50, 30),
                Text = "Height:"
            };
            parent.Controls.Add(height);

            System.Windows.Forms.TextBox heightText = new System.Windows.Forms.TextBox
            {
                Size = new Size(50, 30),
                Text = sender.Height.ToString(),
                Location = new Point(height.Location.X + height.Width + 5, height.Location.Y)
            };
            heightText.TextChanged += new EventHandler(HeightText_TextChanged);
            parent.Controls.Add(heightText);

            Label width = new Label
            {
                Size = new Size(50, 30),
                Location = new Point(height.Location.X, height.Location.Y + height.Height + 5),
                Text = "Width:"
            };
            parent.Controls.Add(width);

            System.Windows.Forms.TextBox widthText = new System.Windows.Forms.TextBox
            {
                Size = new Size(50, 30),
                Text = sender.Width.ToString(),
                Location = new Point(width.Location.X + width.Width + 5, width.Height)
            };
            widthText.TextChanged += new EventHandler(WidthText_TextChanged);
            parent.Controls.Add(widthText);

            NoFocusBorderButton colorSwitch = new NoFocusBorderButton();
            parent.Controls.Add(colorSwitch);
            colorSwitch.BackColor = color;
            colorSwitch.Size = new Size(30, 30);
            colorSwitch.Location = new Point(width.Location.X, width.Location.Y + width.Height + 5);
            colorSwitch.Click += new EventHandler(Color_Clicked);
        }

        private void HeightText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                this.Height = Convert.ToInt32(((System.Windows.Forms.TextBox)sender).Text);
                SetEllipseColor(color, this);
            }
            catch (Exception)
            {
                Message("Invalid height! Try again.");
            }
        }

        private void WidthText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                this.Width = Convert.ToInt32(((System.Windows.Forms.TextBox)sender).Text);
                SetEllipseColor(color, this);
            }
            catch (Exception)
            {
                Message("Invalid width! Try again.");
            }
        }

        private void Color_Clicked(object sender, EventArgs e)
        {
            ColorDialog colorPicker = new ColorDialog();
            if (colorPicker.ShowDialog() == DialogResult.OK)
            {
                ((Button)sender).BackColor = colorPicker.Color;
                SetEllipseColor(colorPicker.Color, this);
                color = colorPicker.Color;
            }
        }

        private void SetEllipseColor(Color col, PictureBox rectangle)
        {
            Bitmap bmp = new Bitmap(rectangle.Width, rectangle.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            using (SolidBrush brush = new SolidBrush(col))
            {
                g.FillEllipse(brush, 0, 0, rectangle.Width, rectangle.Height);
                // rectangle.BackColor = col;
            }
            rectangle.Image = bmp;
        }
    }

}
