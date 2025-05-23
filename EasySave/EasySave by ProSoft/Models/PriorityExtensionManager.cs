using System;
using System.Collections.Generic;
using System.Text.Json;

namespace EasySave_by_ProSoft.Models
{
    public static class PriorityExtensionManager
    {
        public static string[] GetPriorityExtensions()
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
                        priorityExtensionsList.Add(extStr.ToLowerInvariant());
                    }
                }
            }

            return priorityExtensionsList.ToArray();
        }

        public static bool IsPriorityExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            var ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();

            foreach (var prioExt in GetPriorityExtensions())
            {
                if (string.Equals(prioExt, ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}