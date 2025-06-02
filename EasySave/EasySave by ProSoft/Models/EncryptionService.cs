using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Service responsible for encrypting files using an external tool
    /// </summary>
    public class EncryptionService
    {
        private string cryptoSoftPath;
        private static readonly object _encryptionLock = new object();

        /// <summary>
        /// Initializes a new instance of the EncryptionService
        /// </summary>
        public EncryptionService()
        {
            // Path to the external encryption executable
            // Try to get from environment variable first, then use default path
            cryptoSoftPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\ProSoft\EasySave", "CryptoSoftPath", null) as string ??
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\ASSETS\CryptoSoft.exe");
            cryptoSoftPath = Path.GetFullPath(cryptoSoftPath);
        }

        /// <summary>
        /// Checks if a file should be encrypted based on its extension
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="encryptExtensions">List of extensions to encrypt</param>
        /// <returns>True if the file should be encrypted</returns>
        public bool ShouldEncrypt(string filePath, List<string> encryptExtensions)
        {
            if (string.IsNullOrEmpty(filePath) || encryptExtensions == null || encryptExtensions.Count == 0)
                return false;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            foreach (string ext in encryptExtensions)
            {
                string normalizedExt = ext.StartsWith(".") ? ext.ToLowerInvariant() : $".{ext.ToLowerInvariant()}";
                if (normalizedExt == extension)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Encrypts a file using the external encryption tool
        /// </summary>
        /// <param name="fileIn">Source file path</param>
        /// <param name="fileOut">Destination file path</param>
        /// <returns>Time taken to encrypt the file in milliseconds</returns>
        public long EncryptFile(ref string fileIn, string key)
        {
            Debug.WriteLine($"[ENCRYPT] Thread {Thread.CurrentThread.ManagedThreadId} en attente du verrou...");
            lock (_encryptionLock)
            {
                Debug.WriteLine($"[ENCRYPT] Thread {Thread.CurrentThread.ManagedThreadId} a obtenu le verrou, début du chiffrement de {fileIn}");

                if (!File.Exists(cryptoSoftPath))
                {
                    throw new FileNotFoundException($"Encryption tool not found at: {cryptoSoftPath}");
                }

                try
                {
                    // Ensure the destination directory exists
                    string? destDir = Path.GetDirectoryName(fileIn);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    // Check if the file exists before attempting to encrypt
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = cryptoSoftPath;
                        process.StartInfo.Arguments = $"\"{fileIn}\" \"{key}\"";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.Start();

                        long encryptionTime = 0;
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        encryptionTime = long.Parse(output.Trim());

                        // Check if the process exited successfully
                        if (process.ExitCode != 0)
                        {
                            string error = process.StandardError.ReadToEnd();
                            Debug.WriteLine($"[ENCRYPT] Erreur de chiffrement : {error}");
                            return -1;
                            // throw new Exception($"Encryption failed with exit code {process.ExitCode}: {error}");
                        }

                        Debug.WriteLine($"[ENCRYPT] Thread {Thread.CurrentThread.ManagedThreadId} a terminé le chiffrement de {fileIn}");
                        return encryptionTime > 0 ? encryptionTime : 0;
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show($"Unexpected error: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    Debug.WriteLine($"[ENCRYPT] Exception : {ex.Message}");
                    return -1;
                    // throw;
                }
            }
        }

    }
}