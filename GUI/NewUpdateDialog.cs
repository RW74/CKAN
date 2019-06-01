using System.Windows.Forms;

namespace CKAN
{
    public partial class NewUpdateDialog : Form
    {
        protected override bool ProcessDialogKey (Keys keyData)
        {
            if (Form.ModifierKeys == Keys.None && keyData == Keys.Escape) {
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        public NewUpdateDialog (string version, string releaseNotes)
        {
            InitializeComponent();

            VersionLabel.Text = version;
            ReleaseNotesTextbox.Text = releaseNotes;
        }
    }
}
