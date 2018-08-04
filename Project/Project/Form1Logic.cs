using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Net.Mail;
using System.Threading;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Project.Properties;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Project
{
    public partial class Form1 : Form
    {
        List<string> types = new List<string> { ".png", ".jpg", ".jpeg", ".bmp" };
        int zoomCount = 0, selectZoomCount = 0;
        int mode = -1;
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
        bool selectingActive = false;
        bool fullscreenVisible;

        public Stopwatch timerMove;

        delegate void threadBoolCallback(bool active);

        public bool SelectingActive
        {
            get { return selectingActive; }
            set
            {
                selectingActive = value;
                if (selectingActive)
                {
                    this.MenuStripItemEnable(true);
                }
                else
                {
                    this.MenuStripItemEnable(false);
                    toolBox.Controls[0].BackColor = toolBox.Controls[3].BackColor = idleColor;
                }
            }
        }

        private void MenuStripItemEnable(bool active)
        {
            if (this.menuStrip1.InvokeRequired)
            {
                threadBoolCallback callbk = new threadBoolCallback(MenuStripItemEnable);
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

        private void ReportCrash(object sender, ThreadExceptionEventArgs e)
        {
            ReviewForm(e.Exception.ToString());
            Application.Exit();
        }
        private void ReportCrash(object sender, UnhandledExceptionEventArgs e)
        {
            ReviewForm(e.ExceptionObject.ToString());
            Environment.Exit(0);
        }
        
        private void SendReport(string message, string subject)
        {
            string from = "developingsoftware01@gmail.com";
            string pass = "C#ForTheWin";

            SmtpClient client = new SmtpClient
            {
                Port = 587,
                Host = "smtp.gmail.com",
                EnableSsl = true,
                Timeout = 10000,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential(from, pass)
            };

            MailMessage sendmail = new MailMessage
            {
                IsBodyHtml = false,
                From = new MailAddress("developingsoftware01@gmail.com", "Program")
            };
            sendmail.To.Add(new MailAddress("DraekorProjects@gmail.com"));
            sendmail.Subject = subject;
            sendmail.Body = message;

            client.Send(sendmail);
        }

        private void DrawSelection(Point start, Point end)
        {
            Rectangle r = CreateRectangle(start, end);

            Graphics g = selectionBox.CreateGraphics();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Pen p = new Pen(Brushes.Aquamarine)
            {
                DashStyle = DashStyle.DashDot
            };
            if (mode == 0)
            {
                g.DrawRectangle(p, r);
            }

            if (mode == 3)
            {
                g.DrawEllipse(p, r);
            }
        }

        private void DrawRectangle(Point start, Point end)
        {
            Rectangle r = CreateRectangle(selectStart, selectEnd);

            Graphics g = selectionBox.CreateGraphics();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Pen p = new Pen(Brushes.Black)
            {
                DashStyle = DashStyle.Solid
            };
            g.DrawRectangle(p, r);
            g.FillRectangle(Brushes.Black, r);
        }

        private void DrawEllipse(Point start, Point end)
        {
            Rectangle r = CreateRectangle(selectStart, selectEnd);

            Graphics g = selectionBox.CreateGraphics();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Pen p = new Pen(Brushes.Black)
            {
                DashStyle = DashStyle.Solid
            };
            g.DrawEllipse(p, r);
            g.FillEllipse(Brushes.Black, r);
        }

        private void DrawSelectionThread()
        {
            SelectingActive = true;

            Rectangle r = CreateRectangle(selectStart, selectEnd);

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
                if (mode == 0)
                {
                    g.DrawRectangle(p, r);
                }

                if (mode == 3)
                {
                    g.DrawEllipse(p, r);
                }

                Thread.Sleep(1);

            } while (SelectingActive);

            selectionBox.Invalidate();
            TextBoxRefreshThread();
        }

        private void DrawRectangleThread()
        {
            drawingActive = true;

            Rectangle r = CreateRectangle(selectStart, selectEnd);

            Graphics g = selectionBox.CreateGraphics();
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Pen p = new Pen(Brushes.Black)
            {
                DashStyle = DashStyle.Solid
            };
            g.DrawRectangle(p, r);
            g.FillRectangle(Brushes.Black, r);

            //selectionBox.Refresh();
        }

        private void SetRectangleColor(Color col, PictureBox rectangle)
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

        void Initialise_workArea()
        {
            Bitmap drawarea = new Bitmap(workArea.Size.Width, workArea.Size.Height);
            workArea.Image = drawarea;

            using (Graphics g = Graphics.FromImage(drawarea))
            {
                g.Clear(Color.Transparent);
                workArea.BorderStyle = BorderStyle.Fixed3D;
            }
        }

        void Message(string x)
        {
            MessageBox.Show(x);
        }

        private void GenerateToolBox()
        {
            Button select = CreateTool(toolBox.Location.X, 5, toolBox);
            toolTip.SetToolTip(select, "Selection Tool");
            select.BackgroundImage = Properties.Resources.Selection;
            toolBox.Enabled = false;

            Button textbox = CreateTool(toolBox.Location.X, 40, toolBox);
            toolTip.SetToolTip(textbox, "Text Box");
            textbox.BackgroundImage = Resources.Text;

            Button rectangle = CreateTool(toolBox.Location.X, 75, toolBox);
            toolTip.SetToolTip(rectangle, "Rectangle Tool");
            rectangle.BackgroundImage = Resources.Rectangle_inactive;

            Button circSelect = CreateTool(toolBox.Location.X, 110, toolBox);
            toolTip.SetToolTip(circSelect, "Circular Select");
            //temp
            circSelect.BackgroundImage = Resources.Circle_active;
            //temp

            Button ellipseDraw = CreateTool(toolBox.Location.X, 145, toolBox);
            toolTip.SetToolTip(ellipseDraw, "Draw Ellipse");
            ellipseDraw.BackgroundImage = Resources.Circle_active;
        }

        private void ActivateMode()
        {
            DeactivateAll();
            switch (mode)
            {
                case -1: DeactivateAll(); break;
                case 0:
                case 3: ActivateSelect(true); break;
                case 1: ActivateText(true); break;
                case 2: ActivateRectangle(true); break;
                case 4: ActivateEllipse(true); break;

            }
            TextBoxRefresh();
        }

        private void DeactivateAll()
        {
            ActivateSelect(false);
            ActivateText(false);
            if (SelectingActive == true)
            {
                if (selecting.IsAlive == true)
                {
                    SelectingActive = false;
                    selectionBox.Invalidate();
                    TextBoxRefresh();
                }
            }
        }

        private void DeactivateTools()
        {
            ActivateSelect(false);
            ActivateText(false);
            if (SelectingActive == true)
            {
                if (selecting.IsAlive == true)
                {
                    SelectingActive = false;
                    selectionBox.Invalidate();
                    TextBoxRefresh();
                }
            }

            foreach (Button bt in toolBox.Controls)
            {
                bt.BackColor = idleColor;
            }
            mode = -1;
        }

        private void ActivateSelect(bool active)
        {
            selectionBox.Enabled = active;
        }

        private void ActivateText(bool active)
        {
            selectionBox.Enabled = active;
        }

        private void ActivateRectangle(bool active)
        {
            selectionBox.Enabled = active;
        }

        private void ActivateEllipse(bool active)
        {
            selectionBox.Enabled = active;
        }

        private void RectangleProperties()
        {
            Label height = new Label
            {
                Size = new Size(50, 30),
                Text = "Height:"
            };
            properties.Controls.Add(height);

            TextBox heightText = new TextBox
            {
                Size = new Size(50, 30),
                Text = elements[index].Height.ToString(),
                Location = new Point(height.Location.X + height.Width + 5, height.Location.Y)
            };
            heightText.TextChanged += new EventHandler(HeightText_TextChanged);
            properties.Controls.Add(heightText);

            Label width = new Label
            {
                Size = new Size(50, 30),
                Location = new Point(height.Location.X, height.Location.Y + height.Height + 5),
                Text = "Width:"
            };
            properties.Controls.Add(width);

            TextBox widthText = new TextBox
            {
                Size = new Size(50, 30),
                Text = elements[index].Width.ToString(),
                Location = new Point(width.Location.X + width.Width + 5, width.Height)
            };
            widthText.TextChanged += new EventHandler(WidthText_TextChanged);
            properties.Controls.Add(widthText);

            NoFocusBorderButton colorSwitch = new NoFocusBorderButton();
            properties.Controls.Add(colorSwitch);
            colorSwitch.BackColor = ((PictureBox)elements[index]).BackColor;
            colorSwitch.Size = new Size(30, 30);
            colorSwitch.Location = new Point(width.Location.X, width.Location.Y + width.Height + 5);
            colorSwitch.Click += new EventHandler(Color_Clicked);
        }

        delegate void threadtextboxRefresh();

        private void TextBoxRefreshThread()
        {

            bool invReq = false;
            foreach (TextBoxTool elm in elements.OfType<TextBoxTool>())
            {
                if (elm.InvokeRequired)
                {
                    invReq = true;
                    break;
                }
            }

            if (invReq)
            {
                threadtextboxRefresh callbk = new threadtextboxRefresh(TextBoxRefreshThread);
                this.Invoke(callbk);
            }
            else
            {
                foreach (TextBoxTool obj in elements.OfType<TextBoxTool>())
                {
                    obj.ForceRefresh();
                }
            }

        }

        private void TextBoxRefresh()
        {
            foreach (TextBoxTool obj in elements.OfType<TextBoxTool>())
            {
                obj.ForceRefresh();
            }
        }

        private Button CreateTool(int x, int y, Control f)
        {
            Button bt = new Button
            {
                Location = new Point(x, y),
                Size = new Size(toolBox.Width - 10, toolBox.Width - 10)
            };
            bt.MouseEnter += new EventHandler(Tool_MouseEnter);
            bt.MouseLeave += new EventHandler(Tool_MouseLeave);
            bt.BackColor = idleColor;
            bt.TabStop = false;
            bt.Padding = new Padding(5, 0, 5, 0);
            bt.FlatStyle = FlatStyle.Flat;
            bt.FlatAppearance.BorderSize = 0;
            bt.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
            bt.Click += new System.EventHandler(Tool_Clicked);
            f.Controls.Add(bt);

            return bt;
        }

        #region POPCONTROLS

        Form PopForm(string txt)
        {
            Form f = new Form
            {
                Text = txt,
                FormBorderStyle = FormBorderStyle.Fixed3D
            };
            f.Show();

            return f;
        }

        Form PopForm(string txt, int w, int h)
        {
            Form f = new Form
            {
                Text = txt,
                Size = new Size(w, h)
            };
            f.Show();

            return f;
        }

        Label PopLabel(int x, int y, string txt, Form f)
        {
            Label lb = new Label
            {
                Location = new Point(x, y),
                Text = txt,
                AutoSize = true
            };
            f.Controls.Add(lb);
            return lb;
        }

        TextBox PopText(int x, int y, Form f)
        {
            TextBox t = new TextBox
            {
                Location = new Point(x, y)
            };
            f.Controls.Add(t);
            return t;
        }

        RichTextBox PopRichText(int x, int y, int w, int h, Form f)
        {
            RichTextBox t = new RichTextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, h)
            };
            f.Controls.Add(t);
            return t;
        }

        Button PopButton(int x, int y, string txt, Control f)
        {
            Button bt = new Button
            {
                Location = new Point(x, y),
                Text = txt
            };
            f.Controls.Add(bt);
            return bt;
        }

        GroupBox PopGrBox(int x, int y, string caption, List<string> buttons, Form f)
        {
            GroupBox gb = new GroupBox();
            int i = 0;
            gb.Location = new Point(x, y);
            gb.Text = caption;
            foreach (string s in buttons)
            {
                RadioButton rb = new RadioButton
                {
                    Text = s,

                    Location = new Point(15, (i + 1) * 20)
                };
                gb.Controls.Add(rb);
                rb.Click += new System.EventHandler(SaveType_Changed);
                i++;
                if (i > 3)
                {
                    gb.Height += 20;
                }
            }
            ((RadioButton)gb.Controls[0]).Checked = true;
            f.Controls.Add(gb);
            ((RadioButton)gb.Controls[0]).PerformClick();

            return gb;
        }
        #endregion

        #region FILE BUTTONS

        #region NEW

        private void NewAction()
        {
            if (drag == true)
            {
                if (dragging.IsAlive == true)
                {
                    drag = false;
                }
            }

            DeactivateTools();

            Initialise_workArea();

            Form newScreen = PopForm("New", 400, 300);

            Label title_lb = PopLabel(50, 50, "Project's Title", newScreen);

            System.Windows.Forms.TextBox title = PopText(200, 50, newScreen);

            Label open_lb = PopLabel(50, 100, "Open Picture (Optional)", newScreen);

            Button browse = PopButton(210, 95, "Browse", newScreen);

            browse.Click += new System.EventHandler(BrowseClicked);

            PictureBox thumbnail = new PictureBox
            {
                Visible = false,
                Location = new Point(85, 125),
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 50,
                Height = 50
            };

            newScreen.Controls.Add(thumbnail);

            Button create = PopButton(100, 200, "Create", newScreen);

            create.Click += new System.EventHandler(NewOption_Clicked);
        }

        private void BrowseImage(PictureBox pic)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png, *.jpg, *.jpeg, *.bmp)|*.png; *.jpg; *.jpeg; *.bmp)"
            };

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                // pic.Size = new Size(450, 450);
                Image openImage = Image.FromFile(fileDialog.FileName);
                pic.Visible = true;
                original = (Bitmap)openImage;
                ResizePictureBox(pic, openImage);
                zoomCount = 0;
                workArea.Visible = true;
                EnableMenu(true);
                workArea.BorderStyle = BorderStyle.Fixed3D;
                CenterPictureBox();
                originalSize = workArea.Size;

                properties.Location = new Point(display.Location.X + display.Width + 5, display.Location.Y);
                properties.Size = new Size(properties.Right - 5, display.Height);
                Display_update_background();
            }

        }

        private void EnableMenu(bool active)
        {
            closeToolStripMenuItem.Enabled = active;
            saveAsToolStripMenuItem.Enabled = active;
            saveToolStripMenuItem.Enabled = active;
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

        private void ResizePictureBox(PictureBox pic, Image openImage)
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

        #endregion

        #region OPEN

        private void OpenAction()
        {
            if (drag == true)
            {
                if (dragging.IsAlive == true)
                {
                    drag = false;
                }
            }

            DeactivateTools();
            BrowseImage(workArea);
            if (this.Text.Equals("PhotoMaster"))
            {
                this.Text += " - " + Title;
            }
        }

        private void CenterPictureBox()
        {
            int width = display.Width / 2 - workArea.Width / 2;
            int height = display.Height / 2 - workArea.Height / 2;

            workArea.Location = new Point(width, height);

        }

        #endregion

        #region SAVE AS

        private void SaveAsAction()
        {
            Form saveAsScreen = PopForm("Save As", 500, 500);

            Label path_lb = PopLabel(50, 50, "Save Path:", saveAsScreen);

            System.Windows.Forms.TextBox path = PopText(150, 50, saveAsScreen);
            path.Text = "D:\\";

            Button browse = PopButton(280, 48, "Browse", saveAsScreen);

            browse.Click += new System.EventHandler(BrowseSaveAs_Clicked);

            Label name_lb = PopLabel(50, 100, "File Name:", saveAsScreen);

            System.Windows.Forms.TextBox fname = PopText(150, 100, saveAsScreen);
            fname.Text = Title;
            fname.TextChanged += new EventHandler(Fname_TextChanged);

            Button save = PopButton(150, 150, "Save", saveAsScreen);

            save.Click += new System.EventHandler(Save_Clicked);

            GroupBox savetypes = PopGrBox(150, 200, "Formats", types, saveAsScreen);
        }

        private string GetCheckedType(GroupBox gb)
        {
            foreach (RadioButton c in gb.Controls)
            {
                if (c.Checked)
                {
                    return c.Text;
                }
            }
            return "";
        }

        #endregion

        #region EXIT

        private void CloseApp(object sender, EventArgs e)
        {
            if (SelectingActive == true)
            {
                if (selecting.IsAlive == true)
                {
                    SelectingActive = false;
                }
            }

            if (drag == true)
            {
                if (dragging.IsAlive == true)
                {
                    drag = false;
                }
            }

            this.Close();
        }

        #endregion

        #endregion

        #region EDIT BUTTONS

        bool drag = false;
        bool transform = false;
        int resizingEdge = 5;
        int index = -1;

        private void CopyAction()
        {
            CreateSelection();
            pasteToolStripMenuItem.Enabled = true;
        }

        private void CutAction()
        {
            pasteToolStripMenuItem.Enabled = true;
            if (SelectingActive)
            {
                SelectingActive = false;
                CreateSelection();
                Bitmap cut = new Bitmap(workArea.Image, workArea.Image.Width, workArea.Image.Height);
                Rectangle r = CreateRectangle(selectStart, selectEnd);

                BitmapData bitmapData = cut.LockBits(new Rectangle(0, 0, cut.Width, cut.Height), ImageLockMode.ReadWrite, cut.PixelFormat);
                int bytesPerPixel = Bitmap.GetPixelFormatSize(cut.PixelFormat) / 8;
                int byteCount = bitmapData.Stride * cut.Height;
                byte[] pixels = new byte[byteCount];
                IntPtr ptrFirstPixel = bitmapData.Scan0;
                Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
                int heightInPixels = bitmapData.Height;
                int widthInBytes = bitmapData.Width * bytesPerPixel;

                if (mode == 0)
                {
                    for (int y = (int)(r.Y * (1.0 * workArea.Image.Height / workArea.Height)); y <= (int)((r.Y + r.Height) * (1.0 * workArea.Image.Height / workArea.Height)); y++)
                    {
                        if (y >= 0 && y < cut.Height)
                        {
                            int currentLine = y * bitmapData.Stride;
                            for (int x = (int)(r.X * (1.0 * workArea.Image.Width / workArea.Width)) * bytesPerPixel; x <= (int)((r.X + r.Width) * (1.0 * workArea.Image.Width / workArea.Width)) * bytesPerPixel; x += bytesPerPixel)
                            {
                                if (x >= 0 && x < cut.Width * bytesPerPixel)
                                {
                                    pixels[currentLine + x] = 0;
                                    pixels[currentLine + x + 1] = 0;
                                    pixels[currentLine + x + 2] = 0;
                                    pixels[currentLine + x + 3] = 0;
                                }
                            }
                        }
                    }
                }

                if (mode == 3)
                {
                    Point scaledX = new Point((int)(selectStart.X * (1.0 * workArea.Image.Width / workArea.Width)), (int)(selectStart.Y * (1.0 * workArea.Image.Height / workArea.Height)));
                    Point scaledY = new Point((int)(selectEnd.X * (1.0 * workArea.Image.Width / workArea.Width)), (int)(selectEnd.Y * (1.0 * workArea.Image.Height / workArea.Height)));
                    Rectangle rscaled = CreateRectangle(scaledX, scaledY);
                    for (int y = (int)(r.Y * (1.0 * workArea.Image.Height / workArea.Height)); y <= (int)((r.Y + r.Height) * (1.0 * workArea.Image.Height / workArea.Height)); y++)
                    {
                        if (y >= 0 && y < cut.Height)
                        {
                            int currentLine = y * bitmapData.Stride;
                            for (int x = (int)(r.X * (1.0 * workArea.Image.Width / workArea.Width)) * bytesPerPixel; x <= (int)((r.X + r.Width) * (1.0 * workArea.Image.Width / workArea.Width)) * bytesPerPixel; x += bytesPerPixel)
                            {
                                if (PointInEllipse(x / bytesPerPixel, y, rscaled) && (x >= 0 && x < cut.Width * bytesPerPixel))
                                {
                                    pixels[currentLine + x] = 0;
                                    pixels[currentLine + x + 1] = 0;
                                    pixels[currentLine + x + 2] = 0;
                                    pixels[currentLine + x + 3] = 0;
                                }
                            }
                        }
                    }
                }
                Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
                cut.UnlockBits(bitmapData);

                workArea.Image = cut;
                Display_update_background();

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
                {
                    index--;
                }
            }
        }

        private void PasteAction()
        {
            PictureBox copy = new PictureBox
            {
                Size = copiedImage.Size
            };
            copy.Location = new Point((display.Width - copy.Width) / 2, (display.Height - copy.Height) / 2);
            copy.BackColor = Color.Transparent;
            copy.Image = copiedImage;
            copy.Visible = true;
            display.Controls.Add(copy);
            copy.BringToFront();
            AddElement(copy);
            SelectingActive = false;
            TextBoxRefresh();

            copy.MouseDown += new MouseEventHandler(Move_MouseDown);
            copy.MouseMove += new MouseEventHandler(Move_MouseMove);
            copy.MouseUp += new MouseEventHandler(Move_MouseUp);
            copy.MouseClick += new MouseEventHandler(Copy_MouseClick);
        }

        private void AddElement(Control element)
        {
            elements.Add(element);
            index = elements.Count - 1;
            if (index == 0)
            {
                deleteToolStripMenuItem.Enabled = true;
                properties.Visible = true;
            }
        }

        private void DeleteAction()
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

        private void CreateSelection()
        {
            Rectangle r = CreateRectangle(selectStart, selectEnd);
            Bitmap original = new Bitmap(workArea.Image, workArea.Width, workArea.Height);

            copiedImage = new Bitmap(r.Width, r.Height);

            using (Graphics g = Graphics.FromImage(copiedImage))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.DrawImage(original, 0, 0, r, GraphicsUnit.Pixel);
            }

            if (mode == 3)
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

        private bool PointInEllipse(int x, int y, Rectangle r)
        {
            return ((Math.Pow(Math.Abs(x - r.Width * 1.0 / 2 - r.X), 2) / Math.Pow(r.Width * 1.0 / 2, 2) + Math.Pow(Math.Abs(y - r.Height * 1.0 / 2 - r.Y), 2) / Math.Pow(r.Height * 1.0 / 2, 2)) < 1);
        }

        private void TransformSelection(Point location)
        {
            PictureBox copy = (PictureBox)elements[index];
            moveNewPosition = Point.Empty;
            moveNewPosition.X = location.X - movePosition.X;
            moveNewPosition.Y = location.Y - movePosition.Y;
            double ratio = 0;
            if (copy.Width > copy.Height)
            {
                if (copy.Height != 0)
                {
                    ratio = copy.Width / copy.Height;
                }
            }
            else
            {
                if (copy.Width != 0)
                {
                    ratio = copy.Height / copy.Width;
                }
            }

            //top left
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
            //bottom right
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
            //top right
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
            //bottom left
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
            //left
            else if (transformPosition.X < resizingEdge)
            {
                copy.Left += moveNewPosition.X;
                copy.Width -= moveNewPosition.X;
            }
            //right
            else if (copy.Cursor == Cursors.SizeWE)
            {
                copy.Width = moveNewPosition.X + startSize.Width;
            }
            //top
            else if (transformPosition.Y < resizingEdge)
            {
                copy.Top += moveNewPosition.Y;
                copy.Height -= moveNewPosition.Y;
            }
            //bottom
            else if (copy.Cursor == Cursors.SizeNS)
            {
                copy.Height = moveNewPosition.Y + startSize.Height;
            }
        }

        private void ChangeTransformCursors()
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

        private void DragPicture()
        {
            timerMove.Start();
            do
            {
                if (timerMove.ElapsedMilliseconds > 50)
                {
                    timerMove.Restart();
                }
            } while (drag);
            timerMove.Reset();
        }

        #endregion

        #region EFFECTS

        private void GrayscaleThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(ConvertGrayscale);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
            {
                convert.Start(picture);
            }
        }

        delegate void threadPictureRefreshCallBack(PictureBox picture);

        private void InvokeRefresh(PictureBox picture)
        {
            if (picture.InvokeRequired)
            {
                threadPictureRefreshCallBack callbk = new threadPictureRefreshCallBack(InvokeRefresh);
                picture.Invoke(callbk, new object[] { picture });
            }
            else
            {
                picture.Refresh();
            }
        }

        delegate void threadPictureConvertCallBack(PictureBox picture, bool done);

        private void InvokeConvertStatus(PictureBox picture, bool done)
        {
            if (picture.InvokeRequired)
            {
                threadPictureConvertCallBack callbk = new threadPictureConvertCallBack(InvokeConvertStatus);
                picture.Invoke(callbk, new object[] { picture, done });
            }
            else
            {
                picture.Enabled = done;
                toolBox.Enabled = done;
            }
        }

        private void ConvertGrayscale(object pic)
        {
            PictureBox picture = (PictureBox)pic;

            InvokeConvertStatus(picture, false);

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


            for (int y = 0; y < heightInPixels; y++)
            {
                int currentLine = y * bitmapData.Stride;
                for (int x = 0; x < widthInBytes; x += bytesPerPixel)
                {
                    int B = pixels[currentLine + x], G = pixels[currentLine + x + 1], R = pixels[currentLine + x + 2], avg = (B + G + R) / 3;

                    pixels[currentLine + x] = (byte)avg;
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
            InvokeConvertStatus(picture, true);
        }

        private void RedFilterThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(RedFilter);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
            {
                convert.Start(picture);
            }
        }

        private void RedFilter(object pic)
        {
            PictureBox picture = (PictureBox)pic;

            InvokeConvertStatus(picture, false);

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
                    int R = pixels[currentLine + x + 2];

                    pixels[currentLine + x] = 0;
                    pixels[currentLine + x + 1] = 0;
                    pixels[currentLine + x + 2] = (byte)R;

                }
            }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;

            InvokeConvertStatus(picture, true);
        }

        private void GreenFilterThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(GreenFilter);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
            {
                convert.Start(picture);
            }
        }

        private void GreenFilter(object pic)
        {
            PictureBox picture = (PictureBox)pic;

            InvokeConvertStatus(picture, false);

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
            InvokeConvertStatus(picture, true);
        }

        private void BlueFilterThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(BlueFilter);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
            {
                convert.Start(picture);
            }
        }

        private void BlueFilter(object pic)
        {
            PictureBox picture = (PictureBox)pic;

            InvokeConvertStatus(picture, false);

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
                    pixels[currentLine + x + 2] = 0;

                }
            }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;
            InvokeConvertStatus(picture, true);
        }

        private void InvertThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(InvertColors);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
            {
                convert.Start(picture);
            }
        }

        private void InvertColors(object pic)
        {
            PictureBox picture = (PictureBox)pic;
            InvokeConvertStatus(picture, false);
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

                    pixels[currentLine + x] = (byte)(255 - B);
                    pixels[currentLine + x + 1] = (byte)(255 - G);
                    pixels[currentLine + x + 2] = (byte)(255 - R);

                }
            }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;
            InvokeConvertStatus(picture, true);
        }

        private void SepiaThread(PictureBox picture)
        {
            ParameterizedThreadStart th = new ParameterizedThreadStart(Sepia);
            Thread convert = new Thread(th);
            if (!convert.IsAlive)
            {
                convert.Start(picture);
            }
        }

        private void Sepia(object pic)
        {
            PictureBox picture = (PictureBox)pic;
            InvokeConvertStatus(picture, false);
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
                    int B = pixels[currentLine + x], G = pixels[currentLine + x + 1], R = pixels[currentLine + x + 2], avg = (R + G + B) / 3;

                    R = B = G = avg;
                    R += 40;
                    G += 20;

                    if (R > 255)
                    {
                        R = 255;
                    }

                    if (G > 255)
                    {
                        G = 255;
                    }

                    if (B > 255)
                    {
                        B = 255;
                    }

                    B -= 30;

                    if (B < 0)
                    {
                        B = 0;
                    }

                    if (B > 255)
                    {
                        B = 255;
                    }

                    pixels[currentLine + x] = (byte)B;
                    pixels[currentLine + x + 1] = (byte)G;
                    pixels[currentLine + x + 2] = (byte)R;

                }
            }

            Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
            originalimg.UnlockBits(bitmapData);

            picture.Image = originalimg;

            InvokeConvertStatus(picture, true);
        }

        #endregion

        #region VIEW BUTTONS

        private void FullScreenAction(FormBorderStyle newBorderStyle, FormWindowState formWindowState, bool newFullScreenActive)
        {
            this.FormBorderStyle = newBorderStyle;
            this.WindowState = formWindowState;
            fullscreenVisible = newFullScreenActive;
            fullScreenToolStripMenuItem.Visible = newFullScreenActive;
            exitFullScreenToolStripMenuItem.Visible = !newFullScreenActive;
        }

        private void ZoomInAction()
        {
            if (zoomCount < 25)
            {
                zoomCount++;
                ZoomIn();
                Display_update_background();
            }
        }

        private void ZoomOutAction()
        {
            if (zoomCount > -15)
            {
                zoomCount--;
                ZoomOut();
                Display_update_background();
            }
        }

        private void ResizeImage(Image image, int width, int height)
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

        private void ZoomIn()
        {
            int x = display.AutoScrollPosition.X, y = display.AutoScrollPosition.Y;

            ResizeImage(workArea.Image, (int)(originalSize.Width * Math.Pow(multiplier, zoomCount)), (int)(originalSize.Height * Math.Pow(multiplier, zoomCount)));
            CenterPictureBox();
            ResizePictureBox(workArea, workArea.Image);

            if (workArea.Width > display.ClientSize.Width || workArea.Height > display.ClientSize.Height)
            {
                display.AutoScrollPosition = new Point(0, 0);
            }

            if (workArea.Width > display.ClientSize.Width)
            {
                workArea.Left = 0;
            }

            if (workArea.Height > display.ClientSize.Height)
            {
                workArea.Top = 0;
            }

            display.AutoScrollPosition = new Point((int)(Math.Abs(x) * multiplier + 0.5 * (multiplier - 1) * display.Width), (int)(Math.Abs(y) * multiplier + 0.5 * (multiplier - 1) * display.Height));
        }

        private void ZoomIn(Point mouse)
        {

            int scrollhorriz = display.AutoScrollPosition.X, scrollvert = display.AutoScrollPosition.Y;
            ResizeImage(workArea.Image, (int)(originalSize.Width * Math.Pow(multiplier, zoomCount)), (int)(originalSize.Height * Math.Pow(multiplier, zoomCount)));
            CenterPictureBox();
            ResizePictureBox(workArea, workArea.Image);

            int x = mouse.X, y = mouse.Y;

            if (workArea.Width > display.ClientSize.Width || workArea.Height > display.ClientSize.Height)
            {
                display.AutoScrollPosition = new Point(0, 0);
            }

            if (workArea.Width > display.ClientSize.Width)
            {
                workArea.Left = 0;
            }

            if (workArea.Height > display.ClientSize.Height)
            {
                workArea.Top = 0;
            }

            display.AutoScrollPosition = new Point((int)(Math.Abs(x) * multiplier - x - scrollhorriz), (int)(Math.Abs(y) * multiplier - y - scrollvert));
        }

        private void ZoomOut()

        {
            int x = display.AutoScrollPosition.X, y = display.AutoScrollPosition.Y;

            ResizeImage(workArea.Image, (int)(originalSize.Width * Math.Pow(multiplier, zoomCount)), (int)(originalSize.Height * Math.Pow(multiplier, zoomCount)));
            CenterPictureBox();
            ResizePictureBox(workArea, workArea.Image);

            if (workArea.Width > display.ClientSize.Width || workArea.Height > display.ClientSize.Height)
            {
                display.AutoScrollPosition = new Point(0, 0);
            }

            if (workArea.Width > display.ClientSize.Width)
            {
                workArea.Left = 0;
            }

            if (workArea.Height > display.ClientSize.Height)
            {
                workArea.Top = 0;
            }

            display.AutoScrollPosition = new Point((int)(Math.Abs(x) / multiplier - 0.5 * (multiplier - 1) * display.Width), (int)(Math.Abs(y) / multiplier - 0.5 * (multiplier - 1) * display.Height));
        }

        private void ZoomOut(Point mouse)
        {
            ResizeImage(workArea.Image, (int)(originalSize.Width * Math.Pow(multiplier, zoomCount)), (int)(originalSize.Height * Math.Pow(multiplier, zoomCount)));
            CenterPictureBox();
            ResizePictureBox(workArea, workArea.Image);
            int x = mouse.X, y = mouse.Y;
            int scrollhorriz = display.AutoScrollPosition.X, scrollvert = display.AutoScrollPosition.Y;
            if (workArea.Width > display.ClientSize.Width || workArea.Height > display.ClientSize.Height)
            {
                display.AutoScrollPosition = new Point(0, 0);
            }

            if (workArea.Width > display.ClientSize.Width)
            {
                workArea.Left = 0;
            }

            if (workArea.Height > display.ClientSize.Height)
            {
                workArea.Top = 0;
            }

            display.AutoScrollPosition = new Point((int)(Math.Abs(x) / multiplier - x - scrollhorriz), (int)(Math.Abs(y) / multiplier - y - scrollvert));
        }

        #endregion

        #region SELECT BUTTONS

        private void AllAction()
        {
            SelectingActive = false;
            selecting = new Thread(DrawSelectionThread);
            selectStart = new Point(0, 0);
            selectEnd = new Point(workArea.Width - 1, workArea.Height - 1);
            selectionBox.Invalidate();
            TextBoxRefresh();
            selecting.Start();
        }

        #endregion

        private void Display_update_background()
        {
            Bitmap copy = new Bitmap(display.Width, display.Height);
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.DrawImage(workArea.Image, CreateRectangle(workArea.Location, new Point(workArea.Location.X + workArea.Width, workArea.Location.Y + workArea.Height)));
                //g.DrawImage(workArea.Image, workArea.Location);
                //g.DrawImage(workArea.Image, new Point[] { workArea.Location, new Point(workArea.Location.X + workArea.Width, workArea.Location.Y), new Point(workArea.Location.X, workArea.Location.Y + workArea.Height), new Point(workArea.Location.X + workArea.Width, workArea.Location.Y + workArea.Height) });
                display.BackgroundImage = copy;
            }
        }

        private Button PopButton(int x, int y, Control f)
        {
            Button bt = new Button
            {
                Location = new Point(x, y),
                Padding = new Padding(5, 0, 5, 0),
                TabStop = false,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(33, 33)
            };
            bt.FlatAppearance.BorderSize = 0;
            bt.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
            bt.MouseEnter += new EventHandler(Tool_MouseEnter);
            bt.MouseLeave += new EventHandler(Tool_MouseLeave);
            bt.BackColor = idleColor;
            f.Controls.Add(bt);
            return bt;
        }

        private void CustomizeButton(ref int i, String buttonTypeName, EventHandler eventFunction, bool buttonEnabled, Bitmap buttonActive, Bitmap buttonInactive)
        {
            Button newButton = PopButton(i * 35, 0, toolBar);
            toolTip.SetToolTip(newButton, buttonTypeName);
            newButton.Click += eventFunction;
            newButton.Enabled = buttonEnabled;
            if (buttonEnabled)
            {
                newButton.BackgroundImage = buttonActive;
            }
            else
            {
                newButton.BackgroundImage = buttonInactive;
            }

            i++;
        }

        private void ToolBarControlEnabledChange(int activeToolBarIndex, int buttonIndex, bool buttonEnabled, Bitmap buttonActive, Bitmap buttonInactive)
        {
            if (activeToolBar == activeToolBarIndex)
            {
                toolBar.Controls[buttonIndex].Enabled = buttonEnabled;
                if (buttonEnabled)
                {
                    toolBar.Controls[buttonIndex].BackgroundImage = buttonActive;
                }
                else
                {
                    toolBar.Controls[buttonIndex].BackgroundImage = buttonInactive;
                }
            }
        }

        private void ToolBar_Reset()
        {
            foreach (Button bt in toolBar.Controls)
            {
                toolBar.Controls.Remove(bt);
                bt.Dispose();
            }
            toolBar.Controls.Clear();
        }
        
    }
}
