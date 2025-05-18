using System;
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

        /// <summary>
        /// Initializes a new instance of the EncryptionService
        /// </summary>
        public EncryptionService()
        {
            // Path to the external encryption executable
            cryptoSoftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\ASSETS\CryptoSoft.exe");
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
            if (!File.Exists(cryptoSoftPath))
            {
                throw new FileNotFoundException($"Encryption tool not found at: {cryptoSoftPath}");
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                // Create destination directory if it doesn't exist
                string? destDir = Path.GetDirectoryName(fileIn);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                // Start the external encryption process
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = cryptoSoftPath;
                    process.StartInfo.Arguments = $"\"{fileIn}\" \"{key}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    
                    process.Start();
                    process.WaitForExit();

                    // Check exit code
                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception($"Encryption failed with exit code {process.ExitCode}: {error}");
                    }
                }

                stopwatch.Stop();
                return stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during encryption: {ex.Message}");
                throw;
            }
        }
    }
}
