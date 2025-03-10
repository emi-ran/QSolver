using System.Windows.Forms;

namespace QSolver
{
    public class DoubleBufferedForm : Form
    {
        public DoubleBufferedForm()
        {
            this.DoubleBuffered = true;
        }
    }
}