using System.Windows;
using MessageBox = System.Windows.MessageBox;


namespace EasySave_by_ProSoft.Services
{
    /// <summary>
    /// Implementation of dialog service using WPF MessageBox
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <summary>
        /// Shows an information message to the user
        /// </summary>
        public void ShowInformation(string message, string title = "Information")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Shows an error message to the user
        /// </summary>
        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }



        /// <summary>
        /// Shows a warning message to the user
        /// </summary>
        public void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowBusinessSoftware(string title = "Sauvegarde bloquée", string localization = null)
        {
            MessageBox.Show(localization, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Shows a confirmation dialog to the user and returns their choice
        /// </summary>
        public bool ShowConfirmation(string message, string title = "Confirmation")
        {
            MessageBoxResult result = MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            return result == MessageBoxResult.OK;
        }

        /// <summary>
        /// Shows a Yes/No confirmation dialog to the user and returns their choice
        /// </summary>
        public bool ShowYesNoDialog(string message, string title = "Confirmation")
        {
            MessageBoxResult result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }




    }
}