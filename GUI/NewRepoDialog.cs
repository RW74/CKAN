﻿using System;
using System.Windows.Forms;

namespace CKAN
{


    public partial class NewRepoDialog : Form
    {
        public NewRepoDialog()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
        }

        protected override bool ProcessDialogKey (Keys keyData)
        {
            if (Form.ModifierKeys == Keys.None && keyData == Keys.Escape) {
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        private void NewRepoDialog_Load(object sender, EventArgs e)
        {
            RepositoryList repositories;

            try
            {
                repositories = Main.FetchMasterRepositoryList();
            }
            catch
            {
                ReposListBox.Items.Add("Failed to fetch master list..");
                return;
            }

            ReposListBox.Items.Clear();

            if (repositories.repositories == null)
            {
                ReposListBox.Items.Add("Failed to fetch master list..");
                return;
            }

            foreach (Repository repository in repositories.repositories)
            {
                ReposListBox.Items.Add(String.Format("{0} | {1}", repository.name, repository.uri));
            }
        }

        private void ReposListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ReposListBox.SelectedItem == null)
            {
                return;
            }

            RepoUrlTextBox.Text = (string) ReposListBox.SelectedItem;
        }

        private void RepoUrlTextBox_TextChanged(object sender, EventArgs e)
        {
            RepoOK.Enabled = RepoUrlTextBox.Text.Length != 0;
        }

    }
}
