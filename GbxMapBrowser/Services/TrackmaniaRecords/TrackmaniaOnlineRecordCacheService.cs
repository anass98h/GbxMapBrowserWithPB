#nullable enable

using GbxMapBrowser.Models.TrackmaniaRecords;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GbxMapBrowser.Services.TrackmaniaRecords
{
    public sealed class TrackmaniaOnlineRecordCacheService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        private readonly string _cachePath;

        public string CachePath => _cachePath;

        public TrackmaniaOnlineRecordCacheService(string? cachePath = null)
        {
            _cachePath = cachePath ?? GetDefaultCachePath();
        }

        public List<TrackmaniaOnlineRecordCacheEntry> Load()
        {
            if (!File.Exists(_cachePath))
            {
                return [];
            }

            try
            {
                string json = File.ReadAllText(_cachePath);

                return JsonSerializer.Deserialize<List<TrackmaniaOnlineRecordCacheEntry>>(json, JsonOptions)
                    ?? [];
            }
            catch
            {
                BackupCorruptCache();
                return [];
            }
        }

        public void Save(IEnumerable<TrackmaniaOnlineRecordCacheEntry> entries)
        {
            string? directory = Path.GetDirectoryName(_cachePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            List<TrackmaniaOnlineRecordCacheEntry> cleanedEntries = entries
                .Where(entry =>
                    !string.IsNullOrWhiteSpace(entry.AccountId) &&
                    !string.IsNullOrWhiteSpace(entry.MapUid))
                .GroupBy(entry => CreateKey(entry.AccountId, entry.MapUid), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(entry => entry.CheckedUtc).First())
                .OrderBy(entry => entry.MapName)
                .ThenBy(entry => entry.MapUid)
                .ToList();

            string json = JsonSerializer.Serialize(cleanedEntries, JsonOptions);

            File.WriteAllText(_cachePath, json);
        }

        public TrackmaniaOnlineRecordCacheEntry? GetEntry(
            IReadOnlyList<TrackmaniaOnlineRecordCacheEntry> entries,
            string accountId,
            string mapUid
        )
        {
            string key = CreateKey(accountId, mapUid);

            return entries.FirstOrDefault(entry =>
                string.Equals(CreateKey(entry.AccountId, entry.MapUid), key, StringComparison.OrdinalIgnoreCase));
        }

        public void Upsert(
            List<TrackmaniaOnlineRecordCacheEntry> entries,
            TrackmaniaOnlineRecordCacheEntry newEntry
        )
        {
            string key = CreateKey(newEntry.AccountId, newEntry.MapUid);

            entries.RemoveAll(entry =>
                string.Equals(CreateKey(entry.AccountId, entry.MapUid), key, StringComparison.OrdinalIgnoreCase));

            entries.Add(newEntry);
        }

        private void BackupCorruptCache()
        {
            if (!File.Exists(_cachePath))
            {
                return;
            }

            string backupPath = _cachePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

            try
            {
                File.Move(_cachePath, backupPath);
            }
            catch
            {
                // Do not crash the app if backup fails.
            }
        }

        private static string CreateKey(string accountId, string mapUid)
        {
            return accountId.Trim().ToLowerInvariant() + "|" + mapUid.Trim().ToLowerInvariant();
        }

        private static string GetDefaultCachePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return Path.Combine(
                localAppData,
                "GbxMapBrowser",
                "trackmania-online-record-cache.json"
            );
        }
    }
}