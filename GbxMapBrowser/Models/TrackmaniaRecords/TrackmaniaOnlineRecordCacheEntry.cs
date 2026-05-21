#nullable enable

using System;

namespace GbxMapBrowser.Models.TrackmaniaRecords
{
    public sealed class TrackmaniaOnlineRecordCacheEntry
    {
        public required string AccountId { get; set; }
        public required string MapUid { get; set; }

        public string? MapId { get; set; }
        public string? MapName { get; set; }

        public TrackmaniaOnlineRecordStatus Status { get; set; } = TrackmaniaOnlineRecordStatus.NeverChecked;

        public int? PersonalBestMs { get; set; }
        public string? PersonalBest { get; set; }
        public string? Medal { get; set; }
        public string? Timestamp { get; set; }

        public DateTime CheckedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? FailedUntilUtc { get; set; }

        public string? ErrorMessage { get; set; }
    }
}