#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GbxMapBrowser.Services.TrackmaniaRecords
{
    public sealed class TrackmaniaAccountIdService
    {
        public string GetStoragePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return Path.Combine(
                localAppData,
                "GbxMapBrowser",
                "trackmania-account-id.txt"
            );
        }

        public string Load()
        {
            string path = GetStoragePath();

            if (!File.Exists(path))
            {
                return "";
            }

            return File.ReadAllText(path).Trim();
        }

        public void Save(string accountId)
        {
            string path = GetStoragePath();
            string directory = Path.GetDirectoryName(path) ?? "";

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, accountId.Trim().ToLowerInvariant());
        }

        public bool IsValidAccountId(string accountId)
        {
            return Guid.TryParse(accountId, out _);
        }

        public bool CanUseOpenplanetAutoDetection()
        {
            return GetCandidateLogFiles().Count > 0;
        }

        public string FindAccountIdFromOpenplanetLogs()
        {
            List<FileInfo> candidateFiles = GetCandidateLogFiles();

            foreach (FileInfo file in candidateFiles)
            {
                string? accountId = TryFindAccountIdInFile(file);

                if (!string.IsNullOrWhiteSpace(accountId))
                {
                    return accountId;
                }
            }

            throw new InvalidOperationException(
                "Could not find your Trackmania Account ID in Openplanet logs.\n\n" +
                "Start Trackmania once with Openplanet enabled, then try again.\n\n" +
                "Expected log line:\n" +
                "NadeoServices account ID: 08242041-438c-4d60-bd98-230335bd678b"
            );
        }

        private static string[] GetOpenplanetFolders()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            return
            [
                Path.Combine(userProfile, "OpenplanetNext"),
                Path.Combine(userProfile, "Openplanet"),
                Path.Combine(documents, "OpenplanetNext"),
                Path.Combine(documents, "Openplanet"),
                Path.Combine(localAppData, "OpenplanetNext"),
                Path.Combine(localAppData, "Openplanet"),
                Path.Combine(appData, "OpenplanetNext"),
                Path.Combine(appData, "Openplanet")
            ];
        }

        private static List<FileInfo> GetCandidateLogFiles()
        {
            List<FileInfo> files = [];

            foreach (string folder in GetOpenplanetFolders())
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                files.AddRange(GetFilesSafe(folder, "*.log"));
                files.AddRange(GetFilesSafe(folder, "*.txt"));
                files.AddRange(GetFilesSafe(folder, "*.json"));
            }

            return files
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(200)
                .ToList();
        }

        private static IEnumerable<FileInfo> GetFilesSafe(string folder, string pattern)
        {
            try
            {
                return Directory
                    .EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
                    .Select(file => new FileInfo(file))
                    .ToArray();
            }
            catch
            {
                return [];
            }
        }

        private static string? TryFindAccountIdInFile(FileInfo file)
        {
            string text;

            try
            {
                text = ReadUsefulFileText(file);
            }
            catch
            {
                return null;
            }

            return TryExtractAccountId(text);
        }

        private static string ReadUsefulFileText(FileInfo file)
        {
            const long maxBytesToRead = 2 * 1024 * 1024;

            if (file.Length <= maxBytesToRead)
            {
                return File.ReadAllText(file.FullName);
            }

            using FileStream stream = File.OpenRead(file.FullName);

            stream.Seek(-maxBytesToRead, SeekOrigin.End);

            using StreamReader reader = new(stream);

            return reader.ReadToEnd();
        }

        private static string? TryExtractAccountId(string text)
        {
            string normalized = Regex.Replace(text, @"\s+", "");

            Match markerMatch = Regex.Match(
                normalized,
                @"NadeoServicesaccountID:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
                RegexOptions.IgnoreCase
            );

            if (markerMatch.Success)
            {
                return markerMatch.Groups[1].Value.ToLowerInvariant();
            }

            return null;
        }
    }
}