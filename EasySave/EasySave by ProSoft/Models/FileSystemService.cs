using System.IO;
using System.Security.Cryptography;

namespace EasySave.Services
{
    /// <summary>
    /// Provides file system operations such as copying files, calculating file hashes, and retrieving file information.
    /// </summary>
    public class FileSystemService
    {
        /// <summary>
        /// Copies a file from the source path to the destination path, creating directories if necessary.
        /// </summary>
        /// <param name="source">The source file path.</param>
        /// <param name="destination">The destination file path.</param>
        /// <exception cref="Exception">Thrown if an error occurs during the copy operation.</exception>
        public void CopyFile(string source, string destination)
        {
            try
            {
                string? destDir = Path.GetDirectoryName(destination);
                if (destDir != null && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(source, destination, true); // true for overwrite existing files
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error copying file from '{source}' to '{destination}': {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                throw;
            }
        }

        /// <summary>
        /// Computes the SHA-256 hash of a file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The SHA-256 hash as a hexadecimal string, or an empty string if an error occurs.</returns>
        public string GetFileHash(string path)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(path);
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error computing hash for '{path}': {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the size of a file in bytes.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The size of the file in bytes, or 0 if an error occurs.</returns>
        public long GetSize(string path)
        {
            try
            {
                return (int)new FileInfo(path).Length;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error getting size for '{path}': {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return 0;
            }
        }

        /// <summary>
        /// Creates a directory at the specified path.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <exception cref="Exception">Thrown if an error occurs during directory creation.</exception>
        public void CreateDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error creating directory '{path}': {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all files in a directory and its subdirectories.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <returns>A list of file paths, or an empty list if an error occurs.</returns>
        public List<string> GetFilesInDirectory(string path)
        {
            try
            {
                return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error computing hash for '{path}': {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return new List<string>();
            }
        }

        /// <summary>
        /// Checks if a directory exists at the specified path.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <returns>True if the directory exists, otherwise false.</returns>
        public bool ExistDir(string path)
        {
            return Directory.Exists(path);
        }

        /// <summary>
        /// Checks if a file exists at the specified path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>True if the file exists, otherwise false.</returns>
        public bool ExistFile(string path)
        {
            return File.Exists(path);
        }

        /// <summary>  
        /// Retrieves all directories, subdirectories, and files contained in the specified directory path.  
        /// </summary>  
        /// <param name="path">The directory path.</param>  
        /// <returns>A list of paths for all directories, subdirectories, and files, or an empty list if an error occurs.</returns>  
        public List<string> GetAllContents(string path)
        {
            try
            {
                var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                return directories.Concat(files).ToList();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error computing hash for '{path}': {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return new List<string>();
            }
        }

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="path">The file path to delete.</param>
        public void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error computing hash for '{path}': {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                throw;
            }
        }

        /// <summary>
        /// Deletes the specified directory and its contents.
        /// </summary>
        /// <param name="path">The directory path to delete.</param>
        public void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error computing hash for '{path}': {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                throw;
            }
        }
    }
}
