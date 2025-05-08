// EasySave.Services.FileSystemService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography; // Exemple pour GetFileHash

namespace EasySave.Services
{
    public class FileSystemService
    {
        public void CopyFile(string source, string destination)
        {
            try
            {
                string? destDir = Path.GetDirectoryName(destination);
                if (destDir != null && !Directory.Exists(destDir)) // Utilise System.IO.Directory.Exists
                {
                    Directory.CreateDirectory(destDir); // Utilise System.IO.Directory.CreateDirectory
                }
                File.Copy(source, destination, true); // true pour overwrite
                // Dans une version réelle, cette méthode retournerait un FileCopyResult
                // et/ou notifierait des observateurs via la stratégie.
                Console.WriteLine($"FileSystemService: Copied '{source}' to '{destination}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR copying '{source}' to '{destination}': {ex.Message}");
                throw; // Il est souvent préférable de laisser l'appelant gérer l'exception.
            }
        }

        public string GetFileHash(string path)
        {
            // TODO: Implémenter une logique de hachage robuste si nécessaire
            Console.WriteLine($"FileSystemService: GetFileHash for '{path}' (stub).");
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(path))
                    {
                        byte[] hash = sha256.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR GetFileHash for '{path}': {ex.Message}");
                return string.Empty;
            }
        }

        public long GetSize(string path) // Le diagramme spécifie int
        {
            try
            {
                // Attention: une conversion en int peut entraîner une perte de données pour les gros fichiers.
                // long est généralement préférable pour la taille des fichiers.
                return (int)new FileInfo(path).Length;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR GetSize for '{path}': {ex.Message}");
                return 0;
            }
        }

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

        public List<string> GetFilesInDirectory(string path)
        {
            try
            {
                // Le diagramme ne spécifie pas la recherche récursive, donc on ne la met pas par défaut.
                // Pour inclure les sous-dossiers : Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).ToList();
                return Directory.GetFiles(path).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR GetFilesInDirectory for '{path}': {ex.Message}");
                return new List<string>();
            }
        }

        public List<string> GetModifiedFilesSince(string path, DateTime since)
        {
            try
            {
                // Ici, on va supposer une recherche récursive car c'est plus logique pour les sauvegardes
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

        // Ajout des méthodes qui étaient dans le diagramme mais potentiellement omises dans le code précédent
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public DateTime GetFileLastWriteTime(string filePath) // Ajouté car souvent utile et implicite
        {
            try
            {
                return File.GetLastWriteTime(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileSystemService ERROR GetFileLastWriteTime for '{filePath}': {ex.Message}");
                return DateTime.MinValue;
            }
        }
    }
}