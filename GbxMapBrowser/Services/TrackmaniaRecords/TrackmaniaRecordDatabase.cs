#nullable enable

using GbxMapBrowser.Models.TrackmaniaRecords;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GbxMapBrowser.Services.TrackmaniaRecords
{
    public sealed class TrackmaniaRecordDatabase
    {
        private readonly string _databasePath;

        public string DatabasePath => _databasePath;

        public TrackmaniaRecordDatabase(string? databasePath = null)
        {
            _databasePath = databasePath ?? GetDefaultDatabasePath();
        }

        public List<TrackmaniaMapRecord> Load()
        {
            if (!File.Exists(_databasePath))
            {
                return [];
            }

            try
            {
                string json = File.ReadAllText(_databasePath);

                return JsonSerializer.Deserialize<List<TrackmaniaMapRecord>>(json)
                    ?? [];
            }
            catch
            {
                BackupCorruptDatabase();
                return [];
            }
        }

        public void Save(IEnumerable<TrackmaniaMapRecord> records)
        {
            string? directory = Path.GetDirectoryName(_databasePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var cleanedRecords = records
                .Where(record => !string.IsNullOrWhiteSpace(record.MapUid))
                .GroupBy(record => record.MapUid)
                .Select(group => group.OrderByDescending(record => record.LastSeenUtc).First())
                .OrderBy(record => record.MapName)
                .ThenBy(record => record.MapUid)
                .ToList();

            string json = JsonSerializer.Serialize(
                cleanedRecords,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            );

            File.WriteAllText(_databasePath, json);
        }

        private void BackupCorruptDatabase()
        {
            if (!File.Exists(_databasePath))
            {
                return;
            }

            string backupPath = _databasePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

            try
            {
                File.Move(_databasePath, backupPath);
            }
            catch
            {
                // Do not crash the app if backup fails.
            }
        }

        private static string GetDefaultDatabasePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return Path.Combine(
                localAppData,
                "GbxMapBrowser",
                "trackmania-records.json"
            );
        }
    }
}