using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Project
{
    public class Shape : PictureBox
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

        public Shape(Point start, Point end, Control parent)
        {
            Rectangle drawingLimits = CreateRectangle(start, end);
            parent.Controls.Add(this);
            this.BringToFront();
            this.Size = new Size(drawingLimits.Width, drawingLimits.Height);
            this.Location = new Point(drawingLimits.X + parent.Controls[parent.Controls.Count - 1].Left, drawingLimits.Y + parent.Controls[parent.Controls.Count - 1].Top);

            this.MouseDown += new MouseEventHandler(Move_MouseDown);
            this.MouseMove += new MouseEventHandler(Move_MouseMove);
            this.MouseUp += new MouseEventHandler(Move_MouseUp);
        }

        private void Move_MouseUp(object sender, MouseEventArgs e)
        {
            dragShape = false;
            transformShape = false;
            initialPosition = Point.Empty;
            moveNewPosition = Point.Empty;
        }

        private void Move_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragShape)
            {
                finalPosition = e.Location;
                ChangeTransformCursors((Shape)sender);
            }
            if (dragShape && e.Button == MouseButtons.Left)
            {
                if (transformShape && ((Control)sender).Focused)
                {
                    TransformSelection(e.Location, ((Shape)sender));
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

        private void ChangeTransformCursors(Shape sender)
        {
            transformShape = true;
            if ((finalPosition.X < resizingEdge && finalPosition.Y < resizingEdge) || (finalPosition.X > sender.Width - resizingEdge && finalPosition.Y > sender.Height - resizingEdge))
            {
                if (sender.Cursor != Cursors.SizeNWSE)
                {
                    sender.Cursor = Cursors.SizeNWSE;
                }
            }
            else if ((finalPosition.X > sender.Width - resizingEdge && finalPosition.Y < resizingEdge) || (finalPosition.X < resizingEdge && finalPosition.Y > sender.Height - resizingEdge))
            {
                if (sender.Cursor != Cursors.SizeNESW)
                {
                    sender.Cursor = Cursors.SizeNESW;
                }
            }
            else if (finalPosition.X < resizingEdge || finalPosition.X > sender.Width - resizingEdge)
            {
                if (sender.Cursor != Cursors.SizeWE)
                {
                    sender.Cursor = Cursors.SizeWE;
                }
            }
            else if (finalPosition.Y < resizingEdge || finalPosition.Y > sender.Height - resizingEdge)
            {
                if (sender.Cursor != Cursors.SizeNS)
                {
                    sender.Cursor = Cursors.SizeNS;
                }
            }
            else
            {
                if (sender.Cursor != Cursors.SizeAll)
                {
                    sender.Cursor = Cursors.SizeAll;
                }

                transformShape = false;
            }
        }

        private void TransformSelection(Point location, Shape sender)
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

        private void Move_MouseDown(object sender, MouseEventArgs e)
        {
            ((Control)sender).Focus();
            dragShape = true;
            initialPosition = e.Location;
            draggingShape = new Thread(DragPicture);
            draggingShape.Start();
            initialSize = ((Shape)sender).Size;
            ((Control)sender).BringToFront();
            // ((Shape)sender).BringToFront();
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
            } while (dragShape);
            timerMove.Reset();
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

        protected void Message(string x)
        {
            MessageBox.Show(x);
        }

    }
}
