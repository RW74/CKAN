using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using CKAN.Versioning;
using log4net;

namespace CKAN
{
    public partial class SettingsDialog : Form
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SettingsDialog));

        private long m_cacheSize;
        private int m_cacheFileCount;
        private Win32Registry winReg;

        private List<Repository> _sortedRepos = new List<Repository>();

        /// <summary>
        /// Initialize a settings window
        /// </summary>
        public SettingsDialog()
        {
            InitializeComponent();
            if (Platform.IsMono)
            {
                this.ClearCacheMenu.Renderer = new FlatToolStripRenderer();
            }
            winReg = new Win32Registry();
        }

        protected override bool ProcessDialogKey (Keys keyData)
        {
            if (Form.ModifierKeys == Keys.None && keyData == Keys.Escape) {
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        private void SettingsDialog_Load(object sender, EventArgs e)
        {
            UpdateDialog();
        }

        public void UpdateDialog()
        {
            RefreshReposListBox();
            RefreshAuthTokensListBox();

            LocalVersionLabel.Text = Meta.GetVersion();

            CheckUpdateOnLaunchCheckbox.Checked = Main.Instance.configuration.CheckForUpdatesOnLaunch;
            RefreshOnStartupCheckbox.Checked = Main.Instance.configuration.RefreshOnStartup;
            HideEpochsCheckbox.Checked = Main.Instance.configuration.HideEpochs;
            HideVCheckbox.Checked = Main.Instance.configuration.HideV;
            AutoSortUpdateCheckBox.Checked = Main.Instance.configuration.AutoSortByUpdate;
            EnableTrayIconCheckBox.Checked = MinimizeToTrayCheckBox.Enabled = Main.Instance.configuration.EnableTrayIcon;
            MinimizeToTrayCheckBox.Checked = Main.Instance.configuration.MinimizeToTray;
            PauseRefreshCheckBox.Checked = Main.Instance.configuration.RefreshPaused;

            UpdateRefreshRate();

            UpdateCacheInfo(winReg.DownloadCacheDir);
            if (winReg.CacheSizeLimit.HasValue)
            {
                // Show setting in MB
                CacheLimit.Text = (winReg.CacheSizeLimit.Value / 1024 / 1024).ToString();
            }
        }

        private void UpdateRefreshRate()
        {
            int rate = winReg.RefreshRate;
            RefreshTextBox.Text = rate.ToString();
            PauseRefreshCheckBox.Enabled = rate != 0;
            Main.Instance.pauseToolStripMenuItem.Enabled = winReg.RefreshRate != 0;
            Main.Instance.UpdateRefreshTimer();
        }

        private void RefreshReposListBox()
        {
            // Give the Repository the priority it
            // currently has in the gui
            for (int i = 0; i < _sortedRepos.Count; i++)
            {
                _sortedRepos[i].priority = i;
            }

            var manager = RegistryManager.Instance(Main.Instance.CurrentInstance);
            var registry = manager.registry;
            _sortedRepos = new List<Repository>(registry.Repositories.Values);

            _sortedRepos.Sort((repo1, repo2) => repo1.priority.CompareTo(repo2.priority));
            ReposListBox.Items.Clear();
            foreach (var repo in _sortedRepos)
            {
                ReposListBox.Items.Add(string.Format("{0} | {1}", repo.name, repo.uri));
            }

            manager.Save();
        }

        private void UpdateCacheInfo(string newPath)
        {
            string failReason;
            if (newPath == winReg.DownloadCacheDir
                || Main.Instance.Manager.TrySetupCache(newPath, out failReason))
            {
                Main.Instance.Manager.Cache.GetSizeInfo(out m_cacheFileCount, out m_cacheSize);
                CachePath.Text = winReg.DownloadCacheDir;
                CacheSummary.Text = $"{m_cacheFileCount} files, {CkanModule.FmtSize(m_cacheSize)}";
                CacheSummary.ForeColor   = SystemColors.ControlText;
                OpenCacheButton.Enabled  = true;
                ClearCacheButton.Enabled = (m_cacheSize > 0);
                PurgeToLimitMenuItem.Enabled = (winReg.CacheSizeLimit.HasValue
                    && m_cacheSize > winReg.CacheSizeLimit.Value);
            }
            else
            {
                CacheSummary.Text        = $"Invalid path: {failReason}";
                CacheSummary.ForeColor   = Color.Red;
                OpenCacheButton.Enabled  = false;
                ClearCacheButton.Enabled = false;
            }
        }

        private void CachePath_TextChanged(object sender, EventArgs e)
        {
            UpdateCacheInfo(CachePath.Text);
        }

        private void CacheLimit_TextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(CacheLimit.Text))
            {
                winReg.CacheSizeLimit = null;
            }
            else
            {
                // Translate from MB to bytes
                winReg.CacheSizeLimit = Convert.ToInt64(CacheLimit.Text) * 1024 * 1024;
            }
            UpdateCacheInfo(CachePath.Text);
        }

        private void CacheLimit_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void ChangeCacheButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog cacheChooser = new FolderBrowserDialog()
            {
                Description         = "Choose a folder for storing CKAN's mod downloads:",
                RootFolder          = Environment.SpecialFolder.MyComputer,
                SelectedPath        = winReg.DownloadCacheDir,
                ShowNewFolderButton = true
            };
            DialogResult result = cacheChooser.ShowDialog();
            if (result == DialogResult.OK)
            {
                UpdateCacheInfo(cacheChooser.SelectedPath);
            }
        }

        private void PurgeToLimitMenuItem_Click(object sender, EventArgs e)
        {
            // Purge old downloads if we're over the limit
            if (winReg.CacheSizeLimit.HasValue)
            {
                Main.Instance.Manager.Cache.EnforceSizeLimit(
                    winReg.CacheSizeLimit.Value,
                    RegistryManager.Instance(Main.Instance.CurrentInstance).registry
                );
                UpdateCacheInfo(winReg.DownloadCacheDir);
            }
        }

        private void PurgeAllMenuItem_Click(object sender, EventArgs e)
        {
            YesNoDialog deleteConfirmationDialog = new YesNoDialog();
            string confirmationText = String.Format
            (
                "Do you really want to delete {0} cached files, freeing {1}?",
                m_cacheFileCount,
                CkanModule.FmtSize(m_cacheSize)
            );

            if (deleteConfirmationDialog.ShowYesNoDialog(confirmationText) == DialogResult.Yes)
            {
                // tell the cache object to nuke itself
                Main.Instance.Manager.Cache.RemoveAll();

                // forcibly tell all mod rows to re-check cache state
                foreach (DataGridViewRow row in Main.Instance.ModList.Rows)
                {
                    var mod = row.Tag as GUIMod;
                    mod?.UpdateIsCached();
                }

                // finally, clear the preview contents list
                Main.Instance.UpdateModContentsTree(null, true);

                UpdateCacheInfo(winReg.DownloadCacheDir);
            }
        }

        private void ResetCacheButton_Click(object sender, EventArgs e)
        {
            // Reset to default cache path
            UpdateCacheInfo("");
        }

        private void OpenCacheButton_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName        = winReg.DownloadCacheDir,
                UseShellExecute = true,
                Verb            = "open"
            });
        }

        private void ReposListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            DeleteRepoButton.Enabled = ReposListBox.SelectedItem != null;

            if (ReposListBox.SelectedIndex > 0 && ReposListBox.SelectedIndex < ReposListBox.Items.Count)
            {
                UpRepoButton.Enabled = true;
            }
            else
            {
                UpRepoButton.Enabled = false;
            }

            if (ReposListBox.SelectedIndex  < ReposListBox.Items.Count - 1 && ReposListBox.SelectedIndex >= 0)
            {
                DownRepoButton.Enabled = true;
            }
            else
            {
                DownRepoButton.Enabled = false;
            }
        }

        private void DeleteRepoButton_Click(object sender, EventArgs e)
        {
            if (ReposListBox.SelectedItem == null)
            {
                return;
            }

            var item = _sortedRepos[ReposListBox.SelectedIndex];
            var registry = RegistryManager.Instance(Main.Instance.CurrentInstance).registry;
            registry.Repositories.Remove(item.name);
            RefreshReposListBox();
            DeleteRepoButton.Enabled = false;
        }

        private void NewRepoButton_Click(object sender, EventArgs e)
        {
            var dialog = new NewRepoDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var repo = dialog.RepoUrlTextBox.Text.Split('|');
                    var name = repo[0].Trim();
                    var url = repo[1].Trim();

                    var registry = RegistryManager.Instance(Main.Instance.CurrentInstance).registry;
                    SortedDictionary<string, Repository> repositories = registry.Repositories;
                    if (repositories.ContainsKey(name))
                    {
                        repositories.Remove(name);
                    }

                    repositories.Add(name, new Repository(name, url, _sortedRepos.Count));
                    registry.Repositories = repositories;

                    RefreshReposListBox();
                }
                catch (Exception)
                {
                    Main.Instance.currentUser.RaiseError("Invalid repo format - should be \"<name> | <url>\"");
                }
            }
        }

        private void UpRepoButton_Click(object sender, EventArgs e)
        {
            if (ReposListBox.SelectedItem == null)
            {
                return;
            }

            if (ReposListBox.SelectedIndex == 0)
            {
                return;
            }

            var item = _sortedRepos[ReposListBox.SelectedIndex];
            _sortedRepos.RemoveAt(ReposListBox.SelectedIndex);
            _sortedRepos.Insert(ReposListBox.SelectedIndex - 1, item);
            RefreshReposListBox();
        }

        private void DownRepoButton_Click(object sender, EventArgs e)
        {
            if (ReposListBox.SelectedItem == null)
            {
                return;
            }

            if (ReposListBox.SelectedIndex == ReposListBox.Items.Count - 1)
            {
                return;
            }

            var item = _sortedRepos[ReposListBox.SelectedIndex];
            _sortedRepos.RemoveAt(ReposListBox.SelectedIndex);
            _sortedRepos.Insert(ReposListBox.SelectedIndex + 1, item);
            RefreshReposListBox();
        }

        private void RefreshAuthTokensListBox()
        {
            AuthTokensListBox.Items.Clear();
            foreach (string host in Win32Registry.GetAuthTokenHosts())
            {
                string token;
                if (Win32Registry.TryGetAuthToken(host, out token))
                {
                    AuthTokensListBox.Items.Add(string.Format("{0} | {1}", host, token));
                }
            }
        }

        private void AuthTokensListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            DeleteAuthTokenButton.Enabled = AuthTokensListBox.SelectedItem != null;
        }

        private void NewAuthTokenButton_Click(object sender, EventArgs e)
        {
            // Inspired by https://stackoverflow.com/a/17546909/2422988
            Form newAuthTokenPopup = new Form()
            {
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                StartPosition   = FormStartPosition.CenterParent,
                ClientSize      = new Size(300, 100),
                Text            = "Add Authentication Token"
            };
            Label hostLabel = new Label()
            {
                AutoSize = true,
                Location = new Point(3, 6),
                Size     = new Size(271, 13),
                Text     = "Host:"
            };
            TextBox hostTextBox = new TextBox()
            {
                Location = new Point(45, 6),
                Size     = new Size(newAuthTokenPopup.ClientSize.Width - 40 - 10, 23),
                Text     = ""
            };
            Label tokenLabel = new Label()
            {
                AutoSize = true,
                Location = new Point(3, 35),
                Size     = new Size(271, 13),
                Text     = "Token:"
            };
            TextBox tokenTextBox = new TextBox()
            {
                Location = new Point(45, 35),
                Size     = new Size(newAuthTokenPopup.ClientSize.Width - 40 - 10, 23),
                Text     = ""
            };
            Button acceptButton = new Button()
            {
                DialogResult = DialogResult.OK,
                Name         = "okButton",
                Size         = new Size(75, 23),
                Text         = "&Accept",
                Location     = new Point((newAuthTokenPopup.ClientSize.Width - 80 - 80) / 2, 64)
            };
            acceptButton.Click += (origin, evt) =>
            {
                newAuthTokenPopup.DialogResult = validNewAuthToken(hostTextBox.Text, tokenTextBox.Text)
                    ? DialogResult.OK
                    : DialogResult.None;
            };
            Button cancelButton = new Button()
            {
                DialogResult = DialogResult.Cancel,
                Name         = "cancelButton",
                Size         = new Size(75, 23),
                Text         = "&Cancel",
                Location     = new Point(acceptButton.Location.X + acceptButton.Size.Width + 5, 64)
            };

            newAuthTokenPopup.Controls.Add(hostLabel);
            newAuthTokenPopup.Controls.Add(hostTextBox);
            newAuthTokenPopup.Controls.Add(tokenLabel);
            newAuthTokenPopup.Controls.Add(tokenTextBox);
            newAuthTokenPopup.Controls.Add(acceptButton);
            newAuthTokenPopup.Controls.Add(cancelButton);
            newAuthTokenPopup.AcceptButton = acceptButton;
            newAuthTokenPopup.CancelButton = cancelButton;

            switch (newAuthTokenPopup.ShowDialog(this))
            {
                case DialogResult.Abort:
                case DialogResult.Cancel:
                case DialogResult.Ignore:
                case DialogResult.No:
                    // User cancelled out, so do nothing
                    break;

                case DialogResult.OK:
                case DialogResult.Yes:
                    Win32Registry.SetAuthToken(hostTextBox.Text, tokenTextBox.Text);
                    RefreshAuthTokensListBox();
                    break;
            }
        }

        private static bool validNewAuthToken(string host, string token)
        {
            if (host.Length <= 0)
            {
                GUI.user.RaiseError("Host field is required.");
                return false;
            }
            if (token.Length <= 0)
            {
                GUI.user.RaiseError("Token field is required.");
                return false;
            }
            if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
            {
                GUI.user.RaiseError("{0} is not a valid host name.", host);
                return false;
            }
            string oldToken;
            if (Win32Registry.TryGetAuthToken(host, out oldToken))
            {
                GUI.user.RaiseError("{0} already has an authentication token.", host);
                return false;
            }

            return true;
        }

        private void DeleteAuthTokenButton_Click(object sender, EventArgs e)
        {
            if (AuthTokensListBox.SelectedItem != null)
            {
                string item = AuthTokensListBox.SelectedItem as string;
                string host = item?.Split('|')[0].Trim();

                Win32Registry.SetAuthToken(host, null);
                RefreshAuthTokensListBox();
                DeleteRepoButton.Enabled = false;
            }
        }

        private void CheckForUpdatesButton_Click(object sender, EventArgs e)
        {
            try
            {
                AutoUpdate.Instance.FetchLatestReleaseInfo();
                var latestVersion = AutoUpdate.Instance.latestUpdate.Version;
                if (latestVersion.IsGreaterThan(new ModuleVersion(Meta.GetVersion(VersionFormat.Short))) && AutoUpdate.Instance.IsFetched())
                {
                    InstallUpdateButton.Enabled = true;
                }
                else
                {
                    InstallUpdateButton.Enabled = false;
                }

                LatestVersionLabel.Text = latestVersion.ToString();
            }
            catch (Exception ex)
            {
                log.Warn("Exception caught in CheckForUpdates:\r\n" + ex);
            }
        }

        private void InstallUpdateButton_Click(object sender, EventArgs e)
        {
            if (AutoUpdate.CanUpdate)
            {
                Hide();
                Main.Instance.UpdateCKAN();
            }
            else
            {
                GUI.user.RaiseError("Error during update.\r\nCan't update automatically, because ckan.exe is read-only or we are not allowed to overwrite it. Please update manually via https://github.com/KSP-CKAN/CKAN/releases/latest.");
            }

        }

        private void CheckUpdateOnLaunchCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Main.Instance.configuration.CheckForUpdatesOnLaunch = CheckUpdateOnLaunchCheckbox.Checked;
            Main.Instance.configuration.Save();
        }

        private void RefreshOnStartupCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Main.Instance.configuration.RefreshOnStartup = RefreshOnStartupCheckbox.Checked;
            Main.Instance.configuration.Save();
        }

        private void HideEpochsCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Main.Instance.configuration.HideEpochs = HideEpochsCheckbox.Checked;
            Main.Instance.configuration.Save();
        }

        private void HideVCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Main.Instance.configuration.HideV = HideVCheckbox.Checked;
            Main.Instance.configuration.Save();
        }

        private void AutoSortUpdateCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Main.Instance.configuration.AutoSortByUpdate = AutoSortUpdateCheckBox.Checked;
            Main.Instance.configuration.Save();
        }

        private void EnableTrayIconCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            MinimizeToTrayCheckBox.Enabled = Main.Instance.configuration.EnableTrayIcon = EnableTrayIconCheckBox.Checked;
            Main.Instance.configuration.Save();
            Main.Instance.CheckTrayState();
        }

        private void MinimizeToTrayCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Main.Instance.configuration.MinimizeToTray = MinimizeToTrayCheckBox.Checked;
            Main.Instance.configuration.Save();
            Main.Instance.CheckTrayState();
        }

        private void RefreshTextBox_TextChanged(object sender, EventArgs e)
        {
            winReg.RefreshRate = string.IsNullOrEmpty(RefreshTextBox.Text) ? 0 : int.Parse(RefreshTextBox.Text);
            UpdateRefreshRate();
        }

        private void RefreshTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        private void PauseRefreshCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Main.Instance.configuration.RefreshPaused = PauseRefreshCheckBox.Checked;
            Main.Instance.configuration.Save();

            if (Main.Instance.configuration.RefreshPaused)
                Main.Instance.refreshTimer.Stop();
            else
                Main.Instance.refreshTimer.Start();
        }
    }
}
