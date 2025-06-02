namespace EasySave_by_ProSoft.Services
{
    /// <summary>
    /// Interface for dialog services following MVVM pattern
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows an information message to the user
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the dialog</param>
        void ShowInformation(string message, string title = "Information");

        /// <summary>
        /// Shows an error message to the user
        /// </summary>
        /// <param name="message">The error message to display</param>
        /// <param name="title">The title of the dialog</param>
        void ShowError(string message, string title = "Error");

        /// <summary>
        /// Shows a warning message to the user
        /// </summary>
        /// <param name="message">The warning message to display</param>
        /// <param name="title">The title of the dialog</param>
        void ShowWarning(string message, string title = "Warning");

        void ShowBusinessSoftware(string message, string title = "Sauvegarde bloquée");




        /// <summary>
        /// Shows a confirmation dialog to the user and returns their choice
        /// </summary>
        /// <param name="message">The confirmation message</param>
        /// <param name="title">The title of the dialog</param>
        /// <returns>True if the user confirms, false otherwise</returns>
        bool ShowConfirmation(string message, string title = "Confirmation");

        /// <summary>
        /// Shows a Yes/No confirmation dialog to the user and returns their choice
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The title of the dialog</param>
        /// <returns>True if Yes selected, false if No selected</returns>
        bool ShowYesNoDialog(string message, string title = "Confirmation");
    }
}