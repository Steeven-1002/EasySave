using System.Windows; // For RoutedEventArgs and MessageBox
using System.Windows.Controls; // For UserControl
using EasySave_by_ProSoft.Localization;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using WinForms = System.Windows.Forms;


namespace EasySave_by_ProSoft.Views
{
    public partial class BackupJobsView : System.Windows.Controls.UserControl
    {
        public BackupJobsView()
        {
            InitializeComponent();
        }

        private void LaunchSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(Localization.Resources.MessageBoxLaunchJob);
        }

        private void CreateNewJob_Click(object sender, RoutedEventArgs e)
        {
            if (CreateJobPanel != null)
            {
                CreateJobPanel.Visibility = Visibility.Visible;
            }
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string dialogCom = Localization.Resources.MessageSelectSourceFolder;
            JobSourcePathTextBox.Text = Browse_Click(sender, e, dialogCom);
        }

        private void BrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            string dialogCom = Localization.Resources.MessageSelectTargetFolder;
            JobTargetPathTextBox.Text = Browse_Click(sender, e, dialogCom);
        }

        private string Browse_Click(object sender, RoutedEventArgs e, string com)
        {
            var dialog = new WinForms.FolderBrowserDialog();
            dialog.Description = com; // Set the description based on the button clicked
            WinForms.DialogResult result = dialog.ShowDialog();

            if (result == WinForms.DialogResult.OK)
            {
                string selectedFilePath = dialog.SelectedPath;
                return selectedFilePath;
            }
            return string.Empty; // Return empty string if no folder is selected
        }

        private void ValidateNewJob_Click(object sender, RoutedEventArgs e)
        {
            // Logic to validate fields and create the job
            System.Windows.MessageBox.Show(Localization.Resources.MessageNewJobValidated);
            if (CreateJobPanel != null)
            {
                CreateJobPanel.Visibility = Visibility.Collapsed; // Hide the panel after validation
            }
        }

        private void CancelNewJob_Click(object sender, RoutedEventArgs e)
        {
            if (CreateJobPanel != null)
            {
                CreateJobPanel.Visibility = Visibility.Collapsed; // Hide the panel on cancel
            }
        }       
    }
}