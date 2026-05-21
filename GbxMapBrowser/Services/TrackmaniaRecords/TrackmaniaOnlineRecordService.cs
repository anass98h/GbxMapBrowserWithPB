#nullable enable

using GbxMapBrowser.Models.TrackmaniaRecords;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace GbxMapBrowser.Services.TrackmaniaRecords
{
    public sealed class TrackmaniaOnlineRecordService
    {
        private const string CoreBaseUrl = "https://prod.trackmania.core.nadeo.online";

        private readonly TrackmaniaRecordDatabase _database;
        private readonly TrackmaniaOnlineRecordCacheService _cacheService;
        private readonly TrackmaniaAccountIdService _accountIdService;
        private readonly TrackmaniaDedicatedServerCredentialService _credentialService;
        private readonly HttpClient _httpClient = new();

        public TrackmaniaOnlineRecordService(
            TrackmaniaRecordDatabase database,
            TrackmaniaOnlineRecordCacheService cacheService,
            TrackmaniaAccountIdService accountIdService,
            TrackmaniaDedicatedServerCredentialService credentialService
        )
        {
            _database = database;
            _cacheService = cacheService;
            _accountIdService = accountIdService;
            _credentialService = credentialService;

            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "GbxMapBrowser/0.1");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        }

        public async Task<TrackmaniaOnlineRecordRefreshResult> RefreshMissingOnlineRecordsAsync(
            string? mapFolder,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(mapFolder) || !Directory.Exists(mapFolder))
            {
                throw new InvalidOperationException("The current map folder was not found.");
            }

            string accountId = _accountIdService.Load();

            if (!Guid.TryParse(accountId, out _))
            {
                throw new InvalidOperationException(
                    "Missing Trackmania Account ID.\n\n" +
                    "Click the online setup button and save your Account ID first."
                );
            }

            TrackmaniaDedicatedServerCredentialService.DedicatedServerCredentials? credentials =
                _credentialService.Load();

            if (credentials == null ||
                string.IsNullOrWhiteSpace(credentials.Login) ||
                string.IsNullOrWhiteSpace(credentials.Password))
            {
                throw new InvalidOperationException(
                    "Missing dedicated server credentials.\n\n" +
                    "Click the online setup button and save your dedicated server login and password first."
                );
            }

            TrackmaniaOnlineRecordRefreshResult result = new();

            List<TrackmaniaMapRecord> records = _database.Load();
            List<TrackmaniaOnlineRecordCacheEntry> cacheEntries = _cacheService.Load();

            List<TrackmaniaMapRecord> onlineCandidates = [];

            foreach (TrackmaniaMapRecord record in records)
            {
                if (string.IsNullOrWhiteSpace(record.MapUid))
                {
                    continue;
                }

                if (!IsRecordFromCurrentMapFolder(record, mapFolder))
                {
                    continue;
                }

                result.MapRecordsInCurrentFolder++;

                if (record.PersonalBestMs is not null)
                {
                    continue;
                }

                result.MissingPersonalBestCount++;

                TrackmaniaOnlineRecordCacheEntry? cacheEntry =
                    _cacheService.GetEntry(cacheEntries, accountId, record.MapUid);

                if (cacheEntry?.Status == TrackmaniaOnlineRecordStatus.Found &&
                    cacheEntry.PersonalBestMs is not null)
                {
                    ApplyOnlinePersonalBest(record, cacheEntry.PersonalBestMs.Value, cacheEntry.Timestamp);
                    result.AppliedCachedFoundCount++;
                    continue;
                }

                if (cacheEntry?.Status == TrackmaniaOnlineRecordStatus.NotFound)
                {
                    result.SkippedCachedNotFoundCount++;
                    continue;
                }

                if (cacheEntry?.Status == TrackmaniaOnlineRecordStatus.Failed &&
                    cacheEntry.FailedUntilUtc is not null &&
                    cacheEntry.FailedUntilUtc.Value > DateTime.UtcNow)
                {
                    result.SkippedTemporaryFailedCount++;
                    continue;
                }

                onlineCandidates.Add(record);
            }

            _database.Save(records);

            if (onlineCandidates.Count == 0)
            {
                _cacheService.Save(cacheEntries);
                return result;
            }

            string token = await GetNadeoServicesTokenAsync(credentials, cancellationToken);

            foreach (TrackmaniaMapRecord record in onlineCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(650, cancellationToken);

                try
                {
                    result.CheckedOnlineCount++;

                    MapInfoResult? mapInfo = await GetMapInfoAsync(token, record.MapUid, cancellationToken);

                    if (mapInfo == null)
                    {
                        MarkNotFound(cacheEntries, accountId, record.MapUid, null, null, "Map UID was not found online.");
                        result.NotFoundOnlineCount++;

                        _cacheService.Save(cacheEntries);
                        continue;
                    }

                    FillMissingMapInfo(record, mapInfo);

                    OnlineRecordResult? onlineRecord = await GetRecordAsync(
                        token,
                        accountId,
                        mapInfo.MapId,
                        cancellationToken
                    );

                    if (onlineRecord == null)
                    {
                        MarkNotFound(cacheEntries, accountId, record.MapUid, mapInfo, null, "No online PB was found.");
                        result.NotFoundOnlineCount++;

                        _cacheService.Save(cacheEntries);
                        continue;
                    }

                    if (record.PersonalBestMs is null)
                    {
                        ApplyOnlinePersonalBest(record, onlineRecord.PersonalBestMs, onlineRecord.Timestamp);
                    }

                    TrackmaniaOnlineRecordCacheEntry foundEntry = new()
                    {
                        AccountId = accountId,
                        MapUid = record.MapUid,
                        MapId = mapInfo.MapId,
                        MapName = mapInfo.Name,
                        Status = TrackmaniaOnlineRecordStatus.Found,
                        PersonalBestMs = onlineRecord.PersonalBestMs,
                        PersonalBest = TrackmaniaRecordImportService.FormatMilliseconds(onlineRecord.PersonalBestMs),
                        Medal = record.Medal,
                        Timestamp = onlineRecord.Timestamp,
                        CheckedUtc = DateTime.UtcNow,
                        FailedUntilUtc = null,
                        ErrorMessage = null
                    };

                    _cacheService.Upsert(cacheEntries, foundEntry);

                    result.FoundOnlineCount++;

                    _database.Save(records);
                    _cacheService.Save(cacheEntries);
                }
                catch (Exception ex)
                {
                    TrackmaniaOnlineRecordCacheEntry failedEntry = new()
                    {
                        AccountId = accountId,
                        MapUid = record.MapUid,
                        MapId = null,
                        MapName = record.MapName,
                        Status = TrackmaniaOnlineRecordStatus.Failed,
                        PersonalBestMs = null,
                        PersonalBest = null,
                        Medal = null,
                        Timestamp = null,
                        CheckedUtc = DateTime.UtcNow,
                        FailedUntilUtc = DateTime.UtcNow.AddMinutes(30),
                        ErrorMessage = ex.Message
                    };

                    _cacheService.Upsert(cacheEntries, failedEntry);
                    _cacheService.Save(cacheEntries);

                    result.FailedCount++;
                }
            }

            _database.Save(records);
            _cacheService.Save(cacheEntries);

            return result;
        }

        private async Task<string> GetNadeoServicesTokenAsync(
            TrackmaniaDedicatedServerCredentialService.DedicatedServerCredentials credentials,
            CancellationToken cancellationToken
        )
        {
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                $"{CoreBaseUrl}/v2/authentication/token/basic"
            );

            string basic = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{credentials.Login}:{credentials.Password}")
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            request.Content = new StringContent(
                """{"audience":"NadeoServices"}""",
                Encoding.UTF8,
                "application/json"
            );

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Dedicated server authentication failed: {(int)response.StatusCode} {response.ReasonPhrase}\n\n{body}"
                );
            }

            JsonNode? json = JsonNode.Parse(body);
            string? accessToken = json?["accessToken"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Token response did not contain accessToken.");
            }

            return accessToken;
        }

        private async Task<MapInfoResult?> GetMapInfoAsync(
            string token,
            string mapUid,
            CancellationToken cancellationToken
        )
        {
            string url = $"{CoreBaseUrl}/maps/by-uid/?mapUidList={Uri.EscapeDataString(mapUid)}";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", $"nadeo_v1 t={token}");

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Map info lookup failed: {(int)response.StatusCode} {response.ReasonPhrase}\n\n{body}"
                );
            }

            JsonArray? array = JsonNode.Parse(body)?.AsArray();

            if (array is null || array.Count == 0)
            {
                return null;
            }

            JsonNode? map = array[0];

            if (map is null)
            {
                return null;
            }

            string mapId = map["mapId"]?.GetValue<string>() ?? "";

            if (string.IsNullOrWhiteSpace(mapId))
            {
                return null;
            }

            return new MapInfoResult
            {
                MapUid = map["mapUid"]?.GetValue<string>() ?? mapUid,
                MapId = mapId,
                Name = map["name"]?.GetValue<string>() ?? "",
                BronzeScore = map["bronzeScore"]?.GetValue<int?>(),
                SilverScore = map["silverScore"]?.GetValue<int?>(),
                GoldScore = map["goldScore"]?.GetValue<int?>(),
                AuthorScore = map["authorScore"]?.GetValue<int?>()
            };
        }

        private async Task<OnlineRecordResult?> GetRecordAsync(
            string token,
            string accountId,
            string mapId,
            CancellationToken cancellationToken
        )
        {
            string url =
                $"{CoreBaseUrl}/v2/mapRecords/by-account/?" +
                $"accountIdList={Uri.EscapeDataString(accountId)}&" +
                $"mapId={Uri.EscapeDataString(mapId)}";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", $"nadeo_v1 t={token}");

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Record lookup failed: {(int)response.StatusCode} {response.ReasonPhrase}\n\n{body}"
                );
            }

            JsonArray? array = JsonNode.Parse(body)?.AsArray();

            if (array is null || array.Count == 0)
            {
                return null;
            }

            JsonNode? record = array[0];

            if (record is null)
            {
                return null;
            }

            int? time = record["recordScore"]?["time"]?.GetValue<int?>();

            if (time is null || time.Value == unchecked((int)4294967295))
            {
                return null;
            }

            return new OnlineRecordResult
            {
                PersonalBestMs = time.Value,
                ApiMedal = record["medal"]?.GetValue<int?>(),
                Timestamp = record["timestamp"]?.GetValue<string>()
            };
        }

        private static void FillMissingMapInfo(TrackmaniaMapRecord record, MapInfoResult mapInfo)
        {
            if (string.IsNullOrWhiteSpace(record.MapName))
            {
                record.MapName = mapInfo.Name;
            }

            record.BronzeMs ??= mapInfo.BronzeScore;
            record.SilverMs ??= mapInfo.SilverScore;
            record.GoldMs ??= mapInfo.GoldScore;
            record.AuthorMs ??= mapInfo.AuthorScore;
        }

        private static void ApplyOnlinePersonalBest(
            TrackmaniaMapRecord record,
            int personalBestMs,
            string? timestamp
        )
        {
            record.PersonalBestMs = personalBestMs;
            record.PersonalBest = TrackmaniaRecordImportService.FormatMilliseconds(personalBestMs);
            record.PersonalBestSource = "Online";
            record.Medal = TrackmaniaRecordImportService.CalculateMedal(record);
            record.LastSeenUtc = DateTime.UtcNow;
        }

        private void MarkNotFound(
            List<TrackmaniaOnlineRecordCacheEntry> cacheEntries,
            string accountId,
            string mapUid,
            MapInfoResult? mapInfo,
            string? timestamp,
            string message
        )
        {
            TrackmaniaOnlineRecordCacheEntry notFoundEntry = new()
            {
                AccountId = accountId,
                MapUid = mapUid,
                MapId = mapInfo?.MapId,
                MapName = mapInfo?.Name,
                Status = TrackmaniaOnlineRecordStatus.NotFound,
                PersonalBestMs = null,
                PersonalBest = null,
                Medal = null,
                Timestamp = timestamp,
                CheckedUtc = DateTime.UtcNow,
                FailedUntilUtc = null,
                ErrorMessage = message
            };

            _cacheService.Upsert(cacheEntries, notFoundEntry);
        }

        private static bool IsRecordFromCurrentMapFolder(TrackmaniaMapRecord record, string mapFolder)
        {
            if (string.IsNullOrWhiteSpace(record.LastMapFile))
            {
                return false;
            }

            try
            {
                if (!File.Exists(record.LastMapFile))
                {
                    return false;
                }

                string? recordFolder = Path.GetDirectoryName(record.LastMapFile);

                if (string.IsNullOrWhiteSpace(recordFolder))
                {
                    return false;
                }

                string normalizedRecordFolder = NormalizeFolderPath(recordFolder);
                string normalizedCurrentFolder = NormalizeFolderPath(mapFolder);

                return string.Equals(
                    normalizedRecordFolder,
                    normalizedCurrentFolder,
                    StringComparison.OrdinalIgnoreCase
                );
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeFolderPath(string folder)
        {
            return Path.GetFullPath(folder).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );
        }

        private sealed class MapInfoResult
        {
            public string MapUid { get; set; } = "";
            public string MapId { get; set; } = "";
            public string Name { get; set; } = "";
            public int? BronzeScore { get; set; }
            public int? SilverScore { get; set; }
            public int? GoldScore { get; set; }
            public int? AuthorScore { get; set; }
        }

        private sealed class OnlineRecordResult
        {
            public int PersonalBestMs { get; set; }
            public int? ApiMedal { get; set; }
            public string? Timestamp { get; set; }
        }
    }

    public sealed class TrackmaniaOnlineRecordRefreshResult
    {
        public int MapRecordsInCurrentFolder { get; set; }
        public int MissingPersonalBestCount { get; set; }
        public int AppliedCachedFoundCount { get; set; }
        public int SkippedCachedNotFoundCount { get; set; }
        public int SkippedTemporaryFailedCount { get; set; }
        public int CheckedOnlineCount { get; set; }
        public int FoundOnlineCount { get; set; }
        public int NotFoundOnlineCount { get; set; }
        public int FailedCount { get; set; }
    }
}