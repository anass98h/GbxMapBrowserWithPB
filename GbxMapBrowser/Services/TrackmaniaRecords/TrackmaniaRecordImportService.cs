#nullable enable

using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.LZO;
using GbxMapBrowser.Models.TrackmaniaRecords;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GbxMapBrowser.Services.TrackmaniaRecords
{
    public sealed class TrackmaniaRecordImportService
    {
        private readonly TrackmaniaRecordDatabase _database;

        public TrackmaniaRecordImportService(TrackmaniaRecordDatabase database)
        {
            _database = database;
            Gbx.LZO = new Lzo();
        }

        public TrackmaniaRecordImportResult Refresh(string? mapFolder = null)
        {
            List<TrackmaniaMapRecord> records = _database.Load();

            Dictionary<string, TrackmaniaMapRecord> recordsByUid = records
                .Where(record => !string.IsNullOrWhiteSpace(record.MapUid))
                .GroupBy(record => record.MapUid)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.LastSeenUtc).First());

            int importedReplayCount = ImportReplayFolder(recordsByUid, GetDefaultReplayFolder());
            int importedMapCount = 0;

            if (!string.IsNullOrWhiteSpace(mapFolder) && Directory.Exists(mapFolder))
            {
                importedMapCount = ImportMapFolder(recordsByUid, mapFolder);
            }

            foreach (TrackmaniaMapRecord record in recordsByUid.Values)
            {
                record.Medal = CalculateMedal(record);
            }

            List<TrackmaniaMapRecord> finalRecords = recordsByUid.Values
                .OrderBy(record => record.MapName)
                .ThenBy(record => record.MapUid)
                .ToList();

            _database.Save(finalRecords);

            return new TrackmaniaRecordImportResult
            {
                DatabasePath = _database.DatabasePath,
                TotalRecords = finalRecords.Count,
                ImportedReplayCount = importedReplayCount,
                ImportedMapCount = importedMapCount,
                ReplayFolder = GetDefaultReplayFolder(),
                MapFolder = mapFolder
            };
        }

        public TrackmaniaMapRecord? GetRecordForMapUid(string mapUid)
        {
            return _database.Load()
                .FirstOrDefault(record => string.Equals(record.MapUid, mapUid, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<TrackmaniaMapRecord> GetAllRecords()
        {
            return _database.Load();
        }

        private static int ImportReplayFolder(Dictionary<string, TrackmaniaMapRecord> recordsByUid, string replayFolder)
        {
            if (!Directory.Exists(replayFolder))
            {
                return 0;
            }

            List<string> replayFiles = Directory
                .EnumerateFiles(replayFolder, "*PersonalBest_TimeAttack.Replay.Gbx", SearchOption.AllDirectories)
                .OrderBy(path => path)
                .ToList();

            int importedCount = 0;

            foreach (string replayFile in replayFiles)
            {
                try
                {
                    var node = Gbx.ParseNode(replayFile);

                    if (node is not CGameCtnReplayRecord replay)
                    {
                        continue;
                    }

                    object? mapInfo = GetValue(replay, "MapInfo");

                    string? mapUid =
                        GetValueString(mapInfo, "MapUid", "Uid") ??
                        ParseMapInfoPart(mapInfo, 0);

                    if (string.IsNullOrWhiteSpace(mapUid))
                    {
                        continue;
                    }

                    int? personalBestMs = ToMilliseconds(GetValue(replay, "Time"));

                    if (personalBestMs is null)
                    {
                        continue;
                    }

                    string coloredMapName = GetMapNameFromReplayFile(replayFile);
                    string cleanMapName = StripTrackmaniaFormatting(coloredMapName);

                    TrackmaniaMapRecord record = GetOrCreate(recordsByUid, mapUid);

                    record.MapName = PreferNewValue(record.MapName, cleanMapName);
                    record.ColoredMapName = PreferNewValue(record.ColoredMapName, coloredMapName);
                    record.Environment = PreferNewValue(
                        record.Environment,
                        GetValueString(mapInfo, "Environment", "EnvironmentName") ?? ParseMapInfoPart(mapInfo, 1)
                    );
                    record.AuthorLogin = PreferNewValue(
                        record.AuthorLogin,
                        GetValueString(mapInfo, "AuthorLogin") ?? ParseMapInfoPart(mapInfo, 2)
                    );

                    if (record.PersonalBestMs is null || personalBestMs.Value < record.PersonalBestMs.Value)
                    {
                        record.PersonalBestMs = personalBestMs.Value;
                        record.PersonalBest = FormatMilliseconds(personalBestMs.Value);
                    }

                    record.HasSeenReplay = true;
                    record.LastReplayFile = replayFile;
                    record.LastReplayWriteTimeUtc = File.GetLastWriteTimeUtc(replayFile);
                    record.LastSeenUtc = DateTime.UtcNow;

                    importedCount++;
                }
                catch
                {
                    // Skip unreadable replay files. The UI should not crash because one replay is broken.
                }
            }

            return importedCount;
        }

        private static int ImportMapFolder(Dictionary<string, TrackmaniaMapRecord> recordsByUid, string mapFolder)
        {
            List<string> mapFiles = Directory
                .EnumerateFiles(mapFolder, "*.Gbx", SearchOption.TopDirectoryOnly)
                .Where(path =>
                    path.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".Challenge.Gbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path)
                .ToList();

            int importedCount = 0;

            foreach (string mapFile in mapFiles)
            {
                try
                {
                    var node = Gbx.ParseHeaderNode(mapFile);

                    if (node is not CGameCtnChallenge challenge)
                    {
                        node = Gbx.ParseNode(mapFile);
                    }

                    if (node is not CGameCtnChallenge map)
                    {
                        continue;
                    }

                    string? mapUid = GetValueString(map, "MapUid", "Uid");

                    if (string.IsNullOrWhiteSpace(mapUid))
                    {
                        continue;
                    }

                    TrackmaniaMapRecord record = GetOrCreate(recordsByUid, mapUid);

                    string coloredMapName = GetValueString(map, "MapName", "Name") ?? Path.GetFileNameWithoutExtension(mapFile);
                    string cleanMapName = StripTrackmaniaFormatting(coloredMapName);

                    record.MapName = cleanMapName;
                    record.ColoredMapName = coloredMapName;
                    record.AuthorLogin = PreferNewValue(record.AuthorLogin, GetValueString(map, "AuthorLogin"));
                    record.BronzeMs = ToMilliseconds(GetValue(map, "BronzeTime", "BronzeScore"));
                    record.SilverMs = ToMilliseconds(GetValue(map, "SilverTime", "SilverScore"));
                    record.GoldMs = ToMilliseconds(GetValue(map, "GoldTime", "GoldScore"));
                    record.AuthorMs = ToMilliseconds(GetValue(map, "AuthorTime", "AuthorScore"));

                    record.HasSeenMapFile = true;
                    record.LastMapFile = mapFile;
                    record.LastMapWriteTimeUtc = File.GetLastWriteTimeUtc(mapFile);
                    record.LastSeenUtc = DateTime.UtcNow;
                    record.Medal = CalculateMedal(record);

                    importedCount++;
                }
                catch
                {
                    // Skip unreadable map files.
                }
            }

            return importedCount;
        }

        private static TrackmaniaMapRecord GetOrCreate(Dictionary<string, TrackmaniaMapRecord> recordsByUid, string mapUid)
        {
            if (recordsByUid.TryGetValue(mapUid, out TrackmaniaMapRecord? existingRecord))
            {
                return existingRecord;
            }

            TrackmaniaMapRecord newRecord = new()
            {
                MapUid = mapUid,
                FirstSeenUtc = DateTime.UtcNow,
                LastSeenUtc = DateTime.UtcNow
            };

            recordsByUid[mapUid] = newRecord;

            return newRecord;
        }

        private static string CalculateMedal(TrackmaniaMapRecord record)
        {
            if (record.PersonalBestMs is null)
            {
                return "Unknown";
            }

            int pb = record.PersonalBestMs.Value;

            if (record.AuthorMs is not null && pb <= record.AuthorMs.Value)
            {
                return "Author";
            }

            if (record.GoldMs is not null && pb <= record.GoldMs.Value)
            {
                return "Gold";
            }

            if (record.SilverMs is not null && pb <= record.SilverMs.Value)
            {
                return "Silver";
            }

            if (record.BronzeMs is not null && pb <= record.BronzeMs.Value)
            {
                return "Bronze";
            }

            if (record.BronzeMs is not null)
            {
                return "None";
            }

            return "Unknown";
        }

        private static string GetDefaultReplayFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Trackmania",
                "Replays",
                "Autosaves"
            );
        }

        private static string? PreferNewValue(string? currentValue, string? newValue)
        {
            return string.IsNullOrWhiteSpace(newValue)
                ? currentValue
                : newValue;
        }

        private static object? GetValue(object? obj, params string[] propertyNames)
        {
            if (obj is null)
            {
                return null;
            }

            Type type = obj.GetType();

            foreach (string propertyName in propertyNames)
            {
                var prop = type.GetProperty(propertyName);

                if (prop is not null)
                {
                    return prop.GetValue(obj);
                }
            }

            return null;
        }

        private static string? GetValueString(object? obj, params string[] propertyNames)
        {
            return GetValue(obj, propertyNames)?.ToString();
        }

        private static int? ToMilliseconds(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is TimeSpan timeSpan)
            {
                return (int)Math.Round(timeSpan.TotalMilliseconds);
            }

            var totalMillisecondsProp = value.GetType().GetProperty("TotalMilliseconds");

            if (totalMillisecondsProp is not null)
            {
                object? totalMilliseconds = totalMillisecondsProp.GetValue(value);

                if (totalMilliseconds is double d)
                {
                    return (int)Math.Round(d);
                }

                if (totalMilliseconds is float f)
                {
                    return (int)Math.Round(f);
                }

                if (totalMilliseconds is decimal m)
                {
                    return (int)Math.Round(m);
                }

                if (totalMilliseconds is int i)
                {
                    return i;
                }

                if (totalMilliseconds is long l)
                {
                    return (int)l;
                }
            }

            return null;
        }

        private static string? ParseMapInfoPart(object? mapInfo, int index)
        {
            if (mapInfo is null)
            {
                return null;
            }

            string? text = mapInfo.ToString();

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            MatchCollection matches = Regex.Matches(text, "\"([^\"]*)\"");

            if (index >= matches.Count)
            {
                return null;
            }

            return matches[index].Groups[1].Value;
        }

        private static string GetMapNameFromReplayFile(string file)
        {
            string name = Path.GetFileName(file);

            const string suffix = "_PersonalBest_TimeAttack.Replay.Gbx";

            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
            }

            name = Regex.Replace(name, @"^l_[^_]+_l_", "");

            return name;
        }

        private static string StripTrackmaniaFormatting(string text)
        {
            text = Regex.Replace(text, @"\$[0-9a-fA-F]{3}", "");
            text = Regex.Replace(text, @"\$[a-zA-Z]", "");
            return text.Trim();
        }

        private static string FormatMilliseconds(int milliseconds)
        {
            TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
            int totalMinutes = (int)time.TotalMinutes;

            return $"{totalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}";
        }
    }

    public sealed class TrackmaniaRecordImportResult
    {
        public required string DatabasePath { get; init; }
        public required int TotalRecords { get; init; }
        public required int ImportedReplayCount { get; init; }
        public required int ImportedMapCount { get; init; }
        public required string ReplayFolder { get; init; }
        public string? MapFolder { get; init; }
    }
}