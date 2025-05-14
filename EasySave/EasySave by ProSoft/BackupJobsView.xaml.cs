using System.Windows; // For RoutedEventArgs and MessageBox
using System.Windows.Controls; // For UserControl

namespace EasySave_by_ProSoft.Views
{
    public partial class BackupJobsView : UserControl
    {
        public BackupJobsView()
        {
            InitializeComponent();
        }

        private void LaunchSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Lancement du travail sélectionné (simulation).");
        }

        private void CreateNewJob_Click(object sender, RoutedEventArgs e)
        {
            if (CreateJobPanel != null)
            {
                CreateJobPanel.Visibility = Visibility.Visible;
            }
            MessageBox.Show("Affichage du panneau de création (simulation).");
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            MessageBox.Show("Parcourir source (simulation).");
        }

        private void BrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Parcourir cible (simulation).");
            // Similar logic to BrowseSource_Click
        }

        private void ValidateNewJob_Click(object sender, RoutedEventArgs e)
        {
            // Logic to validate fields and create the job
            MessageBox.Show("Nouveau travail validé (simulation).");
            if (CreateJobPanel != null)
            {
                CreateJobPanel.Visibility = Visibility.Collapsed; // Hide the panel after validation
            }
        }

        private void CancelNewJob_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Création annulée (simulation).");
            if (CreateJobPanel != null)
            {
                CreateJobPanel.Visibility = Visibility.Collapsed; // Hide the panel on cancel
            }
        }       
    }
}