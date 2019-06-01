using System.Windows.Forms;

namespace CKAN
{
    public partial class AskUserForAutoUpdatesDialog : Form
    {
        public AskUserForAutoUpdatesDialog()
        {
            InitializeComponent();
        }
        protected override bool ProcessDialogKey (Keys keyData)
        {
            if (Form.ModifierKeys == Keys.None && keyData == Keys.Escape) {
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

    }
}
