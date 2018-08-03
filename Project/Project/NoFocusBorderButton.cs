using System.Windows.Forms;

namespace Project
{
    public class NoFocusBorderButton : Button
    {
        public NoFocusBorderButton() : base()
        {
            this.SetStyle(ControlStyles.Selectable, false);
        }
    }

}
