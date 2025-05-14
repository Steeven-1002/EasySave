namespace EasySave_by_ProSoft.Views {
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void ValidateSettings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Paramètres validés (depuis SettingsView).");
        }
    }
}