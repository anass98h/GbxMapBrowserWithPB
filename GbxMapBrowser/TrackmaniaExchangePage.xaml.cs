using GbxMapBrowser.Services.TrackmaniaExchange;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GbxMapBrowser
{
    public partial class TrackmaniaExchangePage : UserControl
    {
        private const string OfficialCampaignsFolderName = "Official Campaigns";

        private readonly string _defaultMapFolder;
        private readonly Action _goBack;
        private readonly TrackmaniaExchangeService _trackmaniaExchangeService = new();
        private readonly ObservableCollection<TrackmaniaExchangeResultItem> _items = [];

        public TrackmaniaExchangePage(string defaultMapFolder, Action goBack)
        {
            InitializeComponent();

            _defaultMapFolder = defaultMapFolder;
            _goBack = goBack;
            resultsDataGrid.ItemsSource = _items;
            SetStatus("Choose a Trackmania Exchange action.");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _goBack();
        }

        private async void TrackOfTheDayButton_Click(object sender, RoutedEventArgs e)
        {
            NumberInputWindow window = new(
                "Track of the Day",
                "How many recent Track of the Day maps should be checked?",
                100,
                1,
                500
            )
            {
                Owner = Window.GetWindow(this)
            };

            if (window.ShowDialog() != true)
            {
                return;
            }

            await RunWithBusyStateAsync(async () =>
            {
                SetStatus("Loading Track of the Day maps from TMX...");
                IReadOnlyList<TmxMap> maps = await _trackmaniaExchangeService.GetRecentTrackOfTheDayMapsAsync(window.Value);

                SetStatus("Scanning local maps...");
                HashSet<string> existingMapUids = await Task.Run(() => ScanMapUids(_defaultMapFolder));

                int alreadyOwnedCount = 0;
                int downloadedCount = 0;
                int failedCount = 0;
                _items.Clear();

                foreach (TmxMap map in maps)
                {
                    bool alreadyOwned = !string.IsNullOrWhiteSpace(map.MapUid) &&
                        existingMapUids.Contains(map.MapUid);

                    var item = new TrackmaniaExchangeResultItem
                    {
                        Id = map.MapId,
                        Name = map.Name,
                        MapCount = 1,
                        DownloadedCount = alreadyOwned ? 1 : 0,
                        Status = alreadyOwned ? "Already downloaded" : "Missing"
                    };

                    _items.Add(item);

                    if (alreadyOwned)
                    {
                        alreadyOwnedCount++;
                        continue;
                    }

                    try
                    {
                        SetStatus($"Downloading {map.Name}...");
                        await _trackmaniaExchangeService.DownloadMapAsync(map, _defaultMapFolder);
                        existingMapUids.Add(map.MapUid);
                        item.DownloadedCount = 1;
                        item.Status = "Downloaded";
                        downloadedCount++;
                    }
                    catch
                    {
                        item.Status = "Failed";
                        failedCount++;
                    }

                    resultsDataGrid.Items.Refresh();
                }

                SetStatus(
                    $"Track of the Day check finished. Checked: {maps.Count}. " +
                    $"Already downloaded: {alreadyOwnedCount}. Downloaded: {downloadedCount}. Failed: {failedCount}."
                );
            });
        }

        private async void OfficialCampaignsButton_Click(object sender, RoutedEventArgs e)
        {
            await RunWithBusyStateAsync(LoadOfficialCampaignsAsync);
        }

        private async void DownloadSelectedCampaignButton_Click(object sender, RoutedEventArgs e)
        {
            if (resultsDataGrid.SelectedItem is not TrackmaniaExchangeResultItem item ||
                item.Campaign == null)
            {
                MessageBox.Show(
                    "Select a campaign first.",
                    "No campaign selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            await RunWithBusyStateAsync(async () =>
            {
                await DownloadCampaignAsync(item);
            });
        }

        private async Task LoadOfficialCampaignsAsync()
        {
            SetStatus("Loading official campaigns from TMX...");
            IReadOnlyList<TmxMappack> campaigns = await _trackmaniaExchangeService.GetOfficialCampaignsAsync();

            _items.Clear();

            foreach (TmxMappack campaign in campaigns)
            {
                SetStatus($"Checking {campaign.Name}...");

                IReadOnlyList<TmxMap> maps = await _trackmaniaExchangeService.GetMappackMapsAsync(campaign.Id);
                string campaignFolder = GetOfficialCampaignFolder(campaign.Name);
                HashSet<string> existingMapUids = await Task.Run(() => ScanMapUids(campaignFolder));
                int downloadedCount = maps.Count(map => !string.IsNullOrWhiteSpace(map.MapUid) && existingMapUids.Contains(map.MapUid));

                _items.Add(new TrackmaniaExchangeResultItem
                {
                    Id = campaign.Id,
                    Name = campaign.Name,
                    Status = GetCampaignStatus(downloadedCount, maps.Count),
                    MapCount = maps.Count,
                    DownloadedCount = downloadedCount,
                    Campaign = campaign,
                    CampaignMaps = maps
                });
            }

            SetStatus($"Loaded {campaigns.Count} official campaigns.");
        }

        private async Task DownloadCampaignAsync(TrackmaniaExchangeResultItem item)
        {
            IReadOnlyList<TmxMap> maps = item.CampaignMaps.Count > 0
                ? item.CampaignMaps
                : await _trackmaniaExchangeService.GetMappackMapsAsync(item.Id);

            string campaignFolder = GetOfficialCampaignFolder(item.Name);
            HashSet<string> existingMapUids = await Task.Run(() => ScanMapUids(campaignFolder));
            int downloadedNowCount = 0;
            int failedCount = 0;

            foreach (TmxMap map in maps)
            {
                if (!string.IsNullOrWhiteSpace(map.MapUid) && existingMapUids.Contains(map.MapUid))
                {
                    continue;
                }

                try
                {
                    SetStatus($"Downloading {item.Name}: {map.Name}...");
                    await _trackmaniaExchangeService.DownloadMapAsync(map, campaignFolder);

                    if (!string.IsNullOrWhiteSpace(map.MapUid))
                    {
                        existingMapUids.Add(map.MapUid);
                    }

                    downloadedNowCount++;
                }
                catch
                {
                    failedCount++;
                }
            }

            item.CampaignMaps = maps;
            item.MapCount = maps.Count;
            item.DownloadedCount = maps.Count(map => !string.IsNullOrWhiteSpace(map.MapUid) && existingMapUids.Contains(map.MapUid));
            item.Status = GetCampaignStatus(item.DownloadedCount, item.MapCount);
            resultsDataGrid.Items.Refresh();

            SetStatus(
                $"Campaign download finished. {item.Name}: downloaded now {downloadedNowCount}, " +
                $"downloaded total {item.DownloadedCount}/{item.MapCount}, failed {failedCount}."
            );
        }

        private async Task RunWithBusyStateAsync(Func<Task> action)
        {
            SetButtonsEnabled(false);
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                SetStatus("TMX action failed: " + ex.Message);
                MessageBox.Show(
                    ex.Message,
                    "Trackmania Exchange",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            finally
            {
                Mouse.OverrideCursor = null;
                SetButtonsEnabled(true);
            }
        }

        private void SetButtonsEnabled(bool isEnabled)
        {
            trackOfTheDayButton.IsEnabled = isEnabled;
            officialCampaignsButton.IsEnabled = isEnabled;
            downloadSelectedCampaignButton.IsEnabled = isEnabled;
        }

        private string GetOfficialCampaignFolder(string campaignName)
        {
            string folderName = GetSafeFolderName(campaignName);

            return Path.Combine(
                _defaultMapFolder,
                OfficialCampaignsFolderName,
                folderName
            );
        }

        private static string GetCampaignStatus(int downloadedCount, int totalCount)
        {
            if (totalCount == 0)
            {
                return "No maps found";
            }

            if (downloadedCount == 0)
            {
                return "Not downloaded";
            }

            if (downloadedCount >= totalCount)
            {
                return "Downloaded";
            }

            return "Partial";
        }

        private static HashSet<string> ScanMapUids(string folder)
        {
            HashSet<string> mapUids = new(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(folder))
            {
                return mapUids;
            }

            IEnumerable<string> mapFiles;

            try
            {
                mapFiles = Directory.EnumerateFiles(folder, "*.Gbx", SearchOption.AllDirectories)
                    .Where(file =>
                        file.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".Challenge.Gbx", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch
            {
                return mapUids;
            }

            foreach (string mapFile in mapFiles)
            {
                try
                {
                    MapInfo mapInfo = new(mapFile, true);

                    if (mapInfo.IsWorking && !string.IsNullOrWhiteSpace(mapInfo.MapUid))
                    {
                        mapUids.Add(mapInfo.MapUid);
                    }
                }
                catch
                {
                    // Skip unreadable maps while checking local ownership.
                }
            }

            return mapUids;
        }

        private static string GetSafeFolderName(string folderName)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                folderName = folderName.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(folderName)
                ? "Campaign"
                : folderName.Trim();
        }

        private void SetStatus(string status)
        {
            statusTextBlock.Text = status;
        }
    }

    public sealed class TrackmaniaExchangeResultItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public int MapCount { get; set; }
        public int DownloadedCount { get; set; }
        public TmxMappack Campaign { get; set; }
        public IReadOnlyList<TmxMap> CampaignMaps { get; set; } = [];
    }
}
