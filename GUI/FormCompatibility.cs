using System.Drawing;
using System.Windows.Forms;

namespace CKAN
{
    /// <summary>
    /// Inheriting from this class ensures that forms are equally sized on Windows and on Linux/MacOSX
    /// Choose the form size so that it is the right one for Windows.
    /// </summary>
    public class FormCompatibility : Form
    {
        private const int formHeightDifference = 24;

        protected override bool ProcessDialogKey (Keys keyData)
        {
            if (Form.ModifierKeys == Keys.None && keyData == Keys.Escape) {
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        public void ApplyFormCompatibilityFixes()
        {
            if (!Platform.IsWindows)
            {
                ClientSize = new Size(ClientSize.Width, ClientSize.Height + formHeightDifference);      
            }
        }

    }

}
