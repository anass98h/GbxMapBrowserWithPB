#nullable enable

using System;

namespace GbxMapBrowser.Models.TrackmaniaRecords
{
    public sealed class TrackmaniaMapRecord
    {
        public required string MapUid { get; set; }

        public string? MapName { get; set; }
        public string? ColoredMapName { get; set; }
        public string? Environment { get; set; }
        public string? AuthorLogin { get; set; }

        public int? PersonalBestMs { get; set; }
        public string? PersonalBest { get; set; }

        public int? BronzeMs { get; set; }
        public int? SilverMs { get; set; }
        public int? GoldMs { get; set; }
        public int? AuthorMs { get; set; }

        public string Medal { get; set; } = "Unknown";
        public string PersonalBestSource { get; set; } = "Unknown";

        public bool HasSeenReplay { get; set; }
        public bool HasSeenMapFile { get; set; }

        public string? LastReplayFile { get; set; }
        public DateTime? LastReplayWriteTimeUtc { get; set; }

        public string? LastMapFile { get; set; }
        public DateTime? LastMapWriteTimeUtc { get; set; }

        public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    }
}