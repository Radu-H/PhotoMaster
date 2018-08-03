using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.Threading;
using System.Diagnostics;
using Project.Properties;


namespace Project
{
    public partial class Form1 : Form
    {
        string Title = "Untitled";
        
        Point selectStart = Point.Empty;
        Point originalSelectStart = Point.Empty;
        Point selectEnd = Point.Empty;
        Point originalSelectEnd = Point.Empty;
        Size originalSize = new Size();
        Bitmap original;
        Bitmap copiedImage;

        double multiplier = 1.2;
        public DisplayPanel display;
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

        
        public Form1()
        {
            InitializeComponent();

            //Application.ThreadException += ReportCrash;
            //AppDomain.CurrentDomain.UnhandledException += ReportCrash;

            //picturebox
            workArea = new PictureBox
            {
                Size = new Size(450, 450),
                Location = Point.Empty,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Visible = false
            };
            workArea.MouseWheel += new System.Windows.Forms.MouseEventHandler(PictureBox1_Wheel);
            workArea.SizeChanged += new System.EventHandler(WorkArea_Size);
            
            //display panel
            display = new DisplayPanel
            {
                Location = new Point(53, 42)
            };
            this.Controls.Add(display);
            display.BackColor = Color.DarkGray;
            display.AutoScroll = true;
            display.Size = new Size(490, 490);
            display.Resize += new System.EventHandler(Panel1_Resize);
            //display.MouseLeave += new EventHandler(properties_MouseLeave);
            workArea.MouseEnter += new EventHandler(Properties_MouseEnter);
            display.Controls.Add(workArea);
            
            //tool box
            toolBox = new Panel();
            this.Controls.Add(toolBox);
            toolBox.Enabled = true;
            toolBox.BackColor = Color.Transparent;
            toolBox.Location = new Point(5, display.Location.Y);
            toolBox.Size = new Size(display.Left - 10, display.ClientSize.Height);
            GenerateToolBox();

            //toolBar
            toolBar = new Panel();
            this.Controls.Add(toolBar);
            toolBar.Enabled = true;
            toolBar.BackColor = Color.Transparent;
            toolBar.Location = new Point(display.Left, display.Top + display.Height + 5);
            toolBar.Size = new Size(display.Width, 34);

            //selection box
            selectionBox = new PictureBox
            {
                Size = workArea.Size,
                Location = Point.Empty,
                BackColor = Color.Transparent,
                Enabled = false
            };
            workArea.Controls.Add(selectionBox);
            selectionBox.MouseDown += new MouseEventHandler(SelectionBox_MouseDown);
            selectionBox.MouseUp += new MouseEventHandler(SelectionBox_MouseUp);
            selectionBox.MouseMove += new MouseEventHandler(SelectionBox_Move);

            //properties panel
            properties = new Panel();
            this.Controls.Add(properties);
            properties.Visible = false;
            properties.MouseEnter += new EventHandler(Properties_MouseEnter);
            properties.MouseLeave += new EventHandler(Properties_MouseLeave);

            //timer , stopwatch
            timerMove = new Stopwatch();

            //BackColor = Color.FromArgb(2,88,84);
            //menuStrip1.BackColor = Color.FromArgb(2, 88, 84);
            BackColor = Color.FromArgb(102, 153, 153);
            menuStrip1.BackColor = Color.FromArgb(92, 139, 139);
        }
        
        private void ReviewForm()
        {
            Form f = new Form
            {
                Text = "Feedback/ Bug Report",
                Size = new Size(500, 700)
            };

            Label name_lb = PopLabel(150, 50, "Display name: ", f);
            TextBox name = PopText(250, 50, f);
            Label message_lb = PopLabel(150, 100, "Please give us your feedback:", f);
            RichTextBox message = PopRichText(100, 130, 300, 400, f);
            Button bt = PopButton(200, 550, "Send", f);
            
            bt.Click += new EventHandler(SendFeedback);

            f.ShowDialog();
        }

        private void ReviewForm(object exception)
        {
            Form f = new Form
            {
                Text = "Crash Report",
                Size = new Size(500, 700)
            };

            Label name_lb = PopLabel(150, 50, "Display name: ", f);
            TextBox name = PopText(250, 50, f);
            Label message_lb = PopLabel(100, 100, "Please describe what were you doing when the crash occured:", f);
            RichTextBox message = PopRichText(100, 130, 300, 400, f);
            Button bt = PopButton(200, 550, "Send", f);

            bt.Click += (sender, e) => { SendBug(sender, e, (string)exception); };
            f.ShowDialog();
        }

        private void SendBug(object sender, EventArgs e, string exception)
        {
            string message = "Report sent by:   ";
            message += ((Button)sender).FindForm().Controls[1].Text + "\n\n";
            message += "User's message:\n";
            message += ((Button)sender).FindForm().Controls[3].Text + "\n\n";
            message += "Exception:\n" + exception;

            SendReport(message, "Crash report");

            ((Button)sender).FindForm().Close();
        }

        private void SendFeedback(object sender, EventArgs e)
        {
            string message = "Report sent by:   ";
            message += ((Button)sender).FindForm().Controls[1].Text + "\n\n";
            message += "User's message:\n";
            message += ((Button)sender).FindForm().Controls[3].Text;

            SendReport(message, "User's report");

            ((Button)sender).FindForm().Close();
        }

        private void Properties_MouseEnter(object sender, EventArgs e)
        {
            if (index > -1)
                if (elements[index].GetType().Equals(typeof(TextBoxTool)))
                    ((TextBoxTool)elements[index]).RaiseEventsStatus(true);
        }

        private void Properties_MouseLeave(object sender, EventArgs e)
        {
            if (index > -1)
                if (elements[index].GetType().Equals(typeof(TextBoxTool)))
                    ((TextBoxTool)elements[index]).RaiseEventsStatus(false);
        }

        private void WorkArea_Size(object sender, EventArgs e)
        {
            if (SelectingActive == true)
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
                if (SelectingActive == true)
                {
                    SelectingActive = false;
                    if (selecting.IsAlive == true)
                    {
                        selectionBox.Invalidate();
                        Thread.Sleep(3);
                    }
                    selecting = new Thread(DrawSelectionThread);
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

        private void SelectionBox_MouseDown(object sender, MouseEventArgs e)
        {
            selectionBox.Size = workArea.Size;
            selectZoomCount = 0;
            if (SelectingActive == true)
            {
                SelectingActive = false;
            }
            if (drawingActive == true)
            {
                drawingActive = false;
            }

            mouseDown = true;
            originalSelectStart = selectStart = e.Location;
            selectEnd = new Point(-1, -1);
            selectionBox.Invalidate();
            TextBoxRefresh();
        }

        private Rectangle CreateRectangle(Point start, Point end)
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
        
        private void SelectionBox_Move(object sender, MouseEventArgs e)
        {
            Point selectCurrent = new Point(e.Location.X, e.Location.Y);

            if (mouseDown)
            {
                switch (mode)
                {
                    case 0:
                    case 1:
                    case 3:
                        if (selectEnd.X != -1)
                        {
                            DrawSelection(selectStart, selectEnd);
                        }
                        originalSelectEnd = selectEnd = selectCurrent;
                        DrawSelection(selectStart, selectCurrent);
                        break;
                    case 2:
                        if (selectEnd.X != -1)
                        {
                            DrawRectangle(selectStart, selectEnd);
                        }
                        originalSelectEnd = selectEnd = selectCurrent;
                        DrawRectangle(selectStart, selectCurrent);
                        break;
                    case 4:
                        if(selectEnd.X!=-1)
                        {
                            DrawEllipse(selectStart, selectEnd);
                        }
                        originalSelectEnd = selectEnd = selectCurrent;
                        DrawEllipse(selectStart, selectEnd);
                        break;
                }
                selectionBox.Invalidate();
                Application.DoEvents();
                TextBoxRefresh();
            }
        }

        private void SelectionBox_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
            if (toolBox.Controls[1].BackColor != activeColor && mode!=2 && mode!=4)
            {
                selecting = new Thread(DrawSelectionThread);
                if (selectEnd.X != -1)
                {
                    selecting.Start();
                }
            }
            else
            {
                switch (mode)
                {
                    case 1:
                        {
                            TextBoxTool txtbox = new TextBoxTool(CreateRectangle(selectStart, selectEnd), workArea);
                            txtbox.Click += new EventHandler(Object_Click);
                            AddElement(txtbox);
                            break;
                        }
                    case 2:
                        {
                            //Rectangle r = createRectangle(selectStart, selectEnd);
                            //PictureBox rectangle = new PictureBox();
                            //display.Controls.Add(rectangle);
                            //rectangle.BringToFront();
                            //rectangle.Size = new Size(r.Width, r.Height);
                            //rectangle.Location = new Point(r.X + workArea.Left, r.Y + workArea.Top);

                            DrawRectangle rectangle = new DrawRectangle(selectStart, selectEnd, display);
                            rectangle.MouseDown += new MouseEventHandler(Object_Click);
                            //setRectangleColor(Color.Black, rectangle);

                            AddElement(rectangle);
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
                            ellipse.MouseDown += new MouseEventHandler(Object_Click);
                            AddElement(ellipse);
                            break;
                        }
                }
                DeactivateTools();
            }
        }
        
        private void Rectangle_SizeChanged(object sender, EventArgs e)
        {
            //schimba height
            this.Controls[4].Controls[1].Text = elements[index].Height.ToString();
            //schimba width
            this.Controls[4].Controls[3].Text = elements[index].Width.ToString();
        }

        private void Rectangle_MouseClick(object sender, MouseEventArgs e)
        {
            ((Control)sender).Focus();
            this.Controls[4].Controls.Clear();
            RectangleProperties();
        }
        
        private void PictureBox1_Wheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (zoomCount < 25)
                {
                    zoomCount++;
                    ZoomIn(e.Location);
                }
            }
            else
            {
                if (zoomCount > -15 && (workArea.Width >= 48 || workArea.Height >= 48))
                {
                    zoomCount--;
                    ZoomOut(e.Location);
                }
            }
            Display_update_background();
        }
        
        private void Tool_Clicked(object sender, EventArgs e)
        {
            if (SelectingActive == true)
                if (selecting.IsAlive == true)
                {
                    SelectingActive = false;
                    selectionBox.Invalidate();
                    TextBoxRefresh();
                }
            if (((Button)sender).BackColor == activeColor)
            {
                ((Button)sender).BackColor = idleColor;
                mode = -1;
            }
            else
            {
                foreach (Button bt in toolBox.Controls)
                {
                    bt.BackColor = idleColor;
                }
                 ((Button)sender).BackColor = activeColor;
                mode = ((Button)sender).Parent.Controls.GetChildIndex(((Button)sender));
                switch (mode)
                {
                    case 2: ((Button)sender).BackgroundImage = Resources.Rectangle_active; break;
                    case 4: ((Button)sender).BackgroundImage = Resources.Circle_active; break;
                }
            }
            ActivateMode();
        }
        
        private void Color_Clicked(object sender, EventArgs e)
        {
            ColorDialog colorPicker = new ColorDialog();
            if (colorPicker.ShowDialog() == DialogResult.OK)
            {
                ((Button)sender).BackColor = colorPicker.Color;
                SetRectangleColor(colorPicker.Color, (PictureBox)elements[index]);
                
            }
        }

        private void HeightText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                elements[index].Height = Convert.ToInt32(((System.Windows.Forms.TextBox)sender).Text);
            }
            catch(Exception )
            {
                Message("Invalid height! Try again.");
            }
        }

        private void WidthText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                elements[index].Width = Convert.ToInt32(((System.Windows.Forms.TextBox)sender).Text);
            }
            catch(Exception )
            {
                Message("Invalid width! Try again.");
            }
        }
        
        #region FILE BUTTONS

        #region NEW
        
        private void NewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NewAction();
        }

        private void BrowseClicked(object sender, EventArgs e)
        {
            BrowseImage((PictureBox)((Button)sender).Parent.FindForm().Controls[4]);
        }
        
        private void NewOption_Clicked(object sender, EventArgs e)
        {
            Button buttonClicked = sender as Button;

            if (((Button)sender).Parent.FindForm().Controls[1].Text.Length <= 0)
                Message("Insert a project name");
            else
            {
                Title = ((Button)sender).Parent.FindForm().Controls[1].Text;
                this.Text += " - " + Title;
                if (((PictureBox)((Button)sender).Parent.FindForm().Controls[4]).Visible == true)
                {
                    ResizePictureBox(workArea, ((PictureBox)((Button)sender).Parent.FindForm().Controls[4]).Image);
                }
                workArea.Visible = true;
                toolBox.Enabled = true;
                CenterPictureBox();
                ((Button)sender).Parent.FindForm().Close();
                EnableMenu(true);

            }
        }
        #endregion

        #region OPEN
        
        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenAction();
        }
        
        #endregion

        #region SAVE
        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //to be implemented
        }
        #endregion

        #region SAVE AS
        
        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveAsAction();
        }

        private void Fname_TextChanged(object sender, EventArgs e)
        {
            int i = ((System.Windows.Forms.TextBox)sender).SelectionStart;
            ((System.Windows.Forms.TextBox)sender).TextChanged -= Fname_TextChanged;
            if (!((System.Windows.Forms.TextBox)sender).Text.EndsWith("." + GetCheckedType((GroupBox)(((System.Windows.Forms.TextBox)sender).Parent.Controls[6]))))
            {
                if (((System.Windows.Forms.TextBox)sender).Text.LastIndexOf(".") > -1)
                    ((System.Windows.Forms.TextBox)sender).Text = ((System.Windows.Forms.TextBox)sender).Text.Substring(0, ((System.Windows.Forms.TextBox)sender).Text.LastIndexOf("."));
                ((System.Windows.Forms.TextBox)sender).Text += GetCheckedType((GroupBox)(((System.Windows.Forms.TextBox)sender).Parent.Controls[6]));
            }
            ((System.Windows.Forms.TextBox)sender).TextChanged += Fname_TextChanged;
            ((System.Windows.Forms.TextBox)sender).SelectionStart = i;
        }
        
        private void BrowseSaveAs_Clicked(object sender, EventArgs e)
        {
            FolderBrowserDialog saveDialog = new FolderBrowserDialog();
            saveDialog.ShowDialog();
            ((Button)sender).Parent.FindForm().Controls[1].Text = saveDialog.SelectedPath;
        }

        private void Save_Clicked(object sender, EventArgs e)
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

                if (obj.GetType().Equals(typeof(TextBoxTool)))
                {
                    Font saveFont = new Font(((TextBoxTool)obj).Font.FontFamily ,((TextBoxTool)obj).Font.Size * workArea.Image.Width / workArea.Width, ((TextBoxTool)obj).Font.Style);
                    SolidBrush textColor = new SolidBrush(((TextBoxTool)obj).ForeColor);
                    g.DrawString(((TextBoxTool)obj).Text, saveFont, textColor, obj.Location.X * workArea.Image.Width / workArea.Width, obj.Location.Y * workArea.Image.Height / workArea.Height);
                }
            }

            if (types.Contains(((Button)sender).Parent.FindForm().Controls[4].Text.Substring(((Button)sender).Parent.FindForm().Controls[4].Text.IndexOf("."))))
            {
                try
                {
                    saveBitmap.Save(((Button)sender).Parent.FindForm().Controls[1].Text + "\\" + ((Button)sender).Parent.FindForm().Controls[4].Text);

                    Message("Save completed!");

                    ((Button)sender).Parent.FindForm().Close();
                }
                catch (Exception ex)
                {
                    Message(ex.Message);
                }
            }
            else
                Message("Choose a valid file format!");

        }

        private void SaveType_Changed(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Parent.FindForm().Controls[4].Text.Contains("."))
                ((RadioButton)sender).Parent.FindForm().Controls[4].Text = ((RadioButton)sender).Parent.FindForm().Controls[4].Text.Substring(0, ((RadioButton)sender).Parent.FindForm().Controls[4].Text.IndexOf('.')) + ((RadioButton)sender).Text;
            else
                ((RadioButton)sender).Parent.FindForm().Controls[4].Text += ((RadioButton)sender).Text;
        }
        #endregion

        #region IMPORT
        private void ImportToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region EXPORT
        private void ExportToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region CLOSE
        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Controls[4].Controls.Clear();
            Title = "Untitled";
            this.Text = this.Text.Substring(0, this.Text.IndexOf('-') - 1);
            Initialise_workArea();
            if (SelectingActive == true)
                if (selecting.IsAlive == true)
                    SelectingActive = false;

            if (drag == true)
                if (dragging.IsAlive == true)
                    drag = false;

            selectionBox.Invalidate();
            workArea.Visible = false;
            EnableMenu(false);
        }
        #endregion

        #region PRINT
        //to be revised
        private void PrintToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Visible == true)
            {
                PrintDialog print = new PrintDialog();
                if (print.ShowDialog() == DialogResult.OK)
                {
                    PrintDocument pd = new PrintDocument();
                    pd.PrintPage += PrintPage;
                    pd.Print();
                }
            }
            else
            {
                Message("There is no image to print");
            }
        }

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            Image image = workArea.Image;

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
            Message("Imaginea a fost printata cu succes!");
        }
        #endregion

        #region OPTIONS
        private void OptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region EXIT
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Visible == true)
            {
                Form exitForm = PopForm("Exit", 250, 150);
                exitForm.Location = new Point((this.Width - exitForm.Width) / 2, (this.Height - exitForm.Height) / 2);
                Label exitLabel = PopLabel(40, 25, "Do you want to save changes?", exitForm);
                Button yesButton = PopButton(15, 75, "YES", exitForm);
                Button noButton = PopButton(150, 75, "NO", exitForm);

                noButton.Click += new System.EventHandler(CloseApp);

                yesButton.Click += new System.EventHandler(SaveAsToolStripMenuItem_Click);
            }
            else
            {
                this.Close();
            }
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
        
        private void UndoToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void RedoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyAction();
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CutAction();
        }        

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PasteAction();
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteAction();
        }

        private void CropToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectingActive = false;
            selectionBox.Invalidate();
            TextBoxRefresh();

            CreateSelection();
            workArea.Image = copiedImage;
            ResizePictureBox(workArea, workArea.Image);
            CenterPictureBox();
            zoomCount = 0;
            toolBox.Controls[0].BackColor = idleColor;
            DeactivateAll();
        }

        private void Copy_MouseClick(object sender, MouseEventArgs e)
        {
            ContextMenu contextMenu = new ContextMenu();
            MenuItem copy = new MenuItem("Copy");
            MenuItem cut = new MenuItem("Cut");
            MenuItem delete = new MenuItem("Delete");
            MenuItem flipHorizontal = new MenuItem("Flip Horizontal");
            MenuItem flipVertical = new MenuItem("Flip Vertical");

            copy.Click += new EventHandler(Copy_Click);
            cut.Click += new EventHandler(CutToolStripMenuItem_Click);
            delete.Click += new EventHandler(DeleteToolStripMenuItem_Click);
            flipHorizontal.Click += new EventHandler(FlipHorizontal_Click);
            flipVertical.Click += new EventHandler(FlipVertical_Click);

            contextMenu.MenuItems.AddRange(new MenuItem[] { copy, cut, delete, flipHorizontal, flipVertical });
            ((PictureBox)sender).ContextMenu = contextMenu;
        }

        private void Copy_Click(object sender, EventArgs e)
        {
            copiedImage = new Bitmap(((PictureBox)elements[index]).Image);
        }

        private void FlipHorizontal_Click(object sender, EventArgs e)
        {
            Bitmap original = new Bitmap(((PictureBox)elements[index]).Image);
            original.RotateFlip(RotateFlipType.RotateNoneFlipX);
            ((PictureBox)elements[index]).Image = original;
        }

        private void FlipVertical_Click(object sender, EventArgs e)
        {
            Bitmap original = new Bitmap(((PictureBox)elements[index]).Image);
            original.RotateFlip(RotateFlipType.RotateNoneFlipY);
            ((PictureBox)elements[index]).Image = original;
        }

        private void Move_MouseDown(object sender, MouseEventArgs e)
        {
            if (elements[index].GetType().Equals(typeof(TextBoxTool)))
                ((TextBoxTool)elements[index]).LoseFocus((Control)sender);
            index = elements.IndexOf(sender as Control);
            drag = true;
            movePosition = e.Location;
            dragging = new Thread(DragPicture);
            dragging.Start();
            startSize = elements[index].Size;
            ((Control)sender).BringToFront();
            this.Controls[4].Controls.Clear();
            RectangleProperties();
        }

        private void Object_Click(object sender, EventArgs e)
        {
            index = elements.IndexOf(sender as Control);
        }

        private void Move_MouseMove(object sender, MouseEventArgs e)
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
                            TransformSelection(e.Location);
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
                        ChangeTransformCursors();
                    else
                        ((PictureBox)sender).Cursor = Cursors.Default;
                }
            }
        }

        private void Move_MouseUp(object sender, MouseEventArgs e)
        {
            drag = false;
            transform = false;
            movePosition = Point.Empty;
            moveNewPosition = Point.Empty;
        }

        #endregion

        #region EFFECTS
        private void GrayscaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                GrayscaleThread(workArea);
        }
        
        private void RedFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                RedFilterThread(workArea);
        }

        private void GreenFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                GreenFilterThread(workArea);
        }

        private void BlueFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                BlueFilterThread(workArea);
        }
        
        private void InvertColorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                InvertThread(workArea);
        }
        
        private void SepiaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                SepiaThread(workArea);
        }
        
        #endregion

        #region VIEW BUTTONS
        
        private void FullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FullScreenAction();
        }

        private void ExitFullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.WindowState = FormWindowState.Normal;
            fullScreenToolStripMenuItem.Visible = true;
            fullscreenVisible = true;
            exitFullScreenToolStripMenuItem.Visible = false;
        }
        
        private void ZoomInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ZoomInAction();
        }
        
        private void ZoomOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ZoomOutAction();
        }

        private void FitOnScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResizeImage(workArea.Image, display.ClientSize.Width - 120, display.ClientSize.Height - 120);
            ResizePictureBox(workArea, workArea.Image);
            CenterPictureBox();
            zoomCount = (int)Math.Ceiling((Math.Log(workArea.Width / originalSize.Width) / Math.Log(multiplier)));
        }
        
        #endregion

        #region SELECT BUTTONS
        
        private void AllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AllAction();
        }

        private void DeselectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectingActive == true)
                SelectingActive = false;
            selectionBox.Invalidate();
            TextBoxRefresh();
        }

        private void InverseSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
        
        #endregion

        private void Form1_Resize(object sender, EventArgs e)
        {
            display.Size = new Size(this.Width - 340, this.Height - 125);
            properties.Location = new Point(display.Location.X + display.Width + 5, properties.Location.Y);
        }

        private void Panel1_Resize(object sender, EventArgs e)
        {
            int xi = Math.Abs(display.AutoScrollPosition.X), yi = Math.Abs(display.AutoScrollPosition.Y);

            CenterPictureBox();
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
        
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (SelectingActive == true)
                if (selecting.IsAlive == true)
                    SelectingActive = false;

            if (drag == true)
                if (dragging.IsAlive == true)
                    drag = false;
        }

        private void Tool_MouseEnter(object sender, EventArgs e)
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

        private void Tool_MouseLeave(object sender, EventArgs e)
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
        
        private void ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolBar.Visible = true;
            int i = 0;
            ToolBar_Reset();
            activeToolBar = menuStrip1.Items.IndexOf((ToolStripItem)sender);
            switch (activeToolBar)
            {
                case 0:
                    {
                        CustomizeButton(ref i, "New",
                            new EventHandler(NewToolStripMenuItem_Click),
                            true,
                            Resources.New, Resources.New);

                        CustomizeButton(ref i, "Open",
                            new EventHandler(OpenToolStripMenuItem_Click),
                            true,
                            Resources.Open, Resources.Open);

                        //CustomizeButton(ref i, "Save",
                        //    new EventHandler(SaveToolStripMenuItem_Click),
                        //    saveToolStripMenuItem.Enabled,
                        //    Resources.Save_active, Resources.Save_inactive);

                        CustomizeButton(ref i, "Save As",
                            new EventHandler(SaveAsToolStripMenuItem_Click),
                            saveAsToolStripMenuItem.Enabled,
                            Resources.SaveAs_active, Resources.SaveAs_inactive);

                        CustomizeButton(ref i, "Close",
                            new EventHandler(CloseToolStripMenuItem_Click),
                            closeToolStripMenuItem.Enabled,
                            Resources.Close_active, Resources.Close_inactive);

                        //CustomizeButton(ref i, "Print",
                        //    new EventHandler(PrintToolStripMenuItem_Click),
                        //    printToolStripMenuItem.Enabled,
                        //    Resources.Print_active, Resources.Print_inactive);

                        CustomizeButton(ref i, "Exit",
                            new EventHandler(ExitToolStripMenuItem_Click),
                            true,
                            Resources.Exit, Resources.Exit);

                        break;
                    }
                case 1:
                    {
                        CustomizeButton(ref i, "Copy",
                            new EventHandler(CopyToolStripMenuItem_Click),
                            copyToolStripMenuItem.Enabled,
                            Resources.Copy_active, Resources.Copy_inactive);

                        CustomizeButton(ref i, "Cut",
                            new EventHandler(CutToolStripMenuItem_Click),
                            cutToolStripMenuItem.Enabled,
                            Resources.Cut, Resources.Cut);

                        CustomizeButton(ref i, "Paste",
                            new EventHandler(PasteToolStripMenuItem_Click),
                            pasteToolStripMenuItem.Enabled,
                            Resources.Paste_active, Resources.Paste_inactive);

                        CustomizeButton(ref i, "Delete",
                            new EventHandler(DeleteToolStripMenuItem_Click),
                            deleteToolStripMenuItem.Enabled,
                            Resources.Delete_active, Resources.Delete_inactive);

                        CustomizeButton(ref i, "Crop",
                            new EventHandler(CropToolStripMenuItem_Click),
                            cropToolStripMenuItem.Enabled,
                            Resources.Crop_active, Resources.Crop_inactive);

                        break;
                    }
                case 2:
                    {
                        CustomizeButton(ref i, "Grayscale",
                            new EventHandler(GrayscaleToolStripMenuItem_Click),
                            grayscaleToolStripMenuItem.Enabled,
                            Resources.Grayscale, Resources.Grayscale);

                        CustomizeButton(ref i, "Sepia",
                            new EventHandler(SepiaToolStripMenuItem_Click),
                            sepiaToolStripMenuItem.Enabled,
                            Resources.Sepia_active, Resources.Grayscale);

                        CustomizeButton(ref i, "Red Filter",
                            new EventHandler(RedFilterToolStripMenuItem_Click),
                            redFilterToolStripMenuItem.Enabled,
                            Resources.RedFilter_active, Resources.Grayscale);

                        CustomizeButton(ref i, "Green Filter",
                            new EventHandler(GreenFilterToolStripMenuItem_Click),
                            greenFilterToolStripMenuItem.Enabled,
                            Resources.GreenFilter_active, Resources.Grayscale);

                        CustomizeButton(ref i, "Blue Filter",
                            new EventHandler(BlueFilterToolStripMenuItem_Click),
                            blueFilterToolStripMenuItem.Enabled,
                            Resources.BlueFilter_active, Resources.Grayscale);

                        CustomizeButton(ref i, "Invert Filter",
                            new EventHandler(InvertColorsToolStripMenuItem_Click),
                            invertColorsToolStripMenuItem.Enabled,
                            Resources.InvertColors, Resources.Grayscale);

                        CustomizeButton(ref i, "Revert to Original",
                            new EventHandler(RevertToOriginalToolStripMenuItem_Click),
                            revertToOriginalToolStripMenuItem.Enabled,
                            Resources.RevertToOriginal_active, Resources.RevertToOriginal_inactive);

                        break;
                    }
                case 3:
                    {
                        fullscreenVisible = fullScreenToolStripMenuItem.Visible;
                        if (fullscreenVisible)
                        {
                            CustomizeButton(ref i, "Full Screen",
                                new EventHandler(FullScreenToolStripMenuItem_Click),
                                true,
                                Resources.FullScreen, Resources.FullScreen);
                        }
                        else
                        {
                            CustomizeButton(ref i, "Exit Full Screen",
                                new EventHandler(ExitFullScreenToolStripMenuItem_Click),
                                true,
                                Resources.ExitFullScreen, Resources.ExitFullScreen);
                        }

                        CustomizeButton(ref i, "Zoom In",
                            new EventHandler(ZoomInToolStripMenuItem_Click),
                            zoomInToolStripMenuItem.Enabled,
                            Resources.ZoomIn_active, Resources.ZoomIn_inactive);

                        CustomizeButton(ref i, "Zoom Out",
                            new EventHandler(ZoomOutToolStripMenuItem_Click),
                            zoomOutToolStripMenuItem.Enabled,
                            Resources.ZoomOut_active, Resources.ZoomOut_inactive);

                        CustomizeButton(ref i, "Fit On Screen",
                            new EventHandler(FitOnScreenToolStripMenuItem_Click),
                            fitOnScreenToolStripMenuItem.Enabled,
                            Resources.FitOnScreen_active, Resources.FitOnScreen_active);

                        break;
                    }

                case 4:
                    {
                        CustomizeButton(ref i, "Select All",
                            new EventHandler(AllToolStripMenuItem_Click),
                            allToolStripMenuItem.Enabled,
                            Resources.SelectAll_active, Resources.SelectAll_inactive);

                        CustomizeButton(ref i, "Deselect",
                            new EventHandler(DeselectToolStripMenuItem_Click),
                            deselectToolStripMenuItem.Enabled,
                            Resources.Deselect_active, Resources.Deselect_inactive);

                        break;
                    }
            }
        }

        private void SaveToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            //if (activeToolBar == 0)
            //{
            //    toolBar.Controls[2].Enabled = saveToolStripMenuItem.Enabled;
            //    if (toolBar.Controls[2].Enabled)
            //        toolBar.Controls[2].BackgroundImage = Resources.Save_active;
            //    else
            //        toolBar.Controls[2].BackgroundImage = Resources.Save_inactive;
            //}
        }

        private void SaveAsToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
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

        private void CloseToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
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

        private void PrintToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
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

        private void CopyToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
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

        private void CutToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
        {
            //if (activeToolBar == 1)
            //{
            //    toolBar.Controls[0].Enabled = cutToolStripMenuItem.Enabled;
            //    if (toolBar.Controls[0].Enabled)
            //        toolBar.Controls[0].BackgroundImage = Resources.Cut;
            //    else
            //        toolBar.Controls[0].BackgroundImage = Resources.Cut;
            //}
        }

        private void PasteToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
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

        private void DeleteToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
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

        private void CropToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
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

        private void ExitfullScreenToolStripMenuItem_VisibleChanged(object sender, EventArgs e)
        {
            if (activeToolBar == 2)
            {
                if (fullscreenVisible)
                {
                    toolBar.Controls[0].Click -= ExitFullScreenToolStripMenuItem_Click;
                    toolBar.Controls[0].Click += FullScreenToolStripMenuItem_Click;
                    toolBar.Controls[0].BackgroundImage = Resources.FullScreen;
                }
                else
                {
                    toolBar.Controls[0].Click -= FullScreenToolStripMenuItem_Click;
                    toolBar.Controls[0].Click += ExitFullScreenToolStripMenuItem_Click;
                    toolBar.Controls[0].BackgroundImage = Resources.ExitFullScreen;
                }
            }
        }

        private void AllToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
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

        private void RevertToOriginalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (workArea.Enabled)
                workArea.Image = original;
        }

        private void HelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReviewForm();
        }

        private void DeselectToolStripMenuItem_EnabledChanged(object sender, EventArgs e)
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
                NewAction();
            if (e.Control && e.KeyCode == Keys.O)
                OpenAction();
            if (e.Control && e.KeyCode == Keys.Shift && e.KeyCode == Keys.S)
                SaveAsAction();               
            if (e.KeyData == Keys.Delete)
                DeleteAction();
            if (e.Control && e.KeyCode == Keys.C)
                if (copyToolStripMenuItem.Enabled)
                    if(mode==0)
                        CopyAction();
                    else
                    {
                        copiedImage = new Bitmap(((PictureBox)elements[index]).Image);
                    }
            if (e.Control && e.KeyCode == Keys.X)
                if (cutToolStripMenuItem.Enabled)
                    CutAction();
            if (e.Control && e.KeyCode == Keys.V)
                if (pasteToolStripMenuItem.Enabled)
                    PasteAction();
            if (e.Control && e.KeyCode == Keys.F)
                FullScreenAction();
            if (e.Control && e.KeyCode == Keys.Oemplus)
                ZoomInAction();
            if (e.Control && e.KeyCode == Keys.OemMinus)
                ZoomOutAction();
            if (e.Control && e.KeyCode == Keys.A)
                AllAction();
        }

        #endregion
    }

    

    

    
    //public class DirectBitmap : IDisposable
    //{
    //    public Bitmap Bitmap { get; private set; }
    //    public Int32[] Bits { get; private set; }
    //    public bool Disposed { get; private set; }
    //    public int Height { get; private set; }
    //    public int Width { get; private set; }

    //    protected GCHandle BitsHandle { get; private set; }

    //    public DirectBitmap(int width, int height)
    //    {
    //        Width = width;
    //        Height = height;
    //        Bits = new Int32[width * height];
    //        BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
    //        Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, BitsHandle.AddrOfPinnedObject());
    //    }

    //    public DirectBitmap(Bitmap original)
    //    {
    //        Width = original.Width;
    //        Height = original.Height;
    //        Bits = new Int32[Width * Height];
    //        BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
    //        Bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppArgb, BitsHandle.AddrOfPinnedObject());

    //        using (Graphics g = Graphics.FromImage(Bitmap))
    //        {
    //            g.DrawImage(original,0,0);
    //        }
    //    }

    //    public void Dispose()
    //    {
    //        if (Disposed) return;
    //        Disposed = true;
    //        Bitmap.Dispose();
    //        BitsHandle.Free();
    //    }
    //}
    
}
