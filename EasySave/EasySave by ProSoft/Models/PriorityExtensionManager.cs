using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace EasySave_by_ProSoft.Models
{
    public static class PriorityExtensionManager
    {
        private static string[]? priorityExtensions;

        public static object Instance { get; internal set; }

        public static string[] GetPriorityExtensions()
        {
            if (priorityExtensions == null)
            {
                priorityExtensions = LoadPriorityExtensionsFromSettings();
            }
            return priorityExtensions;
        }

        private static string[] LoadPriorityExtensionsFromSettings()
        {
            var priorityExtensionsList = new List<string>();

            var jsonElementObj = AppSettings.Instance.GetSetting("ExtensionFilePriority");

            if (jsonElementObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var ext in jsonElement.EnumerateArray())
                {
                    string? extStr = ext.GetString();
                    if (!string.IsNullOrWhiteSpace(extStr))
                    {
                        extStr = extStr.StartsWith('.') ? extStr : "." + extStr;
                        priorityExtensionsList.Add(extStr);
                    }
                }
            }

            return priorityExtensionsList.ToArray();
        }


        
        public static bool IsPriorityExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;

            return GetPriorityExtensions().Any(prioExt =>
                string.Equals(prioExt, extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
