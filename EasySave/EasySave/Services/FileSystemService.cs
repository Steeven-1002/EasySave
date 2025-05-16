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
                Console.WriteLine($"FileSystemService: Copied '{source}' to '{destination}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR copying '{source}' to '{destination}': {ex.Message}");
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
            Console.WriteLine($"FileSystemService: GetFileHash for '{path}' (stub).");
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(path);
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR GetFileHash for '{path}': {ex.Message}");
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
                Console.WriteLine($"FileSystemService ERROR GetSize for '{path}': {ex.Message}");
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
                // Console.WriteLine($"FileSystemService: Created directory '{path}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR creating directory '{path}': {ex.Message}");
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
                Console.WriteLine($"FileSystemService ERROR GetFilesInDirectory for '{path}': {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Retrieves files in a directory that have been modified since a specified date and time.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <param name="since">The date and time to compare file modification times against.</param>
        /// <returns>A list of file paths, or an empty list if an error occurs.</returns>
        public List<string> GetModifiedFilesSince(string path, DateTime since)
        {
            try
            {
                return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                                .Where(f => File.GetLastWriteTime(f) > since)
                                .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR GetModifiedFilesSince for '{path}': {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Checks if a directory exists at the specified path.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <returns>True if the directory exists, otherwise false.</returns>
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        /// <summary>
        /// Checks if a file exists at the specified path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>True if the file exists, otherwise false.</returns>
        public bool FileExists(string path)
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
                Console.WriteLine($"FileSystemService ERROR GetAllContents for '{path}': {ex.Message}");
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
                    Console.WriteLine($"FileSystemService: Deleted '{path}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR deleting '{path}': {ex.Message}");
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
                    Console.WriteLine($"FileSystemService: Deleted directory '{path}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR deleting directory '{path}': {ex.Message}");
                throw;
            }
        }
    }
}
