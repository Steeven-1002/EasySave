using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace EasySave
{
    public class FileSystemService
    {
        public void CopyFile(string sourcePath, string destinationPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                File.Copy(sourcePath, destinationPath, true); // true permet de remplacer si le fichier existe déjà
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la copie du fichier de {sourcePath} vers {destinationPath}: {ex.Message}");
                throw; // Relancez l'exception pour être gérée par l'appelant
            }
        }

        public string GetRelativePath(string basePath, string fullPath)
        {
            if (!fullPath.StartsWith(basePath))
            {
                throw new ArgumentException("Le chemin complet ne commence pas par le chemin de base.");
            }
            return fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        public string GetFileHash(string filePath)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        byte[] hashBytes = md5.ComputeHash(stream);
                        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du calcul du hash du fichier {filePath}: {ex.Message}");
                return null; // Ou lancez une exception, selon votre gestion des erreurs
            }
        }

        public long GetFileSize(string filePath)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                return fileInfo.Length;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération de la taille du fichier {filePath}: {ex.Message}");
                return -1; // Ou lancez une exception
            }
        }

        public void CreateDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la création du répertoire {path}: {ex.Message}");
                throw;
            }
        }

        public List<string> GetFilesInDirectory(string path)
        {
            try
            {
                return Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération des fichiers dans {path}: {ex.Message}");
                return new List<string>(); // Ou lancez une exception
            }
        }

        public List<string> GetDirectoriesInDirectory(string path)
        {
            try
            {
                return Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération des sous-répertoires dans {path}: {ex.Message}");
                return new List<string>(); // Ou lancez une exception
            }
        }

        public List<string> GetAllFiles(string path)
        {
            try
            {
                return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération de tous les fichiers dans {path}: {ex.Message}");
                return new List<string>(); // Ou lancez une exception
            }
        }

        public List<string> GetModifiedFilesInDirectorySince(string path, DateTime since)
        {
            try
            {
                return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                                .Where(file => File.GetLastWriteTime(file) > since)
                                .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération des fichiers modifiés dans {path} depuis {since}: {ex.Message}");
                return new List<string>(); // Ou lancez une exception
            }
        }

        public void CopyDirectory(string sourceDir, string targetDir)
        {
            try
            {
                Directory.CreateDirectory(targetDir);

                foreach (string file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
                {
                    string relativePath = GetRelativePath(sourceDir, file);
                    string targetFilePath = Path.Combine(targetDir, relativePath);
                    CopyFile(file, targetFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la copie du répertoire de {sourceDir} vers {targetDir}: {ex.Message}");
                throw;
            }
        }

        // Vous pourriez ajouter d'autres méthodes selon vos besoins,
        // comme la suppression de fichiers/répertoires, la vérification de l'existence, etc.
    }
}