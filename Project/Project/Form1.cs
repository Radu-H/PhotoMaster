using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Diagnostics;
using Project.Properties;
using System.Linq;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Net.Mail;

namespace Project
{
    public partial class Form1 : Form
    {
        string Title = "Untitled";
        List<string> types = new List<string> { ".png", ".jpg", ".jpeg", ".bmp" };
        int zoomCount = 0, selectZoomCount = 0;
        int mod = -1;
        int activeToolBar = -1;
        /*
        mod=-1 : no tool selected
        mod= 0 : select mode
        mod= 1 : text box
        mod= 2 : rectangle
        mod= 3 : circular
        mod= 4 : draw ellipse
        */
        bool drawingActive = false;
        bool mouseDown = false;
        bool _selectingActive = false;
        bool fullscrn;
        Point selectStart = Point.Empty;
        Point originalSelectStart = Point.Empty;
        Point selectEnd = Point.Empty;
        Point originalSelectEnd = Point.Empty;
        Size originalSize = new Size();
        Bitmap original;
        Bitmap copiedImage;

        double multiplier = 1.2;
        public displayPanel display;
        public PictureBox workArea;
        public PictureBox selectionBox;
        public Panel toolBox;
        public Panel toolBar;
        public Panel properties;
        public Thread selecting;
        public Thread dragging;
        public ToolTip toolTip = new ToolTip();
        public Color activeColor = Color.FromArgb(62, 94, 94);
        public Color hoverColor = Color.FromArgb(92, 139, 139);
        public Color idleColor = Color.FromArgb(102, 153, 153);

        public Stopwatch timerMove;

        delegate void threadBoolCallback(bool active);

        public bool selectingActive
        {
            get { return _selectingActive; }
            set
            {
                _selectingActive = value;
                if (_selectingActive)
                {
                    this.menuStripItemEnable(true);
                }
                else
                {
                    this.menuStripItemEnable(false);
                    toolBox.Controls[0].BackColor = toolBox.Controls[3].BackColor = idleColor;
                }
            }
        }

        private void menuStripItemEnable(bool active)
        {
            if (this.menuStrip1.InvokeRequired)
            {
                threadBoolCallback callbk = new threadBoolCallback(menuStripItemEnable);
                this.Invoke(callbk, new object[] { active });
            }
            else
            {
                deselectToolStripMenuItem.Enabled = active;
                cropToolStripMenuItem.Enabled = active;
                copyToolStripMenuItem.Enabled = active;
                cutToolStripMenuItem.Enabled = active;
            }
        }

        public Form1()
        {
            InitializeComponent();

            Application.ThreadException += reportCrash;
            AppDomain.CurrentDomain.UnhandledException += reportCrash;

            //picturebox
            workArea = new PictureBox();
            workArea.Size = new Size(450, 450);
            workArea.Location = Point.Empty;
            workArea.SizeMode = PictureBoxSizeMode.Zoom;
            workArea.BackColor = Color.Transparent;
            workArea.Visible = false;
            workArea.MouseWheel += new System.Windows.Forms.MouseEventHandler(pictureBox1_Wheel);
            workArea.SizeChanged += new System.EventHandler(workArea_Size);


            //display panel
            display = new displayPanel();
            display.Location = new Point(53, 42);
            this.Controls.Add(display);
            display.BackColor = Color.DarkGray;
            display.AutoScroll = true;
            display.Size = new Size(490, 490);
            display.Resize += new System.EventHandler(panel1_Resize);
            //display.MouseLeave += new EventHandler(properties_MouseLeave);
            workArea.MouseEnter += new EventHandler(properties_MouseEnter);
            display.Controls.Add(workArea);


            //tool box
            toolBox = new Panel();
            this.Controls.Add(toolBox);
            toolBox.Enabled = true;
            toolBox.BackColor = Color.Transparent;
            toolBox.Location = new Point(5, display.Location.Y);
            toolBox.Size = new Size(display.Left - 10, display.ClientSize.Height);
            generateToolBox();

            //toolBar
            toolBar = new Panel();
            this.Controls.Add(toolBar);
            toolBar.Enabled = true;
            toolBar.BackColor = Color.Transparent;
            toolBar.Location = new Point(display.Left, display.Top + display.Height + 5);
            toolBar.Size = new Size(display.Width, 34);

            //selection box
            selectionBox = new PictureBox();

            selectionBox.Size = workArea.Size;
            selectionBox.Location = Point.Empty;
            selectionBox.BackColor = Color.Transparent;
            selectionBox.Enabled = false;
            workArea.Controls.Add(selectionBox);
            selectionBox.MouseDown += new MouseEventHandler(selectionBox_MouseDown);
            selectionBox.MouseUp += new MouseEventHandler(selectionBox_MouseUp);
            selectionBox.MouseMove += new MouseEventHandler(selectionBox_Move);

            //properties panel
            properties = new Panel();
            this.Controls.Add(properties);
            properties.Visible = false;
            properties.MouseEnter += new EventHandler(properties_MouseEnter);
            properties.MouseLeave += new EventHandler(properties_MouseLeave);

            //timer , stopwatch
            timerMove = new Stopwatch();

            //BackColor = Color.FromArgb(2,88,84);
            //menuStrip1.BackColor = Color.FromArgb(2, 88, 84);
            BackColor = Color.FromArgb(102, 153, 153);
            menuStrip1.BackColor = Color.FromArgb(92, 139, 139);
        }

        private void reportCrash(object sender, ThreadExceptionEventArgs e)
        {
            reviewForm(e.Exception.ToString());
            Application.Exit();
        }
        private void reportCrash(object sender, UnhandledExceptionEventArgs e)
        {
            reviewForm(e.ExceptionObject.ToString());
            Environment.Exit(0);
        }

        private void sendReport(string message, string subject)
        {
            string from = "developingsoftware01@gmail.com";
            string pass = "C#ForTheWin";

            SmtpClient client = new SmtpClient();
            client.Port = 587;
            client.Host = "smtp.gmail.com";
            client.EnableSsl = true;
            client.Timeout = 10000;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential(from, pass);

            MailMessage sendmail = new MailMessage();
            sendmail.IsBodyHtml = false;
            sendmail.From = new MailAddress("developingsoftware01@gmail.com", "Program");
            sendmail.To.Add(new MailAddress("DraekorProjects@gmail.com"));
            sendmail.Subject = subject;
            sendmail.Body = message;

            client.Send(sendmail);
        }

        private void reviewForm()
        {
            // Form f = popForm("Feedback/ Bug Report", 500, 700);
            Form f = new Form();
            f.Text = "Feedback/ Bug Report";
            f.Size = new Size(500, 700);
            
            Label name_lb = popLabel(150, 50, "Display name: ", f);
            TextBox name = popText(250, 50, f);
            Label message_lb = popLabel(150, 100, "Please give us your feedback:", f);
            RichTextBox message = popRichText(100, 130, 300, 400, f);
            Button bt = popButton(200, 550, "Send", f);
            
            bt.Click += new EventHandler(sendFeedback);

            f.ShowDialog();
        }
        private void reviewForm(object exception)
        {
            Form f = new Form();
            f.Text = "Crash Report";
            f.Size = new Size(500, 700);
            Label name_lb = popLabel(150, 50, "Display name: ", f);
            TextBox name = popText(250, 50, f);
            Label message_lb = popLabel(100, 100, "Please describe what were you doing when the crash occured:", f);
            RichTextBox message = popRichText(100, 130, 300, 400, f);
            Button bt = popButton(200, 550, "Send", f);

            bt.Click += (sender, e) => { sendBug(sender, e, (string)exception); };
            f.ShowDialog();
        }

        private void sendBug(object sender, EventArgs e, string exception)
        {
            string message = "Report sent by:   ";
            message += ((Button)sender).FindForm().Controls[1].Text + "\n\n";
            message += "User's message:\n";
            message += ((Button)sender).FindForm().Controls[3].Text + "\n\n";
            message += "Exception:\n" + exception;

            sendReport(message, "Crash report");

            ((Button)sender).FindForm().Close();
        }

        private void sendFeedback(object sender, EventArgs e)
        {
            string message = "Report sent by:   ";
            message += ((Button)sender).FindForm().Controls[1].Text + "\n\n";
            message += "User's message:\n";
            message += ((Button)sender).FindForm().Controls[3].Text;

            sendReport(message, "User's report");

            ((Button)sender).FindForm().Close();
        }

        private void properties_MouseEnter(object sender, EventArgs e)
        {
            if (index > -1)
                if (elements[index].GetType().Equals(typeof(textBox)))
                    ((textBox)elements[index]).raiseEventsStatus(true);
        }

        private void properties_MouseLeave(object sender, EventArgs e)
        {
            if (index > -1)
                if (elements[index].GetType().Equals(typeof(textBox)))
                    ((textBox)elements[index]).raiseEventsStatus(false);
        }

        private void workArea_Size(object sender, EventArgs e)
        {
            if (selectingActive == true)
            {
                if (selectionBox.Size.Width > workArea.Size.Width)
                {
                    selectZoomCount--;
                    selectStart = new Point((int)(originalSelectStart.X * Math.Pow(multiplier, selectZoomCount)), (int)(originalSelectStart.Y * Math.Pow(multiplier, selectZoomCount)));
                    selectEnd = new Point((int)(originalSelectEnd.X * Math.Pow(multiplier, selectZoomCount)), (int)(originalSelectEnd.Y * Math.Pow(multiplier, selectZoomCount)));
                }
                else if (selectionBox.Size.Width < workArea.Size.Width)
                {
                    selectZoomCount++;
                    selectStart = new Point((int)(originalSelectStart.X * Math.Pow(multiplier, selectZoomCount)), (int)(originalSelectStart.Y * Math.Pow(multiplier, selectZoomCount)));
                    selectEnd = new Point((int)(originalSelectEnd.X * Math.Pow(multiplier, selectZoomCount)), (int)(originalSelectEnd.Y * Math.Pow(multiplier, selectZoomCount)));
                }
                selectionBox.Size = workArea.Size;
                if (selectingActive == true)
                {
                    selectingActive = false;
                    if (selecting.IsAlive == true)
                    {
                        selectionBox.Invalidate();
                        Thread.Sleep(3);
                    }
                    selecting = new Thread(drawSelectionThread);
                    selecting.Start();
                }
            }
            //foreach(Control el in elements)
            //{
            //    if (selectionBox.Size.Width > workArea.Size.Width)
            //    {
            //        if (el.Parent.Equals(typeof(Panel)))
            //        {
            //            el.Size = new Size((int)(el.Width / multiplier), (int)(el.Height / multiplier));                
            //        }
            //        else
            //        {
            //            el.Location = new Point((int)(el.Location.X / multiplier), (int)(el.Location.Y /multiplier));
            //            el.Size = new Size((int)(el.Width / multiplier), (int)(el.Height / multiplier));
            //        }
            //        if (el.GetType().Equals(typeof(textBox)))
            //        {
            //            Font newFont = new Font(((textBox)el).Font.FontFamily, (int)(((textBox)el).Font.Size / multiplier), ((textBox)el).Font.Style);
            //            ((textBox)el).Font = newFont;
            //        }
            //    }
            //    else if (selectionBox.Size.Width < workArea.Size.Width)
            //    {
            //        if (el.Parent.Equals(typeof(Panel)))
            //        {
            //            el.Size = new Size((int)(el.Width * multiplier), (int)(el.Height * multiplier));
            //        }
            //        else
            //        {
            //            el.Location = new Point((int)(el.Location.X * multiplier), (int)(el.Location.Y * multiplier));
            //            el.Size = new Size((int)(el.Width * multiplier), (int)(el.Height * multiplier));
            //        }
            //        if (el.GetType().Equals(typeof(textBox)))
            //        {
            //            Font newFont = new Font(((textBox)el).Font.FontFamily, (int)(((textBox)el).Font.Size * multiplier), ((textBox)el).Font.Style);
            //            ((textBox)el).Font = newFont;
            //        }
            //    }               
            //}
        }

        private void selectionBox_MouseDown(object sender, MouseEventArgs e)
        {
            selectionBox.Size = workArea.Size;
            selectZoomCount = 0;
            if (selectingActive == true)
            {
                selectingActive = false;
            }
            if (drawingActive == true)
            {
                drawingActive = false;
            }

            mouseDown = true;
            originalSelectStart = selectStart = e.Location;
            selectEnd = new Point(-1, -1);
            selectionBox.Invalidate();
            textBoxRefresh();
        }

        private Rectangle createRectangle(Point start, Point end)
        {
            Rectangle r = new Rectangle();

            if (start.X < end.X)
            {
                r.X = start.X;
                r.Width = end.X - start.X;
            }
            else
            {
                r.X = end.X;
                r.Width = start.X - end.X;
            }
            if (start.Y < end.Y)
            {
                r.Y = start.Y;
                r.Height = end.Y - start.Y;
            }
            else
            {
                r.Y = end.Y;
                r.Height = start.Y - end.Y;
            }
            return r;
        }

        private void drawSelection(Point start, Point end)
        {
            Rectangle r = createRectangle(start, end);

            Graphics g = selectionBox.CreateGraphics();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Pen p = new Pen(Brushes.Aquamarine);

            p.DashStyle = DashStyle.DashDot;
            if (mod == 0)
                g.DrawRectangle(p, r);
            if (mod == 3)
                g.DrawEllipse(p, r);
        }

        private void drawRectangle(Point start, Point end)
        {
            Rectangle r = createRectangle(selectStart, selectEnd);

            Graphics g = selectionBox.CreateGraphics();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Pen p = new Pen(Brushes.Black);

            p.DashStyle = DashStyle.Solid;
            g.DrawRectangle(p, r);
            g.FillRectangle(Brushes.Black, r);
        }

        private void drawEllipse(Point start,Point end)
        {
            Rectangle r = createRectangle(selectStart, selectEnd);

            Graphics g = selectionBox.CreateGraphics();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Pen p = new Pen(Brushes.Black);

            p.DashStyle = DashStyle.Solid;
            g.DrawEllipse(p, r);
            g.FillEllipse(Brushes.Black, r);
        }

        private void drawSelectionThread()
        {
            selectingActive = true;

            Rectangle r = createRectangle(selectStart, selectEnd);

            Graphics g = selectionBox.CreateGraphics();
            Pen p = new Pen(Brushes.Aquamarine);

            int x = 1;
            do
            {
                if (x == 1)
                {
                    x++;
                    p.Brush = Brushes.Black;
                }
                else
                {
                    x--;
                    p.Brush = Brushes.White;
                }

                p.DashStyle = DashStyle.Solid;
                if(mod==0)
                    g.DrawRectangle(p, r);
                if(mod==3)
                    g.DrawEllipse(p, r);
               
                Thread.Sleep(1);
               
            } while (selectingActive);

            selectionBox.Invalidate();
            textBoxRefreshThread();
        }

        private void drawRectangleThread()
        {
            drawingActive = true;

            Rectangle r = createRectangle(selectStart, selectEnd);

            Graphics g = selectionBox.CreateGraphics();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Pen p = new Pen(Brushes.Black);

            p.DashStyle = DashStyle.Solid;
            g.DrawRectangle(p, r);
            g.FillRectangle(Brushes.Black, r);

            //selectionBox.Refresh();
        }

        private void selectionBox_Move(object sender, MouseEventArgs e)
        {
            Point selectCurrent = new Point(e.Location.X, e.Location.Y);

            if (mouseDown)
            {
                switch (mod)
                {
                    case 0:
                    case 1:
                    case 3:
                        if (selectEnd.X != -1)
                        {
                            drawSelection(selectStart, selectEnd);
                        }
                        originalSelectEnd = selectEnd = selectCurrent;
                        drawSelection(selectStart, selectCurrent);
                        break;
                    case 2:
                        if (selectEnd.X != -1)
                        {
                            drawRectangle(selectStart, selectEnd);
                        }
                        originalSelectEnd = selectEnd = selectCurrent;
                        drawRectangle(selectStart, selectCurrent);
                        break;
                    case 4:
                        if(selectEnd.X!=-1)
                        {
                            drawEllipse(selectStart, selectEnd);
                        }
                        originalSelectEnd = selectEnd = selectCurrent;
                        drawEllipse(selectStart, selectEnd);
                        break;
                }
                selectionBox.Invalidate();
                Application.DoEvents();
                textBoxRefresh();
            }
        }

        private void selectionBox_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
            if (toolBox.Controls[1].BackColor != activeColor && mod!=2 && mod!=4)
            {
                selecting = new Thread(drawSelectionThread);
                if (selectEnd.X != -1)
                {
                    selecting.Start();
                }
            }
            else
            {
                switch (mod)
                {
                    case 1:
                        {
                            textBox txtbox = new textBox(createRectangle(selectStart, selectEnd), workArea);
                            txtbox.Click += new EventHandler(object_Click);
                            addElement(txtbox);
                            break;
                        }
                    /*
                    versiune buggy suprapus pe display
                    nu se face refreshul asa cum ar trebui

                    textBox txtbox = new textBox(createRectangle(selectStart, selectEnd),display);
                    */
                    case 2:
                        {
                            //Rectangle r = createRectangle(selectStart, selectEnd);
                            //PictureBox rectangle = new PictureBox();
                            //display.Controls.Add(rectangle);
                            //rectangle.BringToFront();
                            //rectangle.Size = new Size(r.Width, r.Height);
                            //rectangle.Location = new Point(r.X + workArea.Left, r.Y + workArea.Top);

                            DrawRectangle rectangle = new DrawRectangle(selectStart, selectEnd, display);
                            rectangle.MouseDown += new MouseEventHandler(object_Click);
                            //setRectangleColor(Color.Black, rectangle);

                            addElement(rectangle);
                            //this.Controls[4].Controls.Clear();
                            //rectangleProperties();

                            //rectangle.MouseDown += new MouseEventHandler(move_MouseDown);
                            //rectangle.MouseMove += new MouseEventHandler(move_MouseMove);
                            //rectangle.MouseUp += new MouseEventHandler(move_MouseUp);
                            //rectangle.MouseClick += new MouseEventHandler(rectangle_MouseClick);
                            //rectangle.SizeChanged += new EventHandler(rectangle_SizeChanged);
                            break;
                        }
                    case 4:
                        {
                            DrawEllipse ellipse = new DrawEllipse(selectStart, selectEnd, display);
                            ellipse.MouseDown += new MouseEventHandler(object_Click);
                            addElement(ellipse);
                            break;
                        }
                }
                deactivateTools();
            }
        }

        private void setRectangleColor(Color col, PictureBox rectangle)
        {
            Bitmap bmp = new Bitmap(rectangle.Width, rectangle.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            using (SolidBrush brush = new SolidBrush(col))
            {
                g.FillRectangle(brush, 0, 0, rectangle.Width, rectangle.Height);
                rectangle.BackColor = col;
            }
            rectangle.Image = bmp;
        }

        private void rectangle_SizeChanged(object sender, EventArgs e)
        {
            //schimba height
            this.Controls[4].Controls[1].Text = elements[index].Height.ToString();
            //schimba width
            this.Controls[4].Controls[3].Text = elements[index].Width.ToString();
        }

        private void rectangle_MouseClick(object sender, MouseEventArgs e)
        {
            ((Control)sender).Focus();
            this.Controls[4].Controls.Clear();
            rectangleProperties();
        }


        private void pictureBox1_Wheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (zoomCount < 25)
                {
                    zoomCount++;
                    zoomIn(e.Location);
                }
            }
            else
            {
                if (zoomCount > -15 && (workArea.Width >= 48 || workArea.Height >= 48))
                {
                    zoomCount--;
                    zoomOut(e.Location);
                }
            }
            display_update_background();
        }

        void initialise_workArea()
        {
            Bitmap drawarea = new Bitmap(workArea.Size.Width, workArea.Size.Height);
            workArea.Image = drawarea;

            using (Graphics g = Graphics.FromImage(drawarea))
            {
                g.Clear(Color.Transparent);
                workArea.BorderStyle = BorderStyle.Fixed3D;
            }
        }

        void message(string x)
        {
            MessageBox.Show(x);
        }

        private void generateToolBox()
        {
            Button select = createTool(toolBox.Location.X, 5, toolBox);
            toolTip.SetToolTip(select, "Selection Tool");
            select.BackgroundImage = Properties.Resources.Selection;
            toolBox.Enabled = false;

            Button textbox = createTool(toolBox.Location.X, 40, toolBox);
            toolTip.SetToolTip(textbox, "Text Box");
            textbox.BackgroundImage = Resources.Text;

            Button rectangle = createTool(toolBox.Location.X, 75, toolBox);
            toolTip.SetToolTip(rectangle, "Rectangle Tool");
            rectangle.BackgroundImage = Resources.Rectangle_inactive;

            Button circSelect = createTool(toolBox.Location.X, 110, toolBox);
            toolTip.SetToolTip(circSelect, "Circular Select");
            //temp
            circSelect.BackgroundImage = Resources.Circle_active;
            //temp

            Button ellipseDraw = createTool(toolBox.Location.X, 145, toolBox);
            toolTip.SetToolTip(ellipseDraw, "Draw Ellipse");
            ellipseDraw.BackgroundImage = Resources.Circle_active;
        }

        private void activateMode()
        {
            deactivateAll();
            switch (mod)
            {
                case -1: deactivateAll(); break;
                case 0:
                case 3: activateSelect(true); break;
                case 1: activateText(true); break;
                case 2: activateRectangle(true); break;
                case 4:activateEllipse(true);break;
                    
            }
            textBoxRefresh();
        }

        private void deactivateAll()
        {
            activateSelect(false);
            activateText(false);
            if (selectingActive == true)
                if (selecting.IsAlive == true)
                {
                    selectingActive = false;
                    selectionBox.Invalidate();
                    textBoxRefresh();
                }
        }

        private void deactivateTools()
        {
            activateSelect(false);
            activateText(false);
            if (selectingActive == true)
                if (selecting.IsAlive == true)
                {
                    selectingActive = false;
                    selectionBox.Invalidate();
                    textBoxRefresh();
                }
            foreach (Button bt in toolBox.Controls)
            {
                bt.BackColor = idleColor;
            }
            mod = -1;
        }

        private void activateSelect(bool active)
        {
            selectionBox.Enabled = active;
        }

        private void activateText(bool active)
        {
            selectionBox.Enabled = active;
        }

        private void activateRectangle(bool active)
        {
            selectionBox.Enabled = active;
        }

        private void activateEllipse(bool active)
        {
            selectionBox.Enabled = active;
        }

        private void tool_Clicked(object sender, EventArgs e)
        {
            if (selectingActive == true)
                if (selecting.IsAlive == true)
                {
                    selectingActive = false;
                    selectionBox.Invalidate();
                    textBoxRefresh();
                }
            if (((Button)sender).BackColor == activeColor)
            {
                ((Button)sender).BackColor = idleColor;
                mod = -1;
            }
            else
            {
                foreach (Button bt in toolBox.Controls)
                {
                    bt.BackColor = idleColor;
                }
                 ((Button)sender).BackColor = activeColor;
                mod = ((Button)sender).Parent.Controls.GetChildIndex(((Button)sender));
                switch (mod)
                {
                    case 2: ((Button)sender).BackgroundImage = Resources.Rectangle_active; break;
                    case 4: ((Button)sender).BackgroundImage = Resources.Circle_active; break;
                }
            }
            activateMode();
        }
        
        private void rectangleProperties()
        {
            Label height = new Label();
            height.Size = new Size(50, 30);
            height.Text = "Height:";
            properties.Controls.Add(height);

            TextBox heightText = new TextBox();
            heightText.Size = new Size(50, 30);
            heightText.Text = elements[index].Height.ToString();
            heightText.Location = new Point(height.Location.X + height.Width + 5, height.Location.Y);
            heightText.TextChanged += new EventHandler(heightText_TextChanged);
            properties.Controls.Add(heightText);

            Label width = new Label();
            width.Size = new Size(50, 30);
            width.Location = new Point(height.Location.X, height.Location.Y + height.Height + 5);
            width.Text = "Width:";
            properties.Controls.Add(width);

            TextBox widthText = new TextBox();
            widthText.Size = new Size(50, 30);
            widthText.Text = elements[index].Width.ToString();
            widthText.Location = new Point(width.Location.X + width.Width + 5, width.Height);
            widthText.TextChanged += new EventHandler(widthText_TextChanged);
            properties.Controls.Add(widthText);

            noFocusBorderButton colorSwitch = new noFocusBorderButton();
            properties.Controls.Add(colorSwitch);
            colorSwitch.BackColor = ((PictureBox)elements[index]).BackColor;
            colorSwitch.Size = new Size(30, 30);
            colorSwitch.Location = new Point(width.Location.X, width.Location.Y + width.Height + 5);
            colorSwitch.Click += new EventHandler(color_Clicked);
        }

        private void color_Clicked(object sender, EventArgs e)
        {
            ColorDialog colorPicker = new ColorDialog();
            if (colorPicker.ShowDialog() == DialogResult.OK)
            {
                ((Button)sender).BackColor = colorPicker.Color;
                setRectangleColor(colorPicker.Color, (PictureBox)elements[index]);
                
            }
        }

        private void heightText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                elements[index].Height = Convert.ToInt32(((TextBox)sender).Text);
            }
            catch(Exception )
            {
                message("Invalid height! Try again.");
            }
        }

        private void widthText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                elements[index].Width = Convert.ToInt32(((TextBox)sender).Text);
            }
            catch(Exception )
            {
                message("Invalid width! Try again.");
            }
        }

        delegate void threadtextboxRefresh();

        private void textBoxRefreshThread()
        {
           
                bool invReq = false;
                foreach(textBox elm in elements.OfType<textBox>())
                    if(elm.InvokeRequired)
                    {
                        invReq = true;
                        break;
                    }
                if (invReq)
                {
                    threadtextboxRefresh callbk = new threadtextboxRefresh(textBoxRefreshThread);
                    this.Invoke(callbk);
                }
                else
                {
                    foreach (textBox obj in elements.OfType<textBox>())
                    {
                        obj.ForceRefresh();
                    }
                }
            
        }

        private void textBoxRefresh()
        {
            foreach (textBox obj in elements.OfType<textBox>())
            {
                obj.ForceRefresh();
            }
        }

        private Button createTool(int x, int y, Control f)
        {
            Button bt = new Button();

            bt.Location = new Point(x, y);
            bt.Size = new Size(toolBox.Width - 10, toolBox.Width - 10);
            bt.MouseEnter += new EventHandler(tool_MouseEnter);
            bt.MouseLeave += new EventHandler(tool_MouseLeave);
            bt.BackColor = idleColor;
            bt.TabStop = false;
            bt.Padding = new Padding(5, 0, 5, 0);
            bt.FlatStyle = FlatStyle.Flat;
            bt.FlatAppearance.BorderSize = 0;
            bt.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
            bt.Click += new System.EventHandler(tool_Clicked);
            f.Controls.Add(bt);

            return bt;
        }

        #region POPCONTROLS

        Form popForm(string txt)
        {
            Form f = new Form();
            f.Text = txt;
            f.FormBorderStyle = FormBorderStyle.Fixed3D;
            f.Show();

            return f;
        }

        Form popForm(string txt, int w, int h)
        {
            Form f = new Form();
            f.Text = txt;
            f.Size = new Size(w, h);
            f.Show();

            return f;
        }

        Label popLabel(int x, int y, string txt, Form f)
        {
            Label lb = new Label();
            lb.Location = new Point(x, y);
            lb.Text = txt;
            lb.AutoSize = true;
            f.Controls.Add(lb);
            return lb;
        }

        TextBox popText(int x, int y, Form f)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            f.Controls.Add(t);
            return t;
        }

        RichTextBox popRichText(int x, int y, int w, int h, Form f)
        {
            RichTextBox t = new RichTextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(w, h);
            f.Controls.Add(t);
            return t;
        }

        Button popButton(int x, int y, string txt, Control f)
        {
            Button bt = new Button();
            bt.Location = new Point(x, y);
            bt.Text = txt;
            f.Controls.Add(bt);
            return bt;
        }

        GroupBox popGrBox(int x, int y, string caption, List<string> buttons, Form f)
        {
            GroupBox gb = new GroupBox();
            int i = 0;
            gb.Location = new Point(x, y);
            gb.Text = caption;
            foreach (string s in buttons)
            {
                RadioButton rb = new RadioButton();

                rb.Text = s;

                rb.Location = new Point(15, (i + 1) * 20);
                gb.Controls.Add(rb);
                rb.Click += new System.EventHandler(saveType_Changed);
                i++;
                if (i > 3)
                    gb.Height += 20;
            }
            ((RadioButton)gb.Controls[0]).Checked = true;
            f.Controls.Add(gb);
            ((RadioButton)gb.Controls[0]).PerformClick();

            return gb;
        }
        #endregion

        #region FILE BUTTONS

        #region NEW

        private void newAction()
        {
            if (drag == true)
                if (dragging.IsAlive == true)
                    drag = false;

            deactivateTools();

            initialise_workArea();

            Form newScreen = popForm("New", 400, 300);

            Label title_lb = popLabel(50, 50, "Project's Title", newScreen);

            TextBox title = popText(200, 50, newScreen);

            Label open_lb = popLabel(50, 100, "Open Picture (Optional)", newScreen);

            Button browse = popButton(210, 95, "Browse", newScreen);

            browse.Click += new System.EventHandler(browseClicked);

            PictureBox thumbnail = new PictureBox();
            thumbnail.Visible = false;
            thumbnail.Location = new Point(85, 125);
            thumbnail.SizeMode = PictureBoxSizeMode.Zoom;
            thumbnail.Width = 50;
            thumbnail.Height = 50;

            newScreen.Controls.Add(thumbnail);

            Button create = popButton(100, 200, "Create", newScreen);

            create.Click += new System.EventHandler(newOption_Clicked);
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            newAction();
        }

        private void browseClicked(object sender, EventArgs e)
        {
            browseImage((PictureBox)((Button)sender).Parent.FindForm().Controls[4]);
        }

        private void browseImage(PictureBox pic)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Image Files (*.png, *.jpg, *.jpeg, *.bmp)|*.png; *.jpg; *.jpeg; *.bmp)";

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
               // pic.Size = new Size(450, 450);
                Image openImage = Image.FromFile(fileDialog.FileName);
                pic.Visible = true;
                original = (Bitmap)openImage;
                resizePictureBox(pic, openImage);
                zoomCount = 0;
                workArea.Visible = true;
                enableMenu(true);
                workArea.BorderStyle = BorderStyle.Fixed3D;
                centerPictureBox();              
                originalSize = workArea.Size;

                properties.Location = new Point(display.Location.X + display.Width + 5, display.Location.Y);
                properties.Size = new Size(properties.Right - 5, display.Height);
                display_update_background();
            }

        }

        private void enableMenu(bool active)
        {
            closeToolStripMenuItem.Enabled = active;
            saveAsToolStripMenuItem.Enabled = active;
            saveToolStripMenuItem1.Enabled = active;
            printToolStripMenuItem.Enabled = active;
            zoomInToolStripMenuItem.Enabled = active;
            zoomOutToolStripMenuItem.Enabled = active;
            fitOnScreenToolStripMenuItem.Enabled = active;
            allToolStripMenuItem.Enabled = active;
            grayscaleToolStripMenuItem.Enabled = active;
            invertColorsToolStripMenuItem.Enabled = active;
            cartoonToolStripMenuItem.Enabled = active;
            sepiaToolStripMenuItem.Enabled = active;
            redFilterToolStripMenuItem.Enabled = active;
            greenFilterToolStripMenuItem.Enabled = active;
            blueFilterToolStripMenuItem.Enabled = active;
            revertToOriginalToolStripMenuItem.Enabled = active;
            toolBox.Enabled = active;
        }

        private void resizePictureBox(PictureBox pic, Image openImage)
        {
            pic.Image = openImage;

            double getWidth = openImage.Width;
            double getHeight = openImage.Height;
            double ratio = 0;

            if (getWidth > getHeight)
            {
                ratio = getWidth / pic.Width;
                getWidth = pic.Width;
                getHeight = (int)(getHeight / ratio);
            }
            else
            {
                ratio = getHeight / pic.Height;
                getHeight = pic.Height;
                getWidth = (int)(getWidth / ratio);
            }

            pic.Size = new Size((int)getWidth, (int)getHeight);
            
        }

        private void newOption_Clicked(object sender, EventArgs e)
        {
            Button buttonClicked = sender as Button;

            if (((Button)sender).Parent.FindForm().Controls[1].Text.Length <= 0)
                message("Insert a project name");
            else
            {
                Title = ((Button)sender).Parent.FindForm().Controls[1].Text;
                this.Text += " - " + Title;
                if (((PictureBox)((Button)sender).Parent.FindForm().Controls[4]).Visible == true)
                {
                    resizePictureBox(workArea, ((PictureBox)((Button)sender).Parent.FindForm().Controls[4]).Image);
                }
                workArea.Visible = true;
                toolBox.Enabled = true;
                centerPictureBox();
                ((Button)sender).Parent.FindForm().Close();
                enableMenu(true);

            }
        }
        #endregion

        #region OPEN

        private void openAction()
        {
            if (drag == true)
                if (dragging.IsAlive == true)
                    drag = false;

            deactivateTools();
            browseImage(workArea);
            if (this.Text.Equals("PhotoMaster"))
                this.Text += " - " + Title;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openAction();
        }

        private void centerPictureBox()
        {
            int width = display.Width / 2 - workArea.Width / 2;
            int height = display.Height / 2 - workArea.Height / 2;

            workArea.Location = new Point(width, height);

        }
        #endregion

        #region SAVE
        private void saveToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //to be implemented
        }
        #endregion

        #region SAVE AS

        private void saveAsAction()
        {
            Form saveAsScreen = popForm("Save As", 500, 500);

            Label path_lb = popLabel(50, 50, "Save Path:", saveAsScreen);

            TextBox path = popText(150, 50, saveAsScreen);
            path.Text = "D:\\";

            Button browse = popButton(280, 48, "Browse", saveAsScreen);

            browse.Click += new System.EventHandler(browseSaveAs_Clicked);

            Label name_lb = popLabel(50, 100, "File Name:", saveAsScreen);

            TextBox fname = popText(150, 100, saveAsScreen);
            fname.Text = Title;
            fname.TextChanged += new EventHandler(fname_TextChanged);

            Button save = popButton(150, 150, "Save", saveAsScreen);

            save.Click += new System.EventHandler(save_Clicked);

            GroupBox savetypes = popGrBox(150, 200, "Formats", types, saveAsScreen);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveAsAction();
        }

        private void fname_TextChanged(object sender, EventArgs e)
        {
            int i = ((TextBox)sender).SelectionStart;
            ((TextBox)sender).TextChanged -= fname_TextChanged;
            if (!((TextBox)sender).Text.EndsWith("." + getCheckedType((GroupBox)(((TextBox)sender).Parent.Controls[6]))))
            {
                if (((TextBox)sender).Text.LastIndexOf(".") > -1)
                    ((TextBox)sender).Text = ((TextBox)sender).Text.Substring(0, ((TextBox)sender).Text.LastIndexOf("."));
                ((TextBox)sender).Text += getCheckedType((GroupBox)(((TextBox)sender).Parent.Controls[6]));
            }
            ((TextBox)sender).TextChanged += fname_TextChanged;
            ((TextBox)sender).SelectionStart = i;
        }

        private string getCheckedType(GroupBox gb)
        {
            foreach (RadioButton c in gb.Controls)
            {
                if (c.Checked)
                    return c.Text;
            }
            return "";
        }

        private void browseSaveAs_Clicked(object sender, EventArgs e)
        {
            FolderBrowserDialog saveDialog = new FolderBrowserDialog();
            saveDialog.ShowDialog();
            ((Button)sender).Parent.FindForm().Controls[1].Text = saveDialog.SelectedPath;
        }

        private void save_Clicked(object sender, EventArgs e)
        {
            Bitmap saveBitmap = new Bitmap(this.workArea.Image);

            Graphics g = Graphics.FromImage(saveBitmap);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            foreach (Control obj in elements)
            {
                if (obj.GetType().Equals(typeof(PictureBox)))
                    g.DrawImage(((PictureBox)obj).Image, (int)((((PictureBox)obj).Location.X - workArea.Location.X) * workArea.Image.Width / workArea.Width), (int)((((PictureBox)obj).Location.Y - workArea.Location.Y) * workArea.Image.Height / workArea.Height), ((PictureBox)obj).Width * workArea.Image.Width / workArea.Width, ((PictureBox)obj).Height * workArea.Image.Height / workArea.Height);

                if (obj.GetType().Equals(typeof(DrawRectangle)))
                    g.DrawImage(((DrawRectangle)obj).Image, (int)((((DrawRectangle)obj).Location.X - workArea.Location.X) * workArea.Image.Width / workArea.Width), (int)((((DrawRectangle)obj).Location.Y - workArea.Location.Y) * workArea.Image.Height / workArea.Height), ((DrawRectangle)obj).Width * workArea.Image.Width / workArea.Width, ((DrawRectangle)obj).Height * workArea.Image.Height / workArea.Height);

                if (obj.GetType().Equals(typeof(DrawEllipse)))
                    g.DrawImage(((DrawEllipse)obj).Image, (int)((((DrawEllipse)obj).Location.X - workArea.Location.X) * workArea.Image.Width / workArea.Width), (int)((((DrawEllipse)obj).Location.Y - workArea.Location.Y) * workArea.Image.Height / workArea.Height), ((DrawEllipse)obj).Width * workArea.Image.Width / workArea.Width, ((DrawEllipse)obj).Height * workArea.Image.Height / workArea.Height);

                if (obj.GetType().Equals(typeof(textBox)))
                {
                    Font saveFont = new Font(((textBox)obj).Font.FontFamily ,((textBox)obj).Font.Size * workArea.Image.Width / workArea.Width, ((textBox)obj).Font.Style);
                    SolidBrush textColor = new SolidBrush(((textBox)obj).ForeColor);
                    g.DrawString(((textBox)obj).Text, saveFont, textColor, obj.Location.X * workArea.Image.Width / workArea.Width, obj.Location.Y * workArea.Image.Height / workArea.Height);
                }
            }

            if (types.Contains(((Button)sender).Parent.FindForm().Controls[4].Text.Substring(((Button)sender).Parent.FindForm().Controls[4].Text.IndexOf("."))))
            {
                try
                {
                    saveBitmap.Save(((Button)sender).Parent.FindForm().Controls[1].Text + "\\" + ((Button)sender).Parent.FindForm().Controls[4].Text);

                    message("Save completed!");

                    ((Button)sender).Parent.FindForm().Close();
                }
                catch (Exception ex)
                {
                    message(ex.Message);
                }
            }
            else
                message("Choose a valid file format!");

        }

        private void saveType_Changed(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Parent.FindForm().Controls[4].Text.Contains("."))
                ((RadioButton)sender).Parent.FindForm().Controls[4].Text = ((RadioButton)sender).Parent.FindForm().Controls[4].Text.Substring(0, ((RadioButton)sender).Parent.FindForm().Controls[4].Text.IndexOf('.')) + ((RadioButton)sender).Text;
            else
                ((RadioButton)sender).Parent.FindForm().Controls[4].Text += ((RadioButton)sender).Text;
        }
        #endregion

        #region IMPORT
        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region EXPORT
        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region CLOSE
        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Controls[4].Controls.Clear();
            Title = "Untitled";
            this.Text = this.Text.Substring(0, this.Text.IndexOf('-') - 1);
            initialise_workArea();
            if (selectingActive == true)
                if (selecting.IsAlive == true)
                    selectingActive = false;

            if (drag == true)
                if (dragging.IsAlive == true)
                    drag = false;

            selectionBox.Invalidate();
            workArea.Visible = false;
            enableMenu(false);
        }
        #endregion

        #region PRINT
        //ramane de vazut
        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Visible == true)
            {
                PrintDialog print = new PrintDialog();
                if (print.ShowDialog() == DialogResult.OK)
                {
                    PrintDocument pd = new PrintDocument();
                    pd.PrintPage += printPage;
                    pd.Print();
                }
            }
            else
            {
                message("There is no image to print");
            }
        }

        private void printPage(object sender, PrintPageEventArgs e)
        {

            System.Drawing.Image image = workArea.Image;

            Rectangle m = e.MarginBounds;
            double getWidth = image.Width;
            double getHeight = image.Height;
            double ratio = 0;
            ratio = getWidth / getHeight;

            if (getWidth > getHeight)
            {
                m.Height = (int)(ratio * getWidth);
            }
            else
            {
                m.Width = (int)(ratio * getHeight);
            }
            e.Graphics.DrawImage(image, m);
            e.Graphics.Dispose();
            message("Imaginea a fost printata cu succes!");
        }
        #endregion

        #region OPTIONS
        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region EXIT
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Visible == true)
            {
                Form exitForm = popForm("Exit", 250, 150);
                exitForm.Location = new Point((this.Width - exitForm.Width) / 2, (this.Height - exitForm.Height) / 2);
                Label exitLabel = popLabel(40, 25, "Do you want to save changes?", exitForm);
                Button yesButton = popButton(15, 75, "YES", exitForm);
                Button noButton = popButton(150, 75, "NO", exitForm);

                noButton.Click += new System.EventHandler(closeApp);

                yesButton.Click += new System.EventHandler(saveAsToolStripMenuItem_Click);
            }
            else
            {
                this.Close();
            }
        }

        private void closeApp(object sender, EventArgs e)
        {
            if (selectingActive == true)
                if (selecting.IsAlive == true)
                    selectingActive = false;
            if (drag == true)
                if (dragging.IsAlive == true)
                    drag = false;
            this.Close();
        }
        #endregion

        #endregion

        #region EDIT BUTTONS

        List<Control> elements = new List<Control>();
        Point movePosition = Point.Empty;
        Point moveNewPosition = Point.Empty;
        Point transformPosition = Point.Empty;
        Point location = Point.Empty;
        Size startSize = new Size();
        bool drag = false;
        bool transform = false;
        int resizingEdge = 5;
        int index = -1;

        /// <summary>
        /// Right now it doesn't anything
        /// </summary>
        /// <param name="sender">Just some sender param</param>
        /// <param name="e">Another e</param>
        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void copyAction()
        {
            createSelection();
            pasteToolStripMenuItem.Enabled = true;
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            copyAction();
        }

        private void cutAction()
        {
            pasteToolStripMenuItem.Enabled = true;
            if (selectingActive)
            {
                selectingActive = false;
                createSelection();
                Bitmap cut = new Bitmap(workArea.Image, workArea.Image.Width, workArea.Image.Height);
                Rectangle r = createRectangle(selectStart, selectEnd);

                BitmapData bitmapData = cut.LockBits(new Rectangle(0, 0, cut.Width, cut.Height), ImageLockMode.ReadWrite, cut.PixelFormat);
                int bytesPerPixel = Bitmap.GetPixelFormatSize(cut.PixelFormat) / 8;
                int byteCount = bitmapData.Stride * cut.Height;
                byte[] pixels = new byte[byteCount];
                IntPtr ptrFirstPixel = bitmapData.Scan0;
                Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
                int heightInPixels = bitmapData.Height;
                int widthInBytes = bitmapData.Width * bytesPerPixel;

                if (mod == 0)
                    for (int y = (int)(r.Y * (1.0 * workArea.Image.Height / workArea.Height)); y <= (int)((r.Y + r.Height) * (1.0 * workArea.Image.Height / workArea.Height)); y++)
                        if (y >= 0 && y < cut.Height)
                        {
                            int currentLine = y * bitmapData.Stride;
                            for (int x = (int)(r.X * (1.0 * workArea.Image.Width / workArea.Width)) * bytesPerPixel; x <= (int)((r.X + r.Width) * (1.0 * workArea.Image.Width / workArea.Width)) * bytesPerPixel; x += bytesPerPixel)
                                if (x >= 0 && x < cut.Width * bytesPerPixel)
                                {
                                    pixels[currentLine + x] = 0;
                                    pixels[currentLine + x + 1] = 0;
                                    pixels[currentLine + x + 2] = 0;
                                    pixels[currentLine + x + 3] = 0;
                                }
                        }
                if (mod == 3)
                {
                    Point scaledX = new Point((int)(selectStart.X * (1.0 * workArea.Image.Width / workArea.Width)), (int)(selectStart.Y * (1.0 * workArea.Image.Height / workArea.Height)));
                    Point scaledY = new Point((int)(selectEnd.X * (1.0 * workArea.Image.Width / workArea.Width)), (int)(selectEnd.Y * (1.0 * workArea.Image.Height / workArea.Height)));
                    Rectangle rscaled = createRectangle(scaledX, scaledY);
                    for (int y = (int)(r.Y * (1.0 * workArea.Image.Height / workArea.Height)); y <= (int)((r.Y + r.Height) * (1.0 * workArea.Image.Height / workArea.Height)); y++)
                        if (y >= 0 && y < cut.Height)
                        {
                            int currentLine = y * bitmapData.Stride;
                            for (int x = (int)(r.X * (1.0 * workArea.Image.Width / workArea.Width)) * bytesPerPixel; x <= (int)((r.X + r.Width) * (1.0 * workArea.Image.Width / workArea.Width)) * bytesPerPixel; x += bytesPerPixel)
                                if (PointInEllipse(x / bytesPerPixel, y, rscaled) && (x >= 0 && x < cut.Width * bytesPerPixel))
                                {
                                    pixels[currentLine + x] = 0;
                                    pixels[currentLine + x + 1] = 0;
                                    pixels[currentLine + x + 2] = 0;
                                    pixels[currentLine + x + 3] = 0;
                                }
                        }
                }
                Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
                cut.UnlockBits(bitmapData);

                workArea.Image = cut;
                display_update_background();

                //Bitmap cut = new Bitmap(workArea.Image, workArea.Image.Width, workArea.Image.Height);
                ////DirectBitmap cut = new DirectBitmap(new Bitmap(workArea.Image));
                //Rectangle r = createRectangle(selectStart, selectEnd);


                //if (mod == 0)
                //    for (int x = (int)(r.X * (1.0 * workArea.Image.Width / workArea.Width)); x <= (int)((r.X + r.Width) * (1.0 * workArea.Image.Width / workArea.Width)); x++)
                //        for (int y = (int)(r.Y * (1.0 * workArea.Image.Height / workArea.Height)); y <= (int)((r.Y + r.Height) * (1.0 * workArea.Image.Height / workArea.Height)); y++)
                //        {
                //            cut.SetPixel(x, y, Color.Transparent);
                //            //cut.Bits[x*cut.Bitmap.Height+y]= Color.Transparent.ToArgb();
                //            //cut.Bits[x + y * cut.Bitmap.Width] = Color.Black.ToArgb();

                //        }
                //if (mod == 3)
                //{
                //    Point scaledX = new Point((int)(selectStart.X * (1.0 * workArea.Image.Width / workArea.Width)), (int)(selectStart.Y * (1.0 * workArea.Image.Height / workArea.Height)));
                //    Point scaledY = new Point((int)(selectEnd.X * (1.0 * workArea.Image.Width / workArea.Width)), (int)(selectEnd.Y * (1.0 * workArea.Image.Height / workArea.Height)));
                //    Rectangle rscaled = createRectangle(scaledX, scaledY);
                //    for (int i = 0; i < cut.Width; i++)
                //        for (int j = 0; j < cut.Height; j++)
                //            if (PointInEllipse(i, j, rscaled))
                //                //cut.Bits[i + rscaled.X * (j + rscaled.Y - 1) + j + rscaled.Y] = Color.Transparent.ToArgb();
                //                 cut.SetPixel(i + rscaled.X, j + rscaled.Y, Color.Transparent);
                //                //cut.Bits[i + cut.Bitmap.Width * (j + rscaled.Y)] = Color.Transparent.ToArgb();

                //}
                //workArea.Image = cut;
                //cut.Dispose();
                //Bitmap cut = new Bitmap(workArea.Image, workArea.Image.Width, workArea.Image.Height);
                //Graphics g = Graphics.FromImage(cut);
                //g.FillRectangle(new SolidBrush(Color.White), selectStart.X * workArea.Image.Width / workArea.Width, selectStart.Y * workArea.Image.Height / workArea.Height, workArea.Image.Width * r.Width / workArea.Width, workArea.Image.Height * r.Height / workArea.Height);
                //workArea.Image = cut;
            }
            else
            {
                copiedImage = new Bitmap(((PictureBox)elements[index]).Image);
                display.Controls.Remove(elements[index]);
                elements[index].Dispose();
                elements.Remove(elements[index]);
                if (index > 0)
                    index--;
            }
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cutAction();
        }

        private void pasteAction()
        {
            PictureBox copy = new PictureBox();
            copy.Size = copiedImage.Size;
            copy.Location = new Point((display.Width - copy.Width) / 2, (display.Height - copy.Height) / 2);
            copy.BackColor = Color.Transparent;
            copy.Image = copiedImage;
            copy.Visible = true;
            display.Controls.Add(copy);
            copy.BringToFront();
            addElement(copy);
            selectingActive = false;
            textBoxRefresh();

            copy.MouseDown += new MouseEventHandler(move_MouseDown);
            copy.MouseMove += new MouseEventHandler(move_MouseMove);
            copy.MouseUp += new MouseEventHandler(move_MouseUp);
            copy.MouseClick += new MouseEventHandler(copy_MouseClick);
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pasteAction();
        }

        private void addElement(Control element)
        {
            elements.Add(element);
            index = elements.Count - 1;
            if (index == 0)
            {
                deleteToolStripMenuItem.Enabled = true;
                properties.Visible = true;
            }
        }

        private void deleteAction()
        {
            if (elements.Count > 0)
            {
                Control deleted = elements[index];
                display.Controls.Remove(elements[index]);
                elements.Remove(elements[index]);
                deleted.Dispose();
                this.Controls[4].Controls.Clear();
                index = elements.Count() - 1;
                if (elements.Count <= 0)
                {
                    properties.Visible = false;
                    deleteToolStripMenuItem.Enabled = false;
                }
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            deleteAction();
        }

        private void cropToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selectingActive = false;
            selectionBox.Invalidate();
            textBoxRefresh();

            createSelection();
            workArea.Image = copiedImage;
            resizePictureBox(workArea, workArea.Image);
            centerPictureBox();
            zoomCount = 0;
            toolBox.Controls[0].BackColor = idleColor;
            deactivateAll();
        }

        private void createSelection()
        {
            Rectangle r = createRectangle(selectStart, selectEnd);
            Bitmap original = new Bitmap(workArea.Image, workArea.Width, workArea.Height);
            
            copiedImage = new Bitmap(r.Width, r.Height);

            using (Graphics g = Graphics.FromImage(copiedImage))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.DrawImage(original, 0, 0, r, GraphicsUnit.Pixel);
            }

            // Method I : VS + recursive = poop
            
            //if (mod == 3)
            //    for (int i = 0; i < copiedImage.Width; i++)
            //        for (int j = 0; j < copiedImage.Height; j++)
            //            if (copiedImage.GetPixel(i, j).ToArgb() != Color.Transparent.ToArgb() && !PointInEllipse(i,j, r))
            //                createCircularSelection(ref copiedImage, i, j, r);

            //Method II : 2-3 GB RAM required
            //if (mod == 3)
            //    for (int i = 0; i < copiedImage.Width; i++)
            //        for (int j = 0; j < copiedImage.Height; j++)
            //            if (copiedImage.GetPixel(i, j).ToArgb() != Color.Transparent.ToArgb())
            //                if (!PointInEllipse(i, j, r))
            //                {
            //                    copiedImage.SetPixel(i, j, Color.Transparent);
            //                }

            //Method III : nothing wrong...yet
            if(mod==3)
            {
                TextureBrush map = new TextureBrush(copiedImage);
                Bitmap final = new Bitmap(copiedImage.Width, copiedImage.Height);
                using (Graphics gr = Graphics.FromImage(final))
                {
                    gr.FillEllipse(map, 0, 0, copiedImage.Width, copiedImage.Height);
                }
                copiedImage = final;
            }
        }



        //private void createCircularSelection(ref Bitmap input, int x, int y, Rectangle r)
        //{
        //    
        //        int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };
        //        int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };

        //        input.SetPixel(x, y, Color.Transparent);
        //     
        //        for (int i = 0; i < 8; i++)
        //        {
        //            if (x + dx[i] < input.Width && y + dy[i] < input.Height && x + dx[i] >= 0 && y + dy[i] >= 0)
        //                if (!PointInEllipse(x + dx[i], y + dy[i], r))
        //                    if (input.GetPixel(x + dx[i], y + dy[i]).ToArgb() != Color.Transparent.ToArgb())
        //                    {
        //                        createCircularSelection(ref input, x + dx[i], y + dy[i], r);
        //                    }
        //        }
        //    
        //}

        private bool PointInEllipse(int x, int y, Rectangle r)
        {
            //r.Location = new Point(0, 0);
            //using (GraphicsPath path = new GraphicsPath())
            //{
            //    path.AddEllipse(r);
            //    return path.IsVisible(x, y);
            //}

            return ((Math.Pow(Math.Abs(x-r.Width*1.0/2-r.X),2)/Math.Pow(r.Width * 1.0 / 2,2) + Math.Pow(Math.Abs(y - r.Height * 1.0 / 2 - r.Y), 2) / Math.Pow(r.Height * 1.0 / 2, 2)) < 1);
        }

        private void copy_MouseClick(object sender, MouseEventArgs e)
        {
            ContextMenu contextMenu = new ContextMenu();
            MenuItem copy = new MenuItem("Copy");
            MenuItem cut = new MenuItem("Cut");
            MenuItem delete = new MenuItem("Delete");
            MenuItem flipHorizontal = new MenuItem("Flip Horizontal");
            MenuItem flipVertical = new MenuItem("Flip Vertical");

            copy.Click += new EventHandler(copy_Click);
            cut.Click += new EventHandler(cutToolStripMenuItem_Click);
            delete.Click += new EventHandler(deleteToolStripMenuItem_Click);
            flipHorizontal.Click += new EventHandler(flipHorizontal_Click);
            flipVertical.Click += new EventHandler(flipVertical_Click);

            contextMenu.MenuItems.AddRange(new MenuItem[] { copy, cut, delete, flipHorizontal, flipVertical });
            ((PictureBox)sender).ContextMenu = contextMenu;
        }

        private void copy_Click(object sender, EventArgs e)
        {
            copiedImage = new Bitmap(((PictureBox)elements[index]).Image);
        }

        private void flipHorizontal_Click(object sender, EventArgs e)
        {
            Bitmap original = new Bitmap(((PictureBox)elements[index]).Image);
            original.RotateFlip(RotateFlipType.RotateNoneFlipX);
            ((PictureBox)elements[index]).Image = original;
        }

        private void flipVertical_Click(object sender, EventArgs e)
        {
            Bitmap original = new Bitmap(((PictureBox)elements[index]).Image);
            original.RotateFlip(RotateFlipType.RotateNoneFlipY);
            ((PictureBox)elements[index]).Image = original;
        }

        private void move_MouseDown(object sender, MouseEventArgs e)
        {
            if (elements[index].GetType().Equals(typeof(textBox)))
                ((textBox)elements[index]).LoseFocus((Control)sender);
            index = elements.IndexOf(sender as Control);
            drag = true;
            movePosition = e.Location;
            dragging = new Thread(dragPicture);
            dragging.Start();
            startSize = elements[index].Size;
            ((Control)sender).BringToFront();
            this.Controls[4].Controls.Clear();
            rectangleProperties();
        }

        private void object_Click(object sender, EventArgs e)
        {
            index = elements.IndexOf(sender as Control);
        }

        private void move_MouseMove(object sender, MouseEventArgs e)
        {
            if (((Control)sender).GetType().Equals(typeof(PictureBox)))
            {
                PictureBox copy = null;
                if (elements[index].GetType().Equals(typeof(PictureBox)))
                {
                    copy = (PictureBox)elements[index];
                    if (drag && e.Button == MouseButtons.Left)
                    {
                        if (transform)
                        {
                            transformSelection(e.Location);
                            copy.SizeMode = PictureBoxSizeMode.StretchImage;
                        }
                        else
                        {
                            moveNewPosition = Point.Empty;
                            moveNewPosition.X = e.X - movePosition.X;
                            moveNewPosition.Y = e.Y - movePosition.Y;
                            if (timerMove.ElapsedMilliseconds > 30)
                            {
                                copy.Left += moveNewPosition.X;
                                copy.Top += moveNewPosition.Y;
                            }
                        }
                    }
                }
                if (!drag)
                {
                    transformPosition = e.Location;
                    if (((PictureBox)sender).Equals(copy))
                        changeTransformCursors();
                    else
                        ((PictureBox)sender).Cursor = Cursors.Default;
                }
            }
        }

        private void transformSelection(Point location)
        {
            PictureBox copy = (PictureBox)elements[index];
            moveNewPosition = Point.Empty;
            moveNewPosition.X = location.X - movePosition.X;
            moveNewPosition.Y = location.Y - movePosition.Y;
            double ratio = 0;
            if (copy.Width > copy.Height)
            {
                if (copy.Height != 0)
                    ratio = copy.Width / copy.Height;
            }
            else
            {
                if (copy.Width != 0)
                    ratio = copy.Height / copy.Width;
            }

            //stanga sus
            if (transformPosition.X < resizingEdge && transformPosition.Y < resizingEdge)
            {
                if (Control.ModifierKeys == Keys.Shift)
                {
                    if (copy.Width > copy.Height)
                    {
                        int differenceHeight = copy.Height, differenceWidth = copy.Width;
                        copy.Height -= moveNewPosition.Y;
                        differenceHeight -= copy.Height;
                        copy.Width = (int)(copy.Height * ratio);
                        differenceWidth -= copy.Width;
                        copy.Top += differenceHeight;
                        copy.Left += differenceWidth;
                    }
                    else
                    {
                        int differenceHeight = copy.Height, differenceWidth = copy.Width;
                        copy.Width -= moveNewPosition.X;
                        differenceWidth -= copy.Width;
                        copy.Height = (int)(copy.Width * ratio);
                        differenceHeight -= copy.Height;
                        copy.Top += differenceHeight;
                        copy.Left += differenceWidth;
                    }
                }
                else
                {
                    copy.Left += moveNewPosition.X;
                    copy.Top += moveNewPosition.Y;
                    copy.Height -= moveNewPosition.Y;
                    copy.Width -= moveNewPosition.X;
                }
            }
            //dreapta jos
            else if (copy.Cursor == Cursors.SizeNWSE)
            {
                if (Control.ModifierKeys == Keys.Shift)
                {
                    if (copy.Width > copy.Height)
                    {
                        copy.Height = moveNewPosition.Y + startSize.Height;
                        copy.Width = (int)(copy.Height * ratio);
                    }
                    else
                    {
                        copy.Width = moveNewPosition.X + startSize.Width;
                        copy.Height = (int)(copy.Width * ratio);
                    }
                    
                }
                else
                {
                    copy.Height = moveNewPosition.Y + startSize.Height;
                    copy.Width = moveNewPosition.X + startSize.Width;
                }
            }
            //dreapta sus
            else if (copy.Cursor == Cursors.SizeNESW && location.X > copy.Width / 2)
            {
                if (Control.ModifierKeys == Keys.Shift)
                {
                    if (copy.Width > copy.Height)
                    {
                        int differenceHeight = copy.Height;
                        copy.Height -= moveNewPosition.Y;
                        differenceHeight -= copy.Height;
                        copy.Width = (int)(copy.Height * ratio);
                        copy.Top += differenceHeight;
                    }
                    else
                    {
                        int differenceHeight = copy.Height;
                        copy.Width = moveNewPosition.X + startSize.Width;
                        copy.Height = (int)(copy.Width * ratio);
                        differenceHeight -= copy.Height;
                        copy.Top += differenceHeight;
                    }
                }
                else
                {
                    copy.Top += moveNewPosition.Y;
                    copy.Height -= moveNewPosition.Y;
                    copy.Width = moveNewPosition.X + startSize.Width;
                }
            }
            //stanga jos
            else if (copy.Cursor == Cursors.SizeNESW)
            {
                if (Control.ModifierKeys == Keys.Shift)
                {
                    if (copy.Width > copy.Height)
                    {
                        int differenceWidth = copy.Width;
                        copy.Height = moveNewPosition.Y + startSize.Height;
                        copy.Width = (int)(copy.Height * ratio);
                        differenceWidth -= copy.Width;
                        copy.Left += differenceWidth;
                    }
                    else
                    {
                        int differenceWidth = copy.Width;
                        copy.Width -= moveNewPosition.X;
                        copy.Height = (int)(copy.Width * ratio);
                        differenceWidth -= copy.Width;
                        copy.Left += differenceWidth;
                    }
                }
                else
                {
                    copy.Left += moveNewPosition.X;
                    copy.Height = moveNewPosition.Y + startSize.Height;
                    copy.Width -= moveNewPosition.X;
                }
            }
            //stanga
            else if (transformPosition.X < resizingEdge)
            {
                copy.Left += moveNewPosition.X;
                copy.Width -= moveNewPosition.X;
            }
            //dreapta
            else if (copy.Cursor == Cursors.SizeWE)
            {
                copy.Width = moveNewPosition.X + startSize.Width;
            }
            //sus
            else if (transformPosition.Y < resizingEdge)
            {
                copy.Top += moveNewPosition.Y;
                copy.Height -= moveNewPosition.Y;
            }
            //jos
            else if (copy.Cursor == Cursors.SizeNS)
            {
                copy.Height = moveNewPosition.Y + startSize.Height;
            }
        }

        private void move_MouseUp(object sender, MouseEventArgs e)
        {
            drag = false;
            transform = false;
            movePosition = Point.Empty;
            moveNewPosition = Point.Empty;
        }

        private void changeTransformCursors()
        {
            transform = true;
            if ((transformPosition.X < resizingEdge && transformPosition.Y < resizingEdge) || (transformPosition.X > elements[index].Width - resizingEdge && transformPosition.Y > elements[index].Height - resizingEdge))
            {
                elements[index].Cursor = Cursors.SizeNWSE;
            }
            else if ((transformPosition.X > elements[index].Width - resizingEdge && transformPosition.Y < resizingEdge) || (transformPosition.X < resizingEdge && transformPosition.Y > elements[index].Height - resizingEdge))
            {
                elements[index].Cursor = Cursors.SizeNESW;
            }
            else if (transformPosition.X < resizingEdge || transformPosition.X > elements[index].Width - resizingEdge)
            {
                elements[index].Cursor = Cursors.SizeWE;
            }
            else if (transformPosition.Y < resizingEdge || transformPosition.Y > elements[index].Height - resizingEdge)
            {
                elements[index].Cursor = Cursors.SizeNS;
            }
            else
            {
                elements[index].Cursor = Cursors.SizeAll;
                transform = false;
            }
        }

        private void dragPicture()
        {
            timerMove.Start();
            do
            {
                if (timerMove.ElapsedMilliseconds > 50)
                    timerMove.Restart();


            } while (drag);
            timerMove.Reset();
        }

        #endregion

        #region EFFECTS
        private void grayscaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                grayscaleThread(workArea);
        }

        private void grayscaleThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(convertGrayscale);
            Thread convert = new Thread(th);
            if(!convert.IsAlive)
              convert.Start(picture);
        }

        delegate void threadPictureRefreshCallBack(PictureBox picture);

        private void invokeRefresh(PictureBox picture)
        {
            if (picture.InvokeRequired)
            {
                threadPictureRefreshCallBack callbk = new threadPictureRefreshCallBack(invokeRefresh);
                picture.Invoke(callbk, new object[] { picture });
            }
            else
            {
                picture.Refresh();
            }
        }

        delegate void threadPictureConvertCallBack(PictureBox picture, bool done);

        private void invokeConvertStatus(PictureBox picture, bool done)
        {
            if (picture.InvokeRequired)
            {
                threadPictureConvertCallBack callbk = new threadPictureConvertCallBack(invokeConvertStatus);
                picture.Invoke(callbk, new object[] { picture, done });
            }
            else
            {
                picture.Enabled = done;
                toolBox.Enabled = done;
            }
        }

        private void convertGrayscale(object pic)
        { 
            PictureBox picture = (PictureBox)pic;

            invokeConvertStatus(picture, false);

            Bitmap originalimg = new Bitmap(picture.Image);
            int width = 0, heigth = 0;
            width = originalimg.Width;
            heigth = originalimg.Height;

            BitmapData bitmapData = originalimg.LockBits(new Rectangle(0, 0, originalimg.Width, originalimg.Height), ImageLockMode.ReadWrite, originalimg.PixelFormat);
            int bytesPerPixel = Bitmap.GetPixelFormatSize(originalimg.PixelFormat) / 8;
            int byteCount = bitmapData.Stride * originalimg.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bitmapData.Scan0;
            Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
            int heightInPixels = bitmapData.Height;
            int widthInBytes = bitmapData.Width * bytesPerPixel;

            
                for (int y = 0;y<heightInPixels ; y++)
                {
                    int currentLine = y * bitmapData.Stride;
                    for (int x = 0;x<widthInBytes ; x += bytesPerPixel)
                    {
                        int B = pixels[currentLine + x], G = pixels[currentLine + x + 1], R = pixels[currentLine + x + 2], avg = (B + G + R) / 3;

                        pixels[currentLine + x] =(byte) avg;
                        pixels[currentLine + x + 1] = (byte)avg;
                        pixels[currentLine + x + 2] = (byte)avg;
                        
                    }
                }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;

            //for (x = 0; x < width; x++)
            //{
            //    for (y = 0; y < heigth; y++)
            //    {
            //        Color pixelColor;
            //        lock (originalimg) pixelColor = originalimg.GetPixel(x, y);
            //        A = pixelColor.A;
            //        avg = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;
            //        Color newColor = Color.FromArgb(A, avg, avg, avg);
            //        lock(originalimg) originalimg.SetPixel(x, y, newColor);
            //    }
            //    if (x % 30 == 0)
            //    {
            //        grayscale = originalimg;
            //        picture.Image = grayscale;
            //        invokeRefresh(picture);
            //    }
            //}
            invokeConvertStatus(picture, true);
        }

      
        private void redFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                redFilterThread(workArea);
        }

        private void greenFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                greenFilterThread(workArea);
        }

        private void blueFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                blueFilterThread(workArea);
        }

        private void redFilterThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(redFilter);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
                convert.Start(picture);

        }

        private void redFilter(object pic)
        {
            PictureBox picture = (PictureBox)pic;

            invokeConvertStatus(picture, false);

            Bitmap originalimg = new Bitmap(picture.Image);

            BitmapData bitmapData = originalimg.LockBits(new Rectangle(0, 0, originalimg.Width, originalimg.Height), ImageLockMode.ReadWrite, originalimg.PixelFormat);
            int bytesPerPixel = Bitmap.GetPixelFormatSize(originalimg.PixelFormat) / 8;
            int byteCount = bitmapData.Stride * originalimg.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bitmapData.Scan0;
            Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
            int heightInPixels = bitmapData.Height;
            int widthInBytes = bitmapData.Width * bytesPerPixel;


            for (int y = 0; y < heightInPixels; y++)
            {
                int currentLine = y * bitmapData.Stride;
                for (int x = 0; x < widthInBytes; x += bytesPerPixel)
                {
                    int  R = pixels[currentLine + x + 2];

                    pixels[currentLine + x] = 0;
                    pixels[currentLine + x + 1] = 0;
                    pixels[currentLine + x + 2] = (byte)R;

                }
            }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;

            invokeConvertStatus(picture, true);
        }

        private void greenFilterThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(greenFilter);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
                convert.Start(picture);
        }

        private void greenFilter(object pic)
        {
            PictureBox picture = (PictureBox)pic;

            invokeConvertStatus(picture, false);

            Bitmap originalimg = new Bitmap(picture.Image);
            BitmapData bitmapData = originalimg.LockBits(new Rectangle(0, 0, originalimg.Width, originalimg.Height), ImageLockMode.ReadWrite, originalimg.PixelFormat);
            int bytesPerPixel = Bitmap.GetPixelFormatSize(originalimg.PixelFormat) / 8;
            int byteCount = bitmapData.Stride * originalimg.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bitmapData.Scan0;
            Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
            int heightInPixels = bitmapData.Height;
            int widthInBytes = bitmapData.Width * bytesPerPixel;


            for (int y = 0; y < heightInPixels; y++)
            {
                int currentLine = y * bitmapData.Stride;
                for (int x = 0; x < widthInBytes; x += bytesPerPixel)
                {
                    int G = pixels[currentLine + x + 1];

                    pixels[currentLine + x] = 0;
                    pixels[currentLine + x + 1] = (byte)G;
                    pixels[currentLine + x + 2] = 0;

                }
            }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;
            invokeConvertStatus(picture, true);
        }

        private void blueFilterThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(blueFilter);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
                convert.Start(picture);

        }

        private void blueFilter(object pic)
        {
            PictureBox picture = (PictureBox)pic;

            invokeConvertStatus(picture, false);

            Bitmap originalimg = new Bitmap(picture.Image);
            BitmapData bitmapData = originalimg.LockBits(new Rectangle(0, 0, originalimg.Width, originalimg.Height), ImageLockMode.ReadWrite, originalimg.PixelFormat);
            int bytesPerPixel = Bitmap.GetPixelFormatSize(originalimg.PixelFormat) / 8;
            int byteCount = bitmapData.Stride * originalimg.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bitmapData.Scan0;
            Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
            int heightInPixels = bitmapData.Height;
            int widthInBytes = bitmapData.Width * bytesPerPixel;


            for (int y = 0; y < heightInPixels; y++)
            {
                int currentLine = y * bitmapData.Stride;
                for (int x = 0; x < widthInBytes; x += bytesPerPixel)
                {
                    int B = pixels[currentLine + x];

                    pixels[currentLine + x] = (byte)B;
                    pixels[currentLine + x + 1] = 0;
                    pixels[currentLine + x + 2] =0;

                }
            }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;
            invokeConvertStatus(picture, true);
        }

        private void invertColorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                invertThread(workArea);
        }

        private void invertThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(invertColors);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
                convert.Start(picture);

        }

        private void invertColors(object pic)
        {
            PictureBox picture = (PictureBox)pic;
            invokeConvertStatus(picture, false);
            Bitmap originalimg = new Bitmap(picture.Image);

            BitmapData bitmapData = originalimg.LockBits(new Rectangle(0, 0, originalimg.Width, originalimg.Height), ImageLockMode.ReadWrite, originalimg.PixelFormat);
            int bytesPerPixel = Bitmap.GetPixelFormatSize(originalimg.PixelFormat) / 8;
            int byteCount = bitmapData.Stride * originalimg.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bitmapData.Scan0;
            Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
            int heightInPixels = bitmapData.Height;
            int widthInBytes = bitmapData.Width * bytesPerPixel;


            for (int y = 0; y < heightInPixels; y++)
            {
                int currentLine = y * bitmapData.Stride;
                for (int x = 0; x < widthInBytes; x += bytesPerPixel)
                {
                    int B = pixels[currentLine + x], G = pixels[currentLine + x + 1], R = pixels[currentLine + x + 2];

                    pixels[currentLine + x] = (byte)(255-B);
                    pixels[currentLine + x + 1] = (byte)(255 - G);
                    pixels[currentLine + x + 2] = (byte)(255 - R);

                }
            }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;
            invokeConvertStatus(picture, true);
        }

        private void sepiaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                sepiaThread(workArea);
        }

        private void sepiaThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(Sepia);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
                convert.Start(picture);

        }

        private void Sepia(object pic)
        {
            PictureBox picture = (PictureBox)pic;
            invokeConvertStatus(picture, false);
            Bitmap originalimg = new Bitmap(picture.Image);
            
            BitmapData bitmapData = originalimg.LockBits(new Rectangle(0, 0, originalimg.Width, originalimg.Height), ImageLockMode.ReadWrite, originalimg.PixelFormat);
            int bytesPerPixel = Bitmap.GetPixelFormatSize(originalimg.PixelFormat) / 8;
            int byteCount = bitmapData.Stride * originalimg.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bitmapData.Scan0;
            Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
            int heightInPixels = bitmapData.Height;
            int widthInBytes = bitmapData.Width * bytesPerPixel;


            for (int y = 0; y < heightInPixels; y++)
            {
                int currentLine = y * bitmapData.Stride;
                for (int x = 0; x < widthInBytes; x += bytesPerPixel)
                {
                    int B = pixels[currentLine + x], G = pixels[currentLine + x + 1], R = pixels[currentLine + x + 2],avg=(R+G+B)/3;

                    R = B = G = avg;
                    R += 40;
                    G += 20;

                    if (R > 255) R = 255;
                    if (G > 255) G = 255;
                    if (B > 255) B = 255;

                    B -= 30;

                    if (B < 0) B = 0;
                    if (B > 255) B = 255;
                    pixels[currentLine + x] = (byte)B;
                    pixels[currentLine + x + 1] = (byte)G;
                    pixels[currentLine + x + 2] = (byte)R;

                }
            }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;

            invokeConvertStatus(picture, true);
        }

        #endregion

        #region VIEW BUTTONS

        private void fullScreenAction()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            fullScreenToolStripMenuItem.Visible = false;
            fullscrn = false;
            exitFullScreenToolStripMenuItem.Visible = true;
        }

        private void fullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fullScreenAction();
        }

        private void exitFullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.WindowState = FormWindowState.Normal;
            fullScreenToolStripMenuItem.Visible = true;
            fullscrn = true;
            exitFullScreenToolStripMenuItem.Visible = false;
        }

        private void zoomInAction()
        {
            if (zoomCount < 25)
            {
                zoomCount++;
                zoomIn();
                display_update_background();
            }
        }

        private void zoomInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zoomInAction();
        }

        private void zoomOutAction()
        {
            if (zoomCount > -15)
            {
                zoomCount--;
                zoomOut();
                display_update_background();
            }
        }

        private void zoomOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zoomOutAction();
        }

        private void fitOnScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            resizeImage(workArea.Image, display.ClientSize.Width - 120, display.ClientSize.Height - 120);
            resizePictureBox(workArea, workArea.Image);
            centerPictureBox();
            zoomCount = (int)Math.Ceiling((Math.Log(workArea.Width / originalSize.Width) / Math.Log(multiplier)));
        }

        private void resizeImage(Image image, int width, int height)
        {
            //var destRect = new Rectangle(0, 0, width, height);
            //var destImage = new Bitmap(width, height);

            //destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //using (var graphics = Graphics.FromImage(destImage))
            //{
            //    graphics.CompositingMode = CompositingMode.SourceCopy;
            //    graphics.CompositingQuality = CompositingQuality.HighQuality;
            //    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            //    graphics.SmoothingMode = SmoothingMode.HighQuality;
            //    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            //    using (var wrapMode = new ImageAttributes())
            //    {
            //        wrapMode.SetWrapMode(WrapMode.TileFlipXY);
            //        graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
            //    }
            //}
            //workArea.Size = destImage.Size;
            //workArea.Image = destImage;
            // workArea.Image = finalPic;
            workArea.Size = new Size(width, height);
            //workArea.Image.Save("D:\\wesd.png");
            //resizePictureBox(workArea, destImage);
        }

        private void zoomIn()
        {
            int x = display.AutoScrollPosition.X, y = display.AutoScrollPosition.Y;

            resizeImage(workArea.Image, (int)(originalSize.Width * Math.Pow(multiplier, zoomCount)), (int)(originalSize.Height * Math.Pow(multiplier, zoomCount)));
            centerPictureBox();
            resizePictureBox(workArea, workArea.Image);

            if (workArea.Width > display.ClientSize.Width || workArea.Height > display.ClientSize.Height)
            {
                display.AutoScrollPosition = new Point(0, 0);
            }

            if (workArea.Width > display.ClientSize.Width)
                workArea.Left = 0;

            if (workArea.Height > display.ClientSize.Height)
                workArea.Top = 0;

            display.AutoScrollPosition = new Point((int)(Math.Abs(x) * multiplier + 0.5 * (multiplier - 1) * display.Width), (int)(Math.Abs(y) * multiplier + 0.5 * (multiplier - 1) * display.Height));
        }

        private void zoomIn(Point mouse)
        {

            int scrollhorriz = display.AutoScrollPosition.X, scrollvert = display.AutoScrollPosition.Y;
            resizeImage(workArea.Image, (int)(originalSize.Width * Math.Pow(multiplier, zoomCount)), (int)(originalSize.Height * Math.Pow(multiplier, zoomCount)));
            centerPictureBox();
            resizePictureBox(workArea, workArea.Image);

            int x = mouse.X, y = mouse.Y;

            if (workArea.Width > display.ClientSize.Width || workArea.Height > display.ClientSize.Height)
            {
                display.AutoScrollPosition = new Point(0, 0);
            }

            if (workArea.Width > display.ClientSize.Width)
                workArea.Left = 0;

            if (workArea.Height > display.ClientSize.Height)
                workArea.Top = 0;

            display.AutoScrollPosition = new Point((int)(Math.Abs(x) * multiplier - x - scrollhorriz), (int)(Math.Abs(y) * multiplier - y - scrollvert));
        }

        private void zoomOut()

        {
            int x = display.AutoScrollPosition.X, y = display.AutoScrollPosition.Y;

            resizeImage(workArea.Image, (int)(originalSize.Width * Math.Pow(multiplier, zoomCount)), (int)(originalSize.Height * Math.Pow(multiplier, zoomCount)));
            centerPictureBox();
            resizePictureBox(workArea, workArea.Image);

            if (workArea.Width > display.ClientSize.Width || workArea.Height > display.ClientSize.Height)
            {
                display.AutoScrollPosition = new Point(0, 0);
            }

            if (workArea.Width > display.ClientSize.Width)
                workArea.Left = 0;

            if (workArea.Height > display.ClientSize.Height)
                workArea.Top = 0;

            display.AutoScrollPosition = new Point((int)(Math.Abs(x) / multiplier - 0.5 * (multiplier - 1) * display.Width), (int)(Math.Abs(y) / multiplier - 0.5 * (multiplier - 1) * display.Height));
        }

        private void zoomOut(Point mouse)
        {
            resizeImage(workArea.Image, (int)(originalSize.Width * Math.Pow(multiplier, zoomCount)), (int)(originalSize.Height * Math.Pow(multiplier, zoomCount)));
            centerPictureBox();
            resizePictureBox(workArea, workArea.Image);
            int x = mouse.X, y = mouse.Y;
            int scrollhorriz = display.AutoScrollPosition.X, scrollvert = display.AutoScrollPosition.Y;
            if (workArea.Width > display.ClientSize.Width || workArea.Height > display.ClientSize.Height)
            {
                display.AutoScrollPosition = new Point(0, 0);
            }

            if (workArea.Width > display.ClientSize.Width)
                workArea.Left = 0;

            if (workArea.Height > display.ClientSize.Height)
                workArea.Top = 0;

            display.AutoScrollPosition = new Point((int)(Math.Abs(x) / multiplier - x - scrollhorriz), (int)(Math.Abs(y) / multiplier - y - scrollvert));
        }

        #endregion

        #region SELECT BUTTONS

        private void allAction()
        {
            selectingActive = false;
            selecting = new Thread(drawSelectionThread);
            selectStart = new Point(0, 0);
            selectEnd = new Point(workArea.Width-1, workArea.Height-1);
            selectionBox.Invalidate();
            textBoxRefresh();
            selecting.Start();
        }

        private void allToolStripMenuItem_Click(object sender, EventArgs e)
        {
            allAction();
        }

        private void deselectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (selectingActive == true)
                selectingActive = false;
            selectionBox.Invalidate();
            textBoxRefresh();
        }

        private void inverseSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }


        #endregion

        private void Form1_Resize(object sender, EventArgs e)
        {
            display.Size = new Size(this.Width - 340, this.Height - 125);
            properties.Location = new Point(display.Location.X + display.Width + 5, properties.Location.Y);
        }

        private void panel1_Resize(object sender, EventArgs e)
        {
            int xi = Math.Abs(display.AutoScrollPosition.X), yi = Math.Abs(display.AutoScrollPosition.Y);

            centerPictureBox();
            toolBar.Location = new Point(display.Left, display.Top + display.Height + 5);
            toolBox.Height = display.ClientSize.Height;

            if (workArea.Width > display.ClientSize.Width || workArea.Height > display.ClientSize.Height)
            {
                display.AutoScrollPosition = new Point(0, 0);
            }

            if (workArea.Width > display.ClientSize.Width)
                workArea.Left = 0;

            if (workArea.Height > display.ClientSize.Height)
                workArea.Top = 0;

            display.AutoScrollPosition = new Point(xi, yi);
        }

        private void display_update_background()
        {
            Bitmap copy = new Bitmap(display.Width, display.Height);
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.DrawImage(workArea.Image, createRectangle(workArea.Location, new Point(workArea.Location.X + workArea.Width, workArea.Location.Y + workArea.Height)));
                //g.DrawImage(workArea.Image, workArea.Location);
                //g.DrawImage(workArea.Image, new Point[] { workArea.Location, new Point(workArea.Location.X + workArea.Width, workArea.Location.Y), new Point(workArea.Location.X, workArea.Location.Y + workArea.Height), new Point(workArea.Location.X + workArea.Width, workArea.Location.Y + workArea.Height) });
               display.BackgroundImage = copy;
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (selectingActive == true)
                if (selecting.IsAlive == true)
                    selectingActive = false;

            if (drag == true)
                if (dragging.IsAlive == true)
                    drag = false;
        }

        private Button popButton(int x, int y, Control f)
        {
            Button bt = new Button();
            bt.Location = new Point(x, y);
            bt.Padding = new Padding(5, 0, 5, 0);
            bt.TabStop = false;
            bt.FlatStyle = FlatStyle.Flat;
            bt.Size = new Size(33, 33);
            bt.FlatAppearance.BorderSize = 0;
            bt.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
            bt.MouseEnter += new EventHandler(tool_MouseEnter);
            bt.MouseLeave += new EventHandler(tool_MouseLeave);
            bt.BackColor = idleColor;
            f.Controls.Add(bt);
            return bt;
        }

        private void tool_MouseEnter(object sender, EventArgs e)
        {
            if (((Button)sender).Parent == toolBox)
            {
                if (((Button)sender).BackColor != activeColor)
                    ((Button)sender).BackColor = hoverColor;
                else
                    ((Button)sender).BackColor = activeColor;
            }
            else
                ((Button)sender).BackColor = hoverColor;
        }

        private void tool_MouseLeave(object sender, EventArgs e)
        {
            if (((Button)sender).Parent == toolBox)
            {
                if (((Button)sender).BackColor != activeColor)
                    ((Button)sender).BackColor = idleColor;
                else
                    ((Button)sender).BackColor = activeColor;
            }
            else
                ((Button)sender).BackColor = idleColor;
        }

        private void toolBar_Reset()
        {
            foreach (Button bt in toolBar.Controls)
            {
                toolBar.Controls.Remove(bt);
                bt.Dispose();
            }
            toolBar.Controls.Clear();
        }

        private void ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolBar.Visible = true;
            int i = 0;
            toolBar_Reset();
            activeToolBar = menuStrip1.Items.IndexOf((ToolStripItem)sender);
            switch (activeToolBar)
            {
                case 0:
                    {
                        Button New = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(New, "New");
                        New.Click += new EventHandler(newToolStripMenuItem_Click);
                        New.BackgroundImage = Resources.New;
                        i++;

                        Button open = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(open, "Open");
                        open.Click += new EventHandler(openToolStripMenuItem_Click);
                        open.BackgroundImage = Resources.Open;
                        i++;

                        //Button save = popButton(i * 35, 0, toolBar);
                        //toolTip.SetToolTip(save, "Save");
                        //save.Click += new EventHandler(saveToolStripMenuItem1_Click);
                        //save.Enabled = saveAsToolStripMenuItem.Enabled;
                        //if (save.Enabled)
                        //    save.BackgroundImage = Resources.Save_active;
                        //else
                        //    save.BackgroundImage = Resources.Save_inactive;
                        //i++;

                        Button saveas = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(saveas, "Save As");
                        saveas.Click += new EventHandler(saveAsToolStripMenuItem_Click);
                        saveas.Enabled = saveAsToolStripMenuItem.Enabled;
                        if (saveas.Enabled)
                            saveas.BackgroundImage = Resources.SaveAs_active;
                        else
                            saveas.BackgroundImage = Resources.SaveAs_inactive;
                        i++;

                        Button close = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(close, "Close");
                        close.Click += new EventHandler(closeToolStripMenuItem_Click);
                        close.Enabled = closeToolStripMenuItem.Enabled;
                        if (close.Enabled)
                            close.BackgroundImage = Resources.Close_active;
                        else
                            close.BackgroundImage = Resources.Close_inactive;
                        i++;

                        //Button print = popButton(i * 35, 0, toolBar);
                        //toolTip.SetToolTip(print, "Print");
                        //print.Click += new EventHandler(printToolStripMenuItem_Click);
                        //print.Enabled = printToolStripMenuItem.Enabled;
                        //if (print.Enabled)
                        //    print.BackgroundImage = Resources.Print_active;
                        //else
                        //    print.BackgroundImage = Resources.Print_inactive;
                        //i++;

                        Button exit = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(exit, "Exit");
                        exit.Click += new EventHandler(exitToolStripMenuItem_Click);
                        exit.BackgroundImage = Resources.Exit;

                        break;
                    }
                case 1:
                    {
                        Button copy = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(copy, "Copy");
                        copy.Click += new EventHandler(copyToolStripMenuItem_Click);
                        copy.Enabled = copyToolStripMenuItem.Enabled;
                        if (copy.Enabled)
                            copy.BackgroundImage = Resources.Copy_active;
                        else
                            copy.BackgroundImage = Resources.Copy_inactive;
                        i++;

                        Button paste = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(paste, "Paste");
                        paste.Click += new EventHandler(pasteToolStripMenuItem_Click);
                        paste.Enabled = pasteToolStripMenuItem.Enabled;
                        if (paste.Enabled)
                            paste.BackgroundImage = Resources.Paste_active;
                        else
                            paste.BackgroundImage = Resources.Paste_inactive;
                        i++;

                        Button delete = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(delete, "Delete");
                        delete.Click += new EventHandler(deleteToolStripMenuItem_Click);
                        delete.Enabled = deleteToolStripMenuItem.Enabled;
                        if (delete.Enabled)
                            delete.BackgroundImage = Resources.Delete_active;
                        else
                            delete.BackgroundImage = Resources.Delete_inactive;
                        i++;

                        Button crop = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(crop, "Crop");
                        crop.Click += new EventHandler(cropToolStripMenuItem_Click);
                        crop.Enabled = cropToolStripMenuItem.Enabled;
                        if (crop.Enabled)
                            crop.BackgroundImage = Resources.Crop_active;
                        else
                            crop.BackgroundImage = Resources.Crop_inactive;
                        break;
                    }
                case 2:
                    {
                        Button grayscale = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(grayscale, "Grayscale");
                        grayscale.Click += new EventHandler(grayscaleToolStripMenuItem_Click);
                        grayscale.Enabled = grayscaleToolStripMenuItem.Enabled;
                        grayscale.BackgroundImage = Resources.Grayscale;
                        i++;

                        Button sepia = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(sepia, "Sepia");
                        sepia.Click += new EventHandler(sepiaToolStripMenuItem_Click);
                        sepia.Enabled = sepiaToolStripMenuItem.Enabled;
                        if (sepia.Enabled)
                            sepia.BackgroundImage = Resources.Sepia_active;
                        else
                            sepia.BackgroundImage = Resources.Grayscale;
                        i++;

                        Button red = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(red, "Red Filter");
                        red.Click += new EventHandler(redFilterToolStripMenuItem_Click);
                        red.Enabled = redFilterToolStripMenuItem.Enabled;
                        if (red.Enabled)
                            red.BackgroundImage = Resources.RedFilter_active;
                        else
                            red.BackgroundImage = Resources.Grayscale;
                        i++;

                        Button green = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(green, "Green Filter");
                        green.Click += new EventHandler(greenFilterToolStripMenuItem_Click);
                        green.Enabled = greenFilterToolStripMenuItem.Enabled;
                        if (green.Enabled)
                            green.BackgroundImage = Resources.GreenFilter_active;
                        else
                            green.BackgroundImage = Resources.Grayscale;
                        i++;

                        Button blue = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(blue, "Blue Filter");
                        blue.Click += new EventHandler(blueFilterToolStripMenuItem_Click);
                        blue.Enabled = blueFilterToolStripMenuItem.Enabled;
                        if (blue.Enabled)
                            blue.BackgroundImage = Resources.BlueFilter_active;
                        else
                            blue.BackgroundImage = Resources.Grayscale;
                        i++;

                        Button invert = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(invert, "Invert Colors");
                        invert.Click += new EventHandler(invertColorsToolStripMenuItem_Click);
                        invert.Enabled = invertColorsToolStripMenuItem.Enabled;
                        if (invert.Enabled)
                            invert.BackgroundImage = Resources.InvertColors;
                        else
                            invert.BackgroundImage = Resources.Grayscale;
                        i++;

                        Button original = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(original, "Revert to Original");
                        original.Click += new EventHandler(revertToOriginalToolStripMenuItem_Click);
                        original.Enabled = revertToOriginalToolStripMenuItem.Enabled;
                        if (original.Enabled)
                            original.BackgroundImage = Resources.RevertToOriginal_active;
                        else
                            original.BackgroundImage = Resources.RevertToOriginal_inactive;
                        break;
                    }
                case 3:
                    {
                        Button fullscreen = popButton(i * 35, 0, toolBar);
                        fullscrn = fullScreenToolStripMenuItem.Visible;
                        if (fullScreenToolStripMenuItem.Visible == true)
                        {
                            fullscreen.Click += new EventHandler(fullScreenToolStripMenuItem_Click);
                            fullscreen.BackgroundImage = Resources.FullScreen;
                            toolTip.SetToolTip(fullscreen, "Full Screen");
                        }
                        else
                        {
                            fullscreen.Click += new EventHandler(exitFullScreenToolStripMenuItem_Click);
                            fullscreen.BackgroundImage = Resources.ExitFullScreen;
                            toolTip.SetToolTip(fullscreen, "Exit Full Screen");
                        }

                        i++;

                        Button zoomin = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(zoomin, "Zoom In");
                        zoomin.Click += new EventHandler(zoomInToolStripMenuItem_Click);
                        zoomin.Enabled = zoomInToolStripMenuItem.Enabled;
                        if (zoomin.Enabled)
                            zoomin.BackgroundImage = Resources.ZoomIn_active;
                        else
                            zoomin.BackgroundImage = Resources.ZoomIn_inactive;
                        i++;

                        Button zoomout = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(zoomout, "Zoom Out");
                        zoomout.Click += new EventHandler(zoomOutToolStripMenuItem_Click);
                        zoomout.Enabled = zoomOutToolStripMenuItem.Enabled;
                        if (zoomout.Enabled)
                            zoomout.BackgroundImage = Resources.ZoomOut_active;
                        else
                            zoomout.BackgroundImage = Resources.ZoomOut_inactive;
                        i++;

                        Button fit = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(fit, "Fit On Screen");
                        fit.Click += new EventHandler(fitOnScreenToolStripMenuItem_Click);
                        fit.Enabled = fitOnScreenToolStripMenuItem.Enabled;
                        if (fit.Enabled)
                            fit.BackgroundImage = Resources.FitOnScreen_active;
                        else
                            fit.BackgroundImage = Resources.FitOnScreen_inactive;

                        break;
                    }

                case 4:
                    {
                        Button selectAll = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(selectAll, "Select All");
                        selectAll.Click += new EventHandler(allToolStripMenuItem_Click);
                        selectAll.Enabled = allToolStripMenuItem.Enabled;
                        if (selectAll.Enabled)
                            selectAll.BackgroundImage = Resources.SelectAll_active;
                        else
                            selectAll.BackgroundImage = Resources.SelectAll_inactive;
                        i++;

                        Button deselect = popButton(i * 35, 0, toolBar);
                        toolTip.SetToolTip(deselect, "Deselect");
                        deselect.Click += new EventHandler(deselectToolStripMenuItem_Click);
                        deselect.Enabled = deselectToolStripMenuItem.Enabled;
                        if (deselect.Enabled)
                            deselect.BackgroundImage = Resources.Deselect_active;
                        else
                            deselect.BackgroundImage = Resources.Deselect_inactive;
                        break;
                    }
            }
        }

        private void saveToolStripMenuItem1_EnabledChanged(object sender, EventArgs e)
        {
            //if (activeToolBar == 0)
            //{
            //    toolBar.Controls[2].Enabled = saveToolStripMenuItem1.Enabled;
            //    if (toolBar.Controls[2].Enabled)
            //        toolBar.Controls[2].BackgroundImage = Resources.Save_active;
            //    else
            //        toolBar.Controls[2].BackgroundImage = Resources.Save_inactive;
            //}
        }

        private void saveAsToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 0)
            {
                toolBar.Controls[2].Enabled = saveAsToolStripMenuItem.Enabled;
                if (toolBar.Controls[2].Enabled)
                    toolBar.Controls[2].BackgroundImage = Resources.SaveAs_active;
                else
                    toolBar.Controls[2].BackgroundImage = Resources.SaveAs_inactive;
            }
        }

        private void closeToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 0)
            {
                toolBar.Controls[3].Enabled = closeToolStripMenuItem.Enabled;
                if (toolBar.Controls[3].Enabled)
                    toolBar.Controls[3].BackgroundImage = Resources.Close_active;
                else
                    toolBar.Controls[3].BackgroundImage = Resources.Close_inactive;
            }
        }

        private void printToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            //if (activeToolBar == 0)
            //{
            //    toolBar.Controls[5].Enabled = printToolStripMenuItem.Enabled;
            //    if (toolBar.Controls[5].Enabled)
            //        toolBar.Controls[5].BackgroundImage = Resources.Print_active;
            //    else
            //        toolBar.Controls[5].BackgroundImage = Resources.Print_inactive;
            //}
        }

        private void copyToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 1)
            {
                toolBar.Controls[0].Enabled = copyToolStripMenuItem.Enabled;
                if (toolBar.Controls[0].Enabled)
                    toolBar.Controls[0].BackgroundImage = Resources.Copy_active;
                else
                    toolBar.Controls[0].BackgroundImage = Resources.Copy_inactive;
            }
        }

        private void cutToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            //    if (activeToolBar == 1)
            //    {
            //        toolBar.Controls[0].Enabled = cutToolStripMenuItem.Enabled;
            //        if (toolBar.Controls[0].Enabled)
            //            toolBar.Controls[0].BackgroundImage = Resources.Cut;
            //        else
            //            toolBar.Controls[0].BackgroundImage = Resources.Cut;
            //    }
        }

        private void pasteToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 1)
            {
                toolBar.Controls[1].Enabled = pasteToolStripMenuItem.Enabled;
                if (toolBar.Controls[1].Enabled)
                    toolBar.Controls[1].BackgroundImage = Resources.Paste_active;
                else
                    toolBar.Controls[1].BackgroundImage = Resources.Paste_inactive;
            }
        }

        private void deleteToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 1)
            {
                toolBar.Controls[2].Enabled = deleteToolStripMenuItem.Enabled;
                if (toolBar.Controls[2].Enabled)
                    toolBar.Controls[2].BackgroundImage = Resources.Delete_active;
                else
                    toolBar.Controls[2].BackgroundImage = Resources.Delete_inactive;
            }
        }

        private void cropToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 1)
            {
                toolBar.Controls[3].Enabled = cropToolStripMenuItem.Enabled;
                if (toolBar.Controls[3].Enabled)
                    toolBar.Controls[3].BackgroundImage = Resources.Crop_active;
                else
                    toolBar.Controls[3].BackgroundImage = Resources.Crop_inactive;
            }
        }

        private void exitfullScreenToolStripMenuItem_VisibleChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 2)
            {
                if (fullscrn)
                {
                    toolBar.Controls[0].Click -= exitFullScreenToolStripMenuItem_Click;
                    toolBar.Controls[0].Click += fullScreenToolStripMenuItem_Click;
                    toolBar.Controls[0].BackgroundImage = Resources.FullScreen;
                }
                else
                {
                    toolBar.Controls[0].Click -= fullScreenToolStripMenuItem_Click;
                    toolBar.Controls[0].Click += exitFullScreenToolStripMenuItem_Click;
                    toolBar.Controls[0].BackgroundImage = Resources.ExitFullScreen;
                }
            }
        }

        private void allToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 3)
            {
                toolBar.Controls[0].Enabled = allToolStripMenuItem.Enabled;
                if (toolBar.Controls[0].Enabled)
                    toolBar.Controls[0].BackgroundImage = Resources.SelectAll_active;
                else
                    toolBar.Controls[0].BackgroundImage = Resources.SelectAll_inactive;
            }
        }

        private void revertToOriginalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                workArea.Image = original;
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            reviewForm();
        }

        private void deselectToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 3)
            {
                toolBar.Controls[1].Enabled = deselectToolStripMenuItem.Enabled;
                if (toolBar.Controls[1].Enabled)
                    toolBar.Controls[1].BackgroundImage = Resources.Deselect_active;
                else
                    toolBar.Controls[1].BackgroundImage = Resources.Deselect_inactive;
            }
        }

        #region SHORTCUTS

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.N)
                newAction();
            if (e.Control && e.KeyCode == Keys.O)
                openAction();
            if (e.Control && e.KeyCode == Keys.Shift && e.KeyCode == Keys.S)
                saveAsAction();               
            if (e.KeyData == Keys.Delete)
                deleteAction();
            if (e.Control && e.KeyCode == Keys.C)
                if (copyToolStripMenuItem.Enabled)
                    if(mod==0)
                        copyAction();
                    else
                    {
                        copiedImage = new Bitmap(((PictureBox)elements[index]).Image);
                    }
            if (e.Control && e.KeyCode == Keys.X)
                if (cutToolStripMenuItem.Enabled)
                    cutAction();
            if (e.Control && e.KeyCode == Keys.V)
                if (pasteToolStripMenuItem.Enabled)
                    pasteAction();
            if (e.Control && e.KeyCode == Keys.F)
                fullScreenAction();
            if (e.Control && e.KeyCode == Keys.Oemplus)
                zoomInAction();
            if (e.Control && e.KeyCode == Keys.OemMinus)
                zoomOutAction();
            if (e.Control && e.KeyCode == Keys.A)
                allAction();
        }

        #endregion
    }

    

    public class displayPanel : Panel
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            //base.OnMouseWheel(e);
        }
    }


    #region SHAPES

    public class Shape:PictureBox
    {
        int resizingEdge = 8;
        
        public bool raiseEvents = true;

        protected Point initialPosition = Point.Empty;
        protected Point finalPosition = Point.Empty;
        protected Point moveNewPosition = Point.Empty;

        protected Size initialSize = Size.Empty;
        protected Size finalSize = Size.Empty;

        protected Stopwatch timerMove = new Stopwatch();
        protected Thread draggingShape;

        protected bool dragShape;
        protected bool transformShape;

        public Shape(Point start, Point end,Control parent)
        {
            Rectangle drawingLimits = createRectangle(start, end);
            parent.Controls.Add(this);
            this.BringToFront();
            this.Size = new Size(drawingLimits.Width, drawingLimits.Height);
            this.Location = new Point(drawingLimits.X + parent.Controls[parent.Controls.Count-1].Left, drawingLimits.Y + parent.Controls[parent.Controls.Count - 1].Top);

            this.MouseDown += new MouseEventHandler(move_MouseDown);
            this.MouseMove += new MouseEventHandler(move_MouseMove);
            this.MouseUp += new MouseEventHandler(move_MouseUp);
        }

        private void move_MouseUp(object sender, MouseEventArgs e)
        {
            dragShape = false;
            transformShape = false;
            initialPosition = Point.Empty;
            moveNewPosition = Point.Empty;
        }

        private void move_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragShape)
            {
                finalPosition = e.Location;
                changeTransformCursors((Shape)sender);
            }
            if (dragShape && e.Button == MouseButtons.Left)
            {
                if (transformShape&&((Control)sender).Focused)
                {
                    transformSelection(e.Location, ((Shape)sender));
                }
                else
                {
                    moveNewPosition = Point.Empty;
                    moveNewPosition.X = e.X - initialPosition.X;
                    moveNewPosition.Y = e.Y - initialPosition.Y;
                    if (timerMove.ElapsedMilliseconds > 30)
                    {
                        ((Shape)sender).Left += moveNewPosition.X;
                        ((Shape)sender).Top += moveNewPosition.Y;
                    }
                }

            }
        }

        private void changeTransformCursors(Shape sender)
        {
            transformShape = true;
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
                transformShape = false;
            }
        }

        private void transformSelection(Point location, Shape sender)
        {
            moveNewPosition = Point.Empty;
            moveNewPosition.X = location.X - initialPosition.X;
            moveNewPosition.Y = location.Y - initialPosition.Y;

            //stanga sus
            if (finalPosition.X < resizingEdge && finalPosition.Y < resizingEdge)
            {
                sender.Left += moveNewPosition.X;
                sender.Top += moveNewPosition.Y;
                sender.Height -= moveNewPosition.Y;
                sender.Width -= moveNewPosition.X;
            }
            //dreapta jos
            else if (sender.Cursor == Cursors.SizeNWSE)
            {
                sender.Height = moveNewPosition.Y + initialSize.Height;
                sender.Width = moveNewPosition.X + initialSize.Width;
            }
            //dreapta sus
            else if (sender.Cursor == Cursors.SizeNESW && location.X > sender.Width / 2)
            {
                sender.Top += moveNewPosition.Y;
                sender.Height -= moveNewPosition.Y;
                sender.Width = moveNewPosition.X + initialSize.Width;
            }
            //stanga jos
            else if (sender.Cursor == Cursors.SizeNESW)
            {
                sender.Left += moveNewPosition.X;
                sender.Height = moveNewPosition.Y + initialSize.Height;
                sender.Width -= moveNewPosition.X;
            }
            //stanga
            else if (finalPosition.X < resizingEdge)
            {
                sender.Left += moveNewPosition.X;
                sender.Width -= moveNewPosition.X;
            }
            //dreapta
            else if (sender.Cursor == Cursors.SizeWE)
            {
                sender.Width = moveNewPosition.X + initialSize.Width;
            }
            //sus
            else if (finalPosition.Y < resizingEdge)
            {
                sender.Top += moveNewPosition.Y;
                sender.Height -= moveNewPosition.Y;
            }
            //jos
            else if (sender.Cursor == Cursors.SizeNS)
            {
                sender.Height = moveNewPosition.Y + initialSize.Height;
            }
        }

        private void move_MouseDown(object sender, MouseEventArgs e)
        {
            ((Control)sender).Focus();
            dragShape = true;
            initialPosition = e.Location;
            draggingShape = new Thread(dragPicture);
            draggingShape.Start();
            initialSize = ((Shape)sender).Size;
            ((Control)sender).BringToFront();
           // ((Shape)sender).BringToFront();
        }

        private void dragPicture()
        {
            timerMove.Start();
            do
            {
                if (timerMove.ElapsedMilliseconds > 50)
                    timerMove.Restart();
            } while (dragShape);
            timerMove.Reset();
        }

        private Rectangle createRectangle(Point start, Point end)
        {
            Rectangle r = new Rectangle();

            if (start.X < end.X)
            {
                r.X = start.X;
                r.Width = end.X - start.X;
            }
            else
            {
                r.X = end.X;
                r.Width = start.X - end.X;
            }
            if (start.Y < end.Y)
            {
                r.Y = start.Y;
                r.Height = end.Y - start.Y;
            }
            else
            {
                r.Y = end.Y;
                r.Height = start.Y - end.Y;
            }
            return r;
        }

        protected void message(string x)
        {
            MessageBox.Show(x);
        }

    }

    #region RECTANGLE
    public class DrawRectangle : Shape
    {
        public DrawRectangle(Point start, Point end, Control parent): base(start,end,parent)
        {
            setRectangleColor(Color.Black, this);
            rectangleProperties(this.FindForm().Controls[4], this);

            this.MouseDown += new MouseEventHandler(rectangle_MouseClick);
            this.SizeChanged += new EventHandler(rectangle_SizeChanged);
        }

        private void rectangle_SizeChanged(object sender, EventArgs e)
        {
            //schimba height
            this.FindForm().Controls[4].Controls[1].Text = this.Height.ToString();
            //schimba width
            this.FindForm().Controls[4].Controls[3].Text = this.Width.ToString();
        }

        private void rectangle_MouseClick(object sender, MouseEventArgs e)
        {
            ((Control)sender).Focus();
            Control parent = ((DrawRectangle)sender).FindForm().Controls[4];
            parent.Controls.Clear();
            rectangleProperties(parent, (Control)sender);
        }

        private void rectangleProperties(Control parent, Control sender)
        {
            Label height = new Label();
            height.Size = new Size(50, 30);
            height.Text = "Height:";
            parent.Controls.Add(height);

            TextBox heightText = new TextBox();
            heightText.Size = new Size(50, 30);
            heightText.Text = sender.Height.ToString();
            heightText.Location = new Point(height.Location.X + height.Width + 5, height.Location.Y);
            heightText.TextChanged += new EventHandler(heightText_TextChanged);
            parent.Controls.Add(heightText);

            Label width = new Label();
            width.Size = new Size(50, 30);
            width.Location = new Point(height.Location.X, height.Location.Y + height.Height + 5);
            width.Text = "Width:";
            parent.Controls.Add(width);

            TextBox widthText = new TextBox();
            widthText.Size = new Size(50, 30);
            widthText.Text = sender.Width.ToString();
            widthText.Location = new Point(width.Location.X + width.Width + 5, width.Height);
            widthText.TextChanged += new EventHandler(widthText_TextChanged);
            parent.Controls.Add(widthText);

            noFocusBorderButton colorSwitch = new noFocusBorderButton();
            parent.Controls.Add(colorSwitch);
            colorSwitch.BackColor = ((PictureBox)sender).BackColor;
            colorSwitch.Size = new Size(30, 30);
            colorSwitch.Location = new Point(width.Location.X, width.Location.Y + width.Height + 5);
            colorSwitch.Click += new EventHandler(color_Clicked);
        }

        private void heightText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                this.Height = Convert.ToInt32(((TextBox)sender).Text);
            }
            catch (Exception)
            {
                message("Invalid height! Try again.");
            }
        }

        private void widthText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                this.Width = Convert.ToInt32(((TextBox)sender).Text);
            }
            catch (Exception)
            {
                message("Invalid width! Try again.");
            }
        }

        private void color_Clicked(object sender, EventArgs e)
        {
            ColorDialog colorPicker = new ColorDialog();
            if (colorPicker.ShowDialog() == DialogResult.OK)
            {
                ((Button)sender).BackColor = colorPicker.Color;
                setRectangleColor(colorPicker.Color, this);

            }
        }

        private void setRectangleColor(Color col, PictureBox rectangle)
        {
            Bitmap bmp = new Bitmap(rectangle.Width, rectangle.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            using (SolidBrush brush = new SolidBrush(col))
            {
                g.FillRectangle(brush, 0, 0, rectangle.Width, rectangle.Height);
                rectangle.BackColor = col;
            }
            rectangle.Image = bmp;
        }
        
    }
    #endregion

    #region ELLIPSE

    public class DrawEllipse:Shape
    {
        Color color=Color.Black;
        public DrawEllipse(Point start,Point end, Control parent):base(start,end,parent)
            {
            setEllipseColor(Color.Black, this);
            ellipseProperties(this.FindForm().Controls[4], this);

            this.MouseDown += new MouseEventHandler(rectangle_MouseClick);
            this.SizeChanged += new EventHandler(rectangle_SizeChanged);
        }

        private void rectangle_SizeChanged(object sender, EventArgs e)
        {
            //schimba height
            this.FindForm().Controls[4].Controls[1].Text = this.Height.ToString();
            //schimba width
            this.FindForm().Controls[4].Controls[3].Text = this.Width.ToString();
        }

        private void rectangle_MouseClick(object sender, MouseEventArgs e)
        {
            ((Control)sender).Focus();
            Control parent = ((DrawEllipse)sender).FindForm().Controls[4];
            parent.Controls.Clear();
            ellipseProperties(parent, (Control)sender);
        }

        private void ellipseProperties(Control parent, Control sender)
        {
            Label height = new Label();
            height.Size = new Size(50, 30);
            height.Text = "Height:";
            parent.Controls.Add(height);

            TextBox heightText = new TextBox();
            heightText.Size = new Size(50, 30);
            heightText.Text = sender.Height.ToString();
            heightText.Location = new Point(height.Location.X + height.Width + 5, height.Location.Y);
            heightText.TextChanged += new EventHandler(heightText_TextChanged);
            parent.Controls.Add(heightText);

            Label width = new Label();
            width.Size = new Size(50, 30);
            width.Location = new Point(height.Location.X, height.Location.Y + height.Height + 5);
            width.Text = "Width:";
            parent.Controls.Add(width);

            TextBox widthText = new TextBox();
            widthText.Size = new Size(50, 30);
            widthText.Text = sender.Width.ToString();
            widthText.Location = new Point(width.Location.X + width.Width + 5, width.Height);
            widthText.TextChanged += new EventHandler(widthText_TextChanged);
            parent.Controls.Add(widthText);

            noFocusBorderButton colorSwitch = new noFocusBorderButton();
            parent.Controls.Add(colorSwitch);
            colorSwitch.BackColor = color;
            colorSwitch.Size = new Size(30, 30);
            colorSwitch.Location = new Point(width.Location.X, width.Location.Y + width.Height + 5);
            colorSwitch.Click += new EventHandler(color_Clicked);
        }

        private void heightText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                this.Height = Convert.ToInt32(((TextBox)sender).Text);
                setEllipseColor(color, this);
            }
            catch (Exception)
            {
                message("Invalid height! Try again.");
            }
        }

        private void widthText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                this.Width = Convert.ToInt32(((TextBox)sender).Text);
                setEllipseColor(color, this);
            }
            catch (Exception)
            {
                message("Invalid width! Try again.");
            }
        }

        private void color_Clicked(object sender, EventArgs e)
        {
            ColorDialog colorPicker = new ColorDialog();
            if (colorPicker.ShowDialog() == DialogResult.OK)
            {
                ((Button)sender).BackColor = colorPicker.Color;
                setEllipseColor(colorPicker.Color, this);
                color = colorPicker.Color;
            }
        }

        private void setEllipseColor(Color col, PictureBox rectangle)
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

    #endregion

    #endregion

    #region TEXTBOX
    public class textBox : RichTextBox
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

        public textBox(Point location, Size size, Control parent)
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.Opaque, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            this.TextChanged += textBox_TextChanged;
            this.BackColor = Color.Transparent;
            this.ScrollBars = RichTextBoxScrollBars.None;
            parent.Controls.Add(this);
            this.Location = location;
            this.Size = size;
            this.BringToFront();
            this.MouseDown += textBox_MouseDown;
            this.MouseMove += textBox_MouseMove;
            this.MouseClick += textBox_Click;
            this.MouseUp += textBox_MouseUp;
            this.GotFocus += textBox_GotFocus;
        }

        public textBox(Rectangle box, Control parent)
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.Opaque, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            this.TextChanged += textBox_TextChanged;
            this.BackColor = Color.Transparent;
            this.ScrollBars = RichTextBoxScrollBars.None;
            parent.Controls.Add(this);
            this.Location = box.Location;
            this.Size = box.Size;
            this.BringToFront();
            this.MouseDown += textBox_MouseDown;
            this.MouseMove += textBox_MouseMove;
            this.MouseClick += textBox_Click;
            this.MouseUp += textBox_MouseUp;
            this.GotFocus += textBox_GotFocus;
            this.LostFocus += textBox_LostFocus;
        }

        private void textBox_TextChanged(object sender, EventArgs e)
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

        private void textBox_MouseDown(object sender, MouseEventArgs e)
        {
            dragTextBox = true;
            initialPosition = e.Location;
            draggingText = new Thread(dragText);
            draggingText.Start();
            initialSize = ((textBox)sender).Size;
            ((textBox)sender).BringToFront();
        }

        private void textBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragTextBox)
            {
                finalPosition = e.Location;
                //if (((PictureBox)sender).Equals(copy))
                changeTransformCursors((textBox)sender);
                //else
                //    ((PictureBox)sender).Cursor = Cursors.Default;
            }
            if (dragTextBox && e.Button == MouseButtons.Left)
            {
                if (transformTextBox)
                {
                    transformSelection(e.Location, ((textBox)sender));
                }
                else
                {
                    moveNewPosition = Point.Empty;
                    moveNewPosition.X = e.X - initialPosition.X;
                    moveNewPosition.Y = e.Y - initialPosition.Y;
                    if (timerMove.ElapsedMilliseconds > 30)
                    {
                        ((textBox)sender).Left += moveNewPosition.X;
                        ((textBox)sender).Top += moveNewPosition.Y;
                    }
                }

            }
        }

        private void textBox_MouseUp(object sender, MouseEventArgs e)
        {
            dragTextBox = false;
            transformTextBox = false;
            initialPosition = Point.Empty;
            moveNewPosition = Point.Empty;
        }

        public void raiseEventsStatus(bool newStatus)
        {
            raiseEvents = newStatus;
        }

        private void textBox_Click(object sender, MouseEventArgs e)
        {
            this.ForceRefresh();
        }

        private void transformSelection(Point location, textBox sender)
        {
            moveNewPosition = Point.Empty;
            moveNewPosition.X = location.X - initialPosition.X;
            moveNewPosition.Y = location.Y - initialPosition.Y;

            //stanga sus
            if (finalPosition.X < resizingEdge && finalPosition.Y < resizingEdge)
            {
                sender.Left += moveNewPosition.X;
                sender.Top += moveNewPosition.Y;
                sender.Height -= moveNewPosition.Y;
                sender.Width -= moveNewPosition.X;
            }
            //dreapta jos
            else if (sender.Cursor == Cursors.SizeNWSE)
            {
                sender.Height = moveNewPosition.Y + initialSize.Height;
                sender.Width = moveNewPosition.X + initialSize.Width;
            }
            //dreapta sus
            else if (sender.Cursor == Cursors.SizeNESW && location.X > sender.Width / 2)
            {
                sender.Top += moveNewPosition.Y;
                sender.Height -= moveNewPosition.Y;
                sender.Width = moveNewPosition.X + initialSize.Width;
            }
            //stanga jos
            else if (sender.Cursor == Cursors.SizeNESW)
            {
                sender.Left += moveNewPosition.X;
                sender.Height = moveNewPosition.Y + initialSize.Height;
                sender.Width -= moveNewPosition.X;
            }
            //stanga
            else if (finalPosition.X < resizingEdge)
            {
                sender.Left += moveNewPosition.X;
                sender.Width -= moveNewPosition.X;
            }
            //dreapta
            else if (sender.Cursor == Cursors.SizeWE)
            {
                sender.Width = moveNewPosition.X + initialSize.Width;
            }
            //sus
            else if (finalPosition.Y < resizingEdge)
            {
                sender.Top += moveNewPosition.Y;
                sender.Height -= moveNewPosition.Y;
            }
            //jos
            else if (sender.Cursor == Cursors.SizeNS)
            {
                sender.Height = moveNewPosition.Y + initialSize.Height;
            }
        }

        private void changeTransformCursors(textBox sender)
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

        private void dragText()
        {
            timerMove.Start();
            do
            {
                if (timerMove.ElapsedMilliseconds > 50)
                    timerMove.Restart();


            } while (dragTextBox);
            timerMove.Reset();
        }

        private void textBox_GotFocus(object sender, EventArgs e)
        {
            if (raiseEvents)
            {           
                Control parent = ((textBox)sender).FindForm().Controls[4];
                parent.Controls.Clear();
                textBox_Properties(parent, (Control)sender);
            }
        }

        private int get_fontIndex(Control box)
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

        private void textBox_Properties(Control parent, Control sender)
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
            fonts.SelectedIndex = get_fontIndex(sender);
            fonts.SelectedIndexChanged += new EventHandler(fonts_SelectedIndexChanged);

            TextBox size = new TextBox();
            size.Size = new Size(50, 50);
            parent.Controls.Add(size);
            size.Location = new Point(fonts.Location.X + fonts.Width + 5, fonts.Location.Y);
            size.Visible = true;
            size.Text = sender.Font.Size + "";
            size.LostFocus += new EventHandler(size_LostFocus);
            size.KeyDown += new KeyEventHandler(size_KeyDown);

            noFocusBorderButton bold = new noFocusBorderButton();
            parent.Controls.Add(bold);
            bold.Size = new Size(30, 30);
            bold.FlatStyle = FlatStyle.Flat;
            bold.FlatAppearance.BorderSize = 0;
            bold.BackgroundImage = Resources.Bold;
            bold.Location = new Point(fonts.Location.X, fonts.Location.Y + fonts.Height + 5);
            bold.Click += new EventHandler(style_Clicked);
            bold.BackColor = idleColor;

            noFocusBorderButton italic = new noFocusBorderButton();
            parent.Controls.Add(italic);
            italic.Size = new Size(30, 30);
            italic.FlatStyle = FlatStyle.Flat;
            italic.FlatAppearance.BorderSize = 0;
            italic.BackgroundImage = Resources.Italic;
            italic.Location = new Point(bold.Location.X + bold.Width + 5, bold.Location.Y);
            italic.Click += new EventHandler(style_Clicked);
            italic.BackColor = idleColor;

            noFocusBorderButton underline = new noFocusBorderButton();
            parent.Controls.Add(underline);
            underline.Size = new Size(30, 30);
            underline.FlatStyle = FlatStyle.Flat;
            underline.FlatAppearance.BorderSize = 0;
            underline.BackgroundImage = Resources.Underline;
            underline.Location = new Point(italic.Location.X + italic.Width + 5, italic.Location.Y);
            underline.Click += new EventHandler(style_Clicked);
            underline.BackColor = idleColor;

            noFocusBorderButton colorSwitch = new noFocusBorderButton();
            parent.Controls.Add(colorSwitch);
            colorSwitch.Size = new Size(30, 30);
            colorSwitch.Location = new Point(size.Location.X, size.Location.Y + size.Height + 5);
            colorSwitch.Click += new EventHandler(color_Clicked);
            colorSwitch.BackColor = this.ForeColor;
        }

        private void color_Clicked(object sender, EventArgs e)
        {
            ColorDialog colorPicker = new ColorDialog();
            raiseEvents = false;
            if (colorPicker.ShowDialog() == DialogResult.OK)
            {
                ((Button)sender).BackColor = this.ForeColor = colorPicker.Color;
            }
            raiseEvents = true;
        }

        private void update_FontStyle(object sender)
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

        private void style_Clicked(object sender, EventArgs e)
        {
            Control caller = (Control)sender;

            if (caller.BackColor == activeColor)
                caller.BackColor = idleColor;
            else
                caller.BackColor = activeColor;

            update_FontStyle(sender);
        }

        private void size_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt || e.KeyCode == Keys.Enter)
                this.Font = new Font(this.Font.FontFamily.Name, float.Parse(((TextBox)sender).Text));
            update_FontStyle(sender);
        }

        private void size_LostFocus(object sender, EventArgs e)
        {
            this.Font = new Font(this.Font.FontFamily.Name, float.Parse(((TextBox)sender).Text));
            update_FontStyle(sender);
        }

        private void fonts_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.Font = new Font(((ComboBox)sender).SelectedItem.ToString(), this.Font.Size);
            update_FontStyle(sender);
        }

        private void textBox_LostFocus(object sender, EventArgs e)
        {
            if (raiseEvents)
            {
                Control parent = ((textBox)sender).FindForm().Controls[4];
                parent.Controls.Clear();
            }
        }
    }
    #endregion

    public class noFocusBorderButton : Button
    {
        public noFocusBorderButton() : base()
        {
            this.SetStyle(ControlStyles.Selectable, false);
        }
    }

    
    public class DirectBitmap : IDisposable
    {
        public Bitmap Bitmap { get; private set; }
        public Int32[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Height { get; private set; }
        public int Width { get; private set; }

        protected GCHandle BitsHandle { get; private set; }

        public DirectBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            Bits = new Int32[width * height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, BitsHandle.AddrOfPinnedObject());
        }

        public DirectBitmap(Bitmap original)
        {
            Width = original.Width;
            Height = original.Height;
            Bits = new Int32[Width * Height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppArgb, BitsHandle.AddrOfPinnedObject());

            using (Graphics g = Graphics.FromImage(Bitmap))
            {
                g.DrawImage(original,0,0);
            }
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
    }
    
}
