using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GbxMapBrowser.Models.TrackmaniaRecords;
using GbxMapBrowser.Services.TrackmaniaRecords;
using GbxMapBrowser.Windows;

namespace GbxMapBrowser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _curFolder = "";
        private readonly MapInfoViewModel _mapInfoViewModel = new();
        private readonly GbxGameViewModel _gbxGameViewModel = new();

        private readonly TrackmaniaRecordImportService _trackmaniaRecordImportService = new(new TrackmaniaRecordDatabase());

        private readonly TrackmaniaOnlineRecordService _trackmaniaOnlineRecordService = new(
            new TrackmaniaRecordDatabase(),
            new TrackmaniaOnlineRecordCacheService(),
            new TrackmaniaAccountIdService(),
            new TrackmaniaDedicatedServerCredentialService()
        );
        private readonly SearchOption _searchOption;
        private readonly List<FolderAndFileInfo> _selectedItems = [];
        private object _mapBrowserContent;



        #region Initialization
        public MainWindow()
        {
            _curFolder = LoadDefaultMapFolder();
            _searchOption = SearchOption.TopDirectoryOnly;
            InitializeComponent();
            _mapBrowserContent = gamesListMenu.Content;
            LoadGbxGameList();
            UpdateMapPreviewVisibility(Properties.Settings.Default.ShowMapPreviewColumn);
            loadingLabel.DataContext = _mapInfoViewModel;
            LoadSorting();
            //Properties.Settings.Default.IsFirstRun = true;
            if (Properties.Settings.Default.IsFirstRun)
            {
                ShowGbxGamesWindow();
            }
        }

        private async void Window_LoadedAsync(object sender, RoutedEventArgs e)
        {
            await UpdateMapListAsync(_curFolder);

            Updater updater = new();
            bool isUpToDate = true;

            try
            {
                isUpToDate = await updater.IsUpToDate();
            }
            catch { }

            if (!isUpToDate)
            {
                MessageBoxResult result = MessageBox.Show("New update is available. \n\nDownload now?", "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    SettingsManager.SaveAllSettings(_gbxGameViewModel);
                    updater.DownloadUpdate();
                }
            }

            HistoryManager.UpdateListUI += HistoryManager_UpdateListUI;
        }
        #endregion

        private static string GetDefaultMapFolderSettingsPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return Path.Combine(
                localAppData,
                "GbxMapBrowser",
                "default-map-folder.txt"
            );
        }

        private static string LoadDefaultMapFolder()
        {
            string fallbackFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string settingsPath = GetDefaultMapFolderSettingsPath();

            try
            {
                if (!File.Exists(settingsPath))
                {
                    return fallbackFolder;
                }

                string savedFolder = File.ReadAllText(settingsPath).Trim();

                if (Directory.Exists(savedFolder))
                {
                    return savedFolder;
                }
            }
            catch
            {
                // Fall back to Documents if the saved folder cannot be loaded.
            }

            return fallbackFolder;
        }

        private static void SaveDefaultMapFolder(string folder)
        {
            string settingsPath = GetDefaultMapFolderSettingsPath();
            string settingsDirectory = Path.GetDirectoryName(settingsPath);

            if (!string.IsNullOrWhiteSpace(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            File.WriteAllText(settingsPath, folder);
        }

        private static string GetDownloadsFolder()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return Path.Combine(userProfile, "Downloads");
        }

        private static string GetAvailableDestinationPath(string destinationFolder, string fileName)
        {
            string destinationPath = Path.Combine(destinationFolder, fileName);

            if (!File.Exists(destinationPath))
            {
                return destinationPath;
            }

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            int copyNumber = 1;

            while (true)
            {
                string newFileName = $"{nameWithoutExtension} ({copyNumber}){extension}";
                string newDestinationPath = Path.Combine(destinationFolder, newFileName);

                if (!File.Exists(newDestinationPath))
                {
                    return newDestinationPath;
                }

                copyNumber++;
            }
        }

        #region HistoryManager
        private void HistoryManager_UpdateListUI(object sender, EventArgs e)
        {
            string tooltip = "";
            HistoryManager.HistoryListMinimal.ForEach(x => tooltip += x + "\n");
            tooltip = tooltip.Remove(tooltip.Length - 1);
            undoButton.ToolTip = tooltip;
            redoButton.ToolTip = tooltip;
        }
        #endregion

        #region GbxGameListInit
        private void LoadGbxGameList()
        {
            gamesListMenu.DataContext = _gbxGameViewModel;
            openInComboBox.DataContext = _gbxGameViewModel;
            _gbxGameViewModel.LoadGames();
        }

        private void ShowGbxGamesWindow()
        {
            SettingsWindow settingsWindow = new(_gbxGameViewModel);
            if (IsVisible && this is not null)
                settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
        #endregion

        #region GbxGameListMethods
        private void ManageGamesButton_Click(object sender, RoutedEventArgs e)
        {
            ShowGbxGamesWindow();
        }


        private void DetectOnlineTimesButton_Click(object sender, RoutedEventArgs e)
        {
            TrackmaniaAccountIdWindow window = new()
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void TrackmaniaExchangeButton_Click(object sender, RoutedEventArgs e)
        {
            gamesListMenu.Content = new TrackmaniaExchangePage(
                LoadDefaultMapFolder(),
                () => gamesListMenu.Content = _mapBrowserContent
            );
        }

        private async void OrganizeMapsByMedalButton_Click(object sender, RoutedEventArgs e)
        {
            OrganizeMapsByMedalWindow window = new()
            {
                Owner = this
            };

            if (window.ShowDialog() != true)
            {
                return;
            }

            var selectedMedals = new HashSet<string>(window.SelectedMedals, StringComparer.OrdinalIgnoreCase);
            string defaultMapFolder = LoadDefaultMapFolder();

            if (!Directory.Exists(defaultMapFolder))
            {
                MessageBox.Show(
                    "Default map folder was not found:\n\n" + defaultMapFolder,
                    "Default map folder not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            Control organizeButton = sender as Control;

            if (organizeButton != null)
            {
                organizeButton.IsEnabled = false;
            }

            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                await Task.Run(() => _trackmaniaRecordImportService.Refresh(defaultMapFolder));

                Dictionary<string, TrackmaniaMapRecord> recordsByUid = await Task.Run(() =>
                    _trackmaniaRecordImportService
                        .GetAllRecords()
                        .Where(record => !string.IsNullOrWhiteSpace(record.MapUid))
                        .GroupBy(record => record.MapUid)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
                );

                foreach (string medal in selectedMedals)
                {
                    Directory.CreateDirectory(Path.Combine(defaultMapFolder, medal));
                }

                string[] mapFiles = await Task.Run(() => Directory
                    .EnumerateFiles(defaultMapFolder, "*.Gbx", SearchOption.TopDirectoryOnly)
                    .Where(file =>
                        file.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".Challenge.Gbx", StringComparison.OrdinalIgnoreCase))
                    .ToArray());

                Dictionary<string, int> movedByMedal = new(StringComparer.OrdinalIgnoreCase);

                foreach (string medal in selectedMedals)
                {
                    movedByMedal[medal] = 0;
                }
                int movedCount = 0;
                int skippedCount = 0;
                int failedCount = 0;

                foreach (string sourcePath in mapFiles)
                {
                    try
                    {
                        MapInfo mapInfo = await Task.Run(() => new MapInfo(sourcePath, true));

                        if (!mapInfo.IsWorking ||
                            string.IsNullOrWhiteSpace(mapInfo.MapUid) ||
                            !recordsByUid.TryGetValue(mapInfo.MapUid, out TrackmaniaMapRecord record) ||
                            string.IsNullOrWhiteSpace(record.Medal) ||
                            !selectedMedals.Contains(record.Medal))
                        {
                            skippedCount++;
                            continue;
                        }

                        string destinationFolder = Path.Combine(defaultMapFolder, record.Medal);
                        string destinationPath = GetAvailableDestinationPath(
                            destinationFolder,
                            Path.GetFileName(sourcePath)
                        );

                        File.Move(sourcePath, destinationPath);

                        movedCount++;
                        movedByMedal[record.Medal]++;
                    }
                    catch
                    {
                        failedCount++;
                    }
                }

                _curFolder = defaultMapFolder;

                await UpdateMapListAsync(_curFolder);
                HistoryManager.AddToHistory(_curFolder);

                string movedBreakdown = string.Join(
                    "\n",
                    movedByMedal.Select(pair => $"{pair.Key}: {pair.Value}")
                );

                MessageBox.Show(
                    $"Moved {movedCount} map file(s).\n\n" +
                    movedBreakdown +
                    $"\n\nSkipped: {skippedCount}\n" +
                    $"Failed: {failedCount}\n\n" +
                    $"Default folder:\n{defaultMapFolder}",
                    "Maps organized",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Organize maps failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            finally
            {
                Mouse.OverrideCursor = null;

                if (organizeButton != null)
                {
                    organizeButton.IsEnabled = true;
                }
            }
        }

        private async void MoveDownloadedMapsButton_Click(object sender, RoutedEventArgs e)
        {
            string downloadsFolder = GetDownloadsFolder();
            string defaultMapFolder = LoadDefaultMapFolder();

            if (!Directory.Exists(downloadsFolder))
            {
                MessageBox.Show(
                    "Downloads folder was not found:\n\n" + downloadsFolder,
                    "Downloads folder not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            if (!Directory.Exists(defaultMapFolder))
            {
                MessageBox.Show(
                    "Default map folder was not found:\n\n" + defaultMapFolder,
                    "Default map folder not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            string[] mapFiles = Directory
                .EnumerateFiles(downloadsFolder, "*", SearchOption.TopDirectoryOnly)
                .Where(file => file.EndsWith(".Map.Gbx", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (mapFiles.Length == 0)
            {
                MessageBox.Show(
                    "No .Map.Gbx files were found in Downloads.",
                    "No maps found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            int movedCount = 0;
            int overwrittenCount = 0;
            int keptBothCount = 0;
            int skippedCount = 0;

            foreach (string sourcePath in mapFiles)
            {
                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = Path.Combine(defaultMapFolder, fileName);

                if (File.Exists(destinationPath))
                {
                    MessageBoxResult duplicateChoice = MessageBox.Show(
                        "This map already exists in your default map folder:\n\n" +
                        fileName +
                        "\n\nYes = overwrite existing map\nNo = keep both\nCancel = skip this map",
                        "Duplicate map found",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question
                    );

                    if (duplicateChoice == MessageBoxResult.Cancel)
                    {
                        skippedCount++;
                        continue;
                    }

                    if (duplicateChoice == MessageBoxResult.Yes)
                    {
                        File.Delete(destinationPath);
                        File.Move(sourcePath, destinationPath);

                        movedCount++;
                        overwrittenCount++;

                        continue;
                    }

                    if (duplicateChoice == MessageBoxResult.No)
                    {
                        destinationPath = GetAvailableDestinationPath(defaultMapFolder, fileName);
                        File.Move(sourcePath, destinationPath);

                        movedCount++;
                        keptBothCount++;

                        continue;
                    }
                }

                File.Move(sourcePath, destinationPath);
                movedCount++;
            }

            _curFolder = defaultMapFolder;

            await UpdateMapListAsync(_curFolder);
            HistoryManager.AddToHistory(_curFolder);

            MessageBox.Show(
                $"Moved {movedCount} map file(s).\n\n" +
                $"Overwritten: {overwrittenCount}\n" +
                $"Kept both: {keptBothCount}\n" +
                $"Skipped: {skippedCount}\n\n" +
                $"Destination:\n{defaultMapFolder}",
                "Maps moved",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private async void GamesListMenu_ItemClick(object sender, MahApps.Metro.Controls.ItemClickEventArgs args)
        {
            if (gamesListMenu.SelectedItem == null) return;

            var selGame = (GbxGame)gamesListMenu.SelectedItem;
            if (!selGame.IsVisibleInGameList) return;

            gamesListMenu.Content = _mapBrowserContent;

            // Assign selection of the game
            _gbxGameViewModel.SelectedGbxGame = selGame;
            openInComboBox.SelectedItem = selGame.IsVisibleInGameLaunchMenu ? selGame : (object)null;
            await Task.Delay(100);

            // Assign sorting
            _mapInfoViewModel.SortKind = selGame.DefaultSortKind;

            // Load the folder, add it to history
            _curFolder = selGame.MapsFolder;

            await UpdateMapListAsync(selGame.MapsFolder);
            HistoryManager.AddToHistory(_curFolder);
            await Task.CompletedTask;
        }
        #endregion

        #region GetDataAndUpdateUI

        private async Task UpdateMapListAsync(string mapsFolder)
        {
            _mapInfoViewModel.IsLoading = true;

            UpdateMapPreview(null);
            _mapInfoViewModel.ClearMapList();

            Dictionary<string, TrackmaniaMapRecord> trackmaniaRecordsByUid = [];

            try
            {
                await Task.Run(() => _trackmaniaRecordImportService.Refresh(mapsFolder));

                trackmaniaRecordsByUid = await Task.Run(() =>
                    _trackmaniaRecordImportService
                        .GetAllRecords()
                        .Where(record => !string.IsNullOrWhiteSpace(record.MapUid))
                        .GroupBy(record => record.MapUid)
                        .ToDictionary(group => group.Key, group => group.First())
                );
            }
            catch
            {
                // Do not block the app if PB import fails.
            }

            //update enabled/disabled navigation buttons
            undoButton.IsEnabled = HistoryManager.CanUndo;
            redoButton.IsEnabled = HistoryManager.CanRedo;

            Application.Current.Dispatcher.Invoke(() =>
            {
                mapListBox.ItemsSource = null;
                currentFolderTextBox.Text = mapsFolder;
            });

            string[] folders = await Task.Run(() => FileOperations.TryGetFolders(mapsFolder));
            var mapFiles = await Task.Run(() => FileOperations.TryGetMapPaths(mapsFolder, _searchOption));
            int i = 0;

            foreach (var folderPath in folders)
            {
                await _mapInfoViewModel.AddFolderAsync(folderPath);
                i++;
            }

            foreach (string mapPath in mapFiles)
            {
                await _mapInfoViewModel.AddMapAsync(mapPath);
                i++;
            }

            _mapInfoViewModel.ApplyTrackmaniaRecords(trackmaniaRecordsByUid);

            await _mapInfoViewModel.SortMapListAsync();

            mapListBox.ItemsSource = _mapInfoViewModel.MapList;
            sortMapsComboBox.Text = Sorting.Kinds[(int)_mapInfoViewModel.SortKind];

            mapListBox.Items.Refresh();
            UpdatePbStatsLabel();
            _mapInfoViewModel.IsLoading = false;
        }
        #endregion

        #region AdressBarButtonsEvents

        private async void RefreshPbDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            string defaultMapFolder = LoadDefaultMapFolder();

            if (!Directory.Exists(defaultMapFolder))
            {
                MessageBox.Show(
                    "Default map folder was not found:\n\n" + defaultMapFolder,
                    "Default map folder not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            RefreshPbScopeWindow window = new(defaultMapFolder)
            {
                Owner = this
            };

            if (window.ShowDialog() != true)
            {
                return;
            }

            List<RefreshPbFolderTarget> folderTargets = GetRefreshPbFolderTargets(
                defaultMapFolder,
                window.SelectedFolderNames
            );

            List<RefreshPbFolderTarget> existingFolderTargets = folderTargets
                .Where(target => Directory.Exists(target.Path))
                .ToList();

            if (existingFolderTargets.Count == 0)
            {
                MessageBox.Show(
                    "None of the selected folders exist yet.",
                    "No folders to refresh",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            Control refreshButton = sender as Control;

            if (refreshButton != null)
            {
                refreshButton.IsEnabled = false;
            }

            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                TrackmaniaRecordImportResult importResult = await Task.Run(() => _trackmaniaRecordImportService.Refresh(
                    existingFolderTargets.Select(target => target.Path)
                ));

                TrackmaniaOnlineRecordRefreshResult onlineResult = new();

                foreach (RefreshPbFolderTarget folderTarget in existingFolderTargets)
                {
                    TrackmaniaOnlineRecordRefreshResult folderOnlineResult =
                        await _trackmaniaOnlineRecordService.RefreshMissingOnlineRecordsAsync(folderTarget.Path);

                    AddOnlineRefreshResult(onlineResult, folderOnlineResult);
                }

                await UpdateMapListAsync(_curFolder);

                string missingFoldersText = GetMissingRefreshFoldersText(folderTargets, existingFolderTargets);

                MessageBox.Show(
                    "Refresh PB finished.\n\n" +
                    $"Folders refreshed: {existingFolderTargets.Count} of {folderTargets.Count}\n" +
                    $"Scope: {GetRefreshScopeText(window.SelectedScope)}\n" +
                    missingFoldersText +
                    $"Found offline PBs: {importResult.LocalPersonalBestCount}\n" +
                    $"Maps in selected folders: {onlineResult.MapRecordsInCurrentFolder}\n" +
                    $"Missing PB before online check: {onlineResult.MissingPersonalBestCount}\n" +
                    $"Used cached online PB: {onlineResult.AppliedCachedFoundCount}\n" +
                    $"Skipped cached NotFound: {onlineResult.SkippedCachedNotFoundCount}\n" +
                    $"Skipped temporary Failed: {onlineResult.SkippedTemporaryFailedCount}\n" +
                    $"Checked online now: {onlineResult.CheckedOnlineCount}\n" +
                    $"Found online: {onlineResult.FoundOnlineCount}\n" +
                    $"Not found online: {onlineResult.NotFoundOnlineCount}\n" +
                    $"Failed: {onlineResult.FailedCount}",
                    "Refresh PB",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Refresh PB failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            finally
            {
                Mouse.OverrideCursor = null;

                if (refreshButton != null)
                {
                    refreshButton.IsEnabled = true;
                }
            }
        }

        private static string GetRefreshScopeText(RefreshPbScopeWindow.RefreshPbScope selectedScope)
        {
            return selectedScope switch
            {
                RefreshPbScopeWindow.RefreshPbScope.DefaultMapsOnly => "Default folder maps only",
                RefreshPbScopeWindow.RefreshPbScope.SelectedFolders => "Selected folders",
                _ => "All default map folders"
            };
        }

        private static List<RefreshPbFolderTarget> GetRefreshPbFolderTargets(
            string defaultMapFolder,
            IReadOnlyCollection<string> selectedFolderNames
        )
        {
            var folderTargets = new List<RefreshPbFolderTarget>();

            foreach (string selectedFolderName in selectedFolderNames)
            {
                folderTargets.Add(new RefreshPbFolderTarget(
                    selectedFolderName,
                    GetRefreshPbFolderPath(defaultMapFolder, selectedFolderName)
                ));
            }

            return folderTargets;
        }

        private static string GetRefreshPbFolderPath(string defaultMapFolder, string folderName)
        {
            return folderName switch
            {
                RefreshPbFolderNames.DefaultMaps => defaultMapFolder,
                _ => Path.Combine(defaultMapFolder, folderName)
            };
        }

        private static string GetMissingRefreshFoldersText(
            List<RefreshPbFolderTarget> folderTargets,
            List<RefreshPbFolderTarget> existingFolderTargets
        )
        {
            var existingLabels = new HashSet<string>(
                existingFolderTargets.Select(target => target.Label),
                StringComparer.OrdinalIgnoreCase
            );

            List<string> missingLabels = folderTargets
                .Where(target => !existingLabels.Contains(target.Label))
                .Select(target => target.Label)
                .ToList();

            if (missingLabels.Count == 0)
            {
                return "";
            }

            return "Missing folders skipped: " + string.Join(", ", missingLabels) + "\n";
        }

        private static void AddOnlineRefreshResult(
            TrackmaniaOnlineRecordRefreshResult total,
            TrackmaniaOnlineRecordRefreshResult result
        )
        {
            total.MapRecordsInCurrentFolder += result.MapRecordsInCurrentFolder;
            total.MissingPersonalBestCount += result.MissingPersonalBestCount;
            total.AppliedCachedFoundCount += result.AppliedCachedFoundCount;
            total.SkippedCachedNotFoundCount += result.SkippedCachedNotFoundCount;
            total.SkippedTemporaryFailedCount += result.SkippedTemporaryFailedCount;
            total.CheckedOnlineCount += result.CheckedOnlineCount;
            total.FoundOnlineCount += result.FoundOnlineCount;
            total.NotFoundOnlineCount += result.NotFoundOnlineCount;
            total.FailedCount += result.FailedCount;
        }

        private sealed class RefreshPbFolderTarget
        {
            public RefreshPbFolderTarget(string label, string path)
            {
                Label = label;
                Path = path;
            }

            public string Label { get; }
            public string Path { get; }
        }

        private void UpdatePbStatsLabel()
        {
            try
            {
                var records = _trackmaniaRecordImportService.GetAllRecords();

                int cachedMaps = records.Count;
                int withPersonalBest = records.Count(record => record.PersonalBestMs is not null);
                int withMapFile = records.Count(record => record.HasSeenMapFile);
                int knownMedals = records.Count(record =>
                    !string.Equals(record.Medal, "Unknown", StringComparison.OrdinalIgnoreCase)
                );

                pbStatsLabel.Content =
                    $"PB cache: {cachedMaps} maps | PBs: {withPersonalBest} | Medals: {knownMedals} | Map files: {withMapFile}";
            }
            catch
            {
                pbStatsLabel.Content = "PB cache: unavailable";
            }
        }

        private async void GoDefaultFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string defaultFolder = LoadDefaultMapFolder();

            if (!Directory.Exists(defaultFolder))
            {
                MessageBox.Show(
                    "The saved default folder no longer exists:\n\n" + defaultFolder,
                    "Default folder not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            _curFolder = defaultFolder;

            await UpdateMapListAsync(_curFolder);
            HistoryManager.AddToHistory(_curFolder);
        }

        private async void SetDefaultFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string folder = currentFolderTextBox.Text.Trim();

            if (!Directory.Exists(folder))
            {
                MessageBox.Show(
                    "This folder does not exist:\n\n" + folder,
                    "Invalid default folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            SaveDefaultMapFolder(folder);

            _curFolder = folder;

            await UpdateMapListAsync(_curFolder);
            HistoryManager.AddToHistory(_curFolder);

            MessageBox.Show(
                "Default map folder saved:\n\n" + folder,
                "Default folder saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private async void RefreshMapsButton_Click(object sender, RoutedEventArgs e)
        {
            await UpdateMapListAsync(_curFolder);
        }

        private void OpenInExplorerButton_Click(object sender, RoutedEventArgs e)
        {
            FileOperations.OpenInExplorer(_curFolder);
        }

        private async void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            _curFolder = await Task.Run(HistoryManager.RequestPrev);
            await UpdateMapListAsync(_curFolder);
        }

        private async void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            _curFolder = await Task.Run(HistoryManager.RequestNext);
            await UpdateMapListAsync(_curFolder);
        }

        private async void ParentFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var parentFolder = Directory.GetParent(_curFolder);
                if (parentFolder != null)
                    _curFolder = parentFolder.FullName;
            }
            catch (Exception ee)
            {
                MessageBox.Show(ee.Message);
            }
            await UpdateMapListAsync(_curFolder);
            HistoryManager.AddToHistory(_curFolder);

        }
        #endregion

        #region LaunchingItemAndMapListSelection
        private async Task MapListBoxLaunchItemAsync(FolderAndFileInfo item)
        {
            if (_mapInfoViewModel.IsLoading) return;
            if (item is FolderInfo selFolder)
            {
                _curFolder = selFolder.FullPath;
                HistoryManager.AddToHistory(_curFolder);
                await UpdateMapListAsync(_curFolder);
                UpdateMapPreview(null);
            }
            else if (item is MapInfo mapInfo)
            {
                var selGame = GetSelectedGame();
                if (selGame == null) return;
                mapInfo.OpenMap(selGame);
            }
            else if (item is null)
                MessageBox.Show("Select a map to launch", "Impossible to load map", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        private async Task LaunchItemsAsync(List<FolderAndFileInfo> items)
        {
            if (items.Count == 0) return;
            if (items.Count == 1)
            {
                await MapListBoxLaunchItemAsync(items[0]);
                return;
            }

            MessageBoxResult result = MessageBox.Show("Launch all maps?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No) return;

            foreach (FolderAndFileInfo item in items)
            {
                if (item is MapInfo)
                    await MapListBoxLaunchItemAsync(item);
            }
        }

        private async void MapListBox_MouseDoubleClickAsync(object sender, MouseButtonEventArgs e)
        {
            await LaunchItemsAsync(_selectedItems);
        }
        private void MapListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedItems.Clear();
            if (mapListBox.SelectedItem == null) return;
            foreach (FolderAndFileInfo selItem in mapListBox.SelectedItems)
            {
                _selectedItems.Add(selItem);
            }
            UpdateMapPreview(_selectedItems);
        }
        #endregion

        #region MapPreviewPane

        private void UpdateMapPreviewVisibility(bool isVis)
        {
            mapPreviewColumn.Width = isVis ? new GridLength(1, GridUnitType.Star) : new GridLength(0, GridUnitType.Star);
        }

        private void UpdateMapPreview(List<FolderAndFileInfo> data)
        {
            if (mapPreviewFrame.CanGoBack)
                mapPreviewFrame.RemoveBackEntry();
            mapPreviewFrame.Content = null;
            if (data == null) return;
            mapPreviewFrame.Content = new MapPreviewPage(data);
        }

        private GbxGame GetSelectedGame()
        {
            var selGame = (GbxGame)openInComboBox.SelectedItem;
            if (selGame == null || !selGame.IsVisibleInGameLaunchMenu)
            {
                MessageBox.Show("Choose a game to launch your map with!", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                return null;
            }
            return selGame;
        }

        private async void ButtonPlay_Click(object sender, RoutedEventArgs e)
        {
            await LaunchItemsAsync(_selectedItems);
        }
        #endregion

        #region DragInOutMaps

        private Point initialMousePosition;
        private void DragOutMaps(FolderAndFileInfo[] mapInfos)
        {
            List<string> files = [];
            Array.ForEach(mapInfos, mfo => files.Add(mfo.FullPath));

            var mapFile = new DataObject(DataFormats.FileDrop, files.ToArray());
            DragDrop.DoDragDrop(mapListBox, mapFile, DragDropEffects.Copy);
        }

        private bool IsSelectedItemByName(string name)
        {
            foreach (var mapInfo in _selectedItems)
            {
                if (mapInfo.DisplayName == name) return true;
            }
            return false;
        }

        private void MapListBox_PreviewMouseleftButonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2) return;
            if (_selectedItems.Count == 0) return;

            initialMousePosition = e.GetPosition(mapListBox);
        }

        private void MapListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;
            if (e.OriginalSource is MahApps.Metro.Controls.MetroThumb)
                return;

            // To avoid unintentional drag
            Point mousePosition = e.GetPosition(mapListBox);
            Vector diff = initialMousePosition - mousePosition;

            if (Math.Abs(diff.X) < 10 && Math.Abs(diff.Y) < 10)
                return;

            bool canCopy = false;
            e.Handled = true;
            // Check if it is not dragging out non-selected item
            if (e.OriginalSource is Border border)
            {
                if (border.Child is ContentPresenter contentPresenter)
                {
                    var mapName = contentPresenter.Content.ToString();
                    canCopy = IsSelectedItemByName(mapName);
                }
            }
            else if (e.OriginalSource is TextBlock textBlock)
            {
                canCopy = IsSelectedItemByName(textBlock.Text);
            }

            if (!canCopy) return;

            if (_mapInfoViewModel.IsLoading) return;
            DragOutMaps([.. _selectedItems]);
        }

        private async void MapListBox_Drop(object sender, DragEventArgs e)
        {
            if (_mapInfoViewModel.IsLoading) return;
            string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            if (paths.Length == 0) return;
            try
            {
                FileOperations.CopyFilesToFolder(paths, _curFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            await UpdateMapListAsync(_curFolder);

        }
        #endregion

        #region ItemOperations

        private async Task DeleteSelectedItemsAsync()
        {
            // Delete all?
            if (_selectedItems.Count > 1)
            {
                var result = MessageBox.Show("Are you sure to delete " + _selectedItems.Count + " items?", "", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    foreach (FolderAndFileInfo item in _selectedItems)
                    {
                        await item.DeleteAsync();
                    }
                return;
            }

            //Delete One
            if (_selectedItems[0] is FolderAndFileInfo itemInfo)
            {
                var messageBoxResult = MessageBox.Show($"Are you sure to delete {itemInfo.DisplayName} \nPath: {itemInfo.FullPath}?", "Delete file?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    await itemInfo.DeleteAsync();
                }
            }
        }

        private async Task MoveMapToSkippedFolderAsync(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return;
            }

            string skippedFolder = Path.Combine(_curFolder, "skipped");
            Directory.CreateDirectory(skippedFolder);

            string destinationPath = GetAvailableDestinationPath(
                skippedFolder,
                Path.GetFileName(mapInfo.FullPath)
            );

            await Task.Run(() => File.Move(mapInfo.FullPath, destinationPath));
            await UpdateMapListAsync(_curFolder);
        }
        #endregion

        #region CopyAndPaste
        private void CopySelectedItemsToMemory()
        {
            if (_selectedItems.Count == 0)
                return;
            var fileDropList = new StringCollection();
            foreach (var item in _selectedItems)
                fileDropList.Add(item.FullPath);
            Clipboard.SetFileDropList(fileDropList);
        }

        private async Task PasteItemsFromMemoryAsync()
        {
            string[] clipboardText = null;
            await Task.Run(() =>
            Dispatcher.Invoke(() =>
                clipboardText = (string[])Clipboard.GetDataObject().GetData(DataFormats.FileDrop)
            ));

            if (clipboardText is null)
            {
                throw new Exception("The clipboard is empty.");
            }
            else
            {
                FileOperations.CopyFilesToFolder(clipboardText, _curFolder);
                await UpdateMapListAsync(_curFolder);
            }
        }
        #endregion

        #region KeyPresses

        private async void CurrentFolderTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _curFolder = currentFolderTextBox.Text;
                await UpdateMapListAsync(_curFolder);
                HistoryManager.AddToHistory(_curFolder);
            }
        }

        private async void MapListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Launch / Open
            if (e.Key == Key.Enter)
            {
                await LaunchItemsAsync(_selectedItems);
            }

            // Parent directory
            if (e.Key == Key.Back)
            {
                try
                {
                    var parentFolder = Directory.GetParent(_curFolder);
                    if (parentFolder != null)
                        _curFolder = parentFolder.FullName;
                }
                catch (Exception ee)
                {
                    MessageBox.Show(ee.Message);
                }
                await UpdateMapListAsync(_curFolder);
                HistoryManager.AddToHistory(_curFolder);
            }

            // Undo
            if (Keyboard.Modifiers == ModifierKeys.Alt && Keyboard.IsKeyDown(Key.Left))
                if (HistoryManager.CanUndo)
                    UndoButton_Click(this, null);

            // Redo
            if (Keyboard.Modifiers == ModifierKeys.Alt && Keyboard.IsKeyDown(Key.Right))
                if (HistoryManager.CanRedo)
                    RedoButton_Click(this, null);

            // Delete
            if (e.Key == Key.Delete)
            {
                if (_selectedItems.Count == 0)
                    return;
                await DeleteSelectedItemsAsync();
                await UpdateMapListAsync(_curFolder);
            }

            // Show context menu
            if (Keyboard.Modifiers == ModifierKeys.Shift && Keyboard.IsKeyDown(Key.F10))   //SHIFT + F10        
                ShowContextMenu();

            // Rename
            if (e.Key == Key.F2)
            {
                if (_selectedItems.Count == 0)
                    return;
                if (_selectedItems.Count > 1)
                {
                    MessageBox.Show("Cannot rename multiple maps", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (_selectedItems[0] is MapInfo mapInfo)
                    {
                        MapOperations.RenameMap(mapInfo);
                        await UpdateMapListAsync(_curFolder);
                    }
                    return;
                }
                if (_selectedItems[0] is MapInfo)
                    FileOperations.RenameFile(_selectedItems[0].FullPath);
                else if (_selectedItems[0] is FolderInfo)
                    FileOperations.RenameFolder(_selectedItems[0].FullPath);
                await UpdateMapListAsync(_curFolder);


            }

            // Properties
            if (Keyboard.IsKeyDown(Key.Enter) && Keyboard.Modifiers == ModifierKeys.Alt) //ALT + ENTER
                if (_selectedItems.Count == 1)
                    FileOperations.ShowFileProperties(_selectedItems[0].FullPath);

            // Refresh
            if (e.Key == Key.F5)
            {
                RefreshMapsButton_Click(null, null);
            }

            // Copy
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CopySelectedItemsToMemory();
            }

            // Paste
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                try
                {
                    await PasteItemsFromMemoryAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region ContextMenus

        private void UpdateContextMenu()
        {
            if (_selectedItems.Count == 0)
            {
                mapListBox.ContextMenu = (ContextMenu)FindResource("NoSelectionContextMenu");
                return;
            }

            if (_selectedItems.Count == 1)
            {
                if (_selectedItems[0] is MapInfo)
                    mapListBox.ContextMenu = (ContextMenu)FindResource("MapContextMenu");
                else if (_selectedItems[0] is FolderInfo)
                    mapListBox.ContextMenu = (ContextMenu)FindResource("FolderContextMenu");
                return;
            }

            mapListBox.ContextMenu = (ContextMenu)FindResource("MultiselectionContextMenu");
        }

        private void ShowContextMenu()
        {
            UpdateContextMenu();
            mapListBox.ContextMenu.PlacementTarget = mapListBox;
            mapListBox.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            mapListBox.ContextMenu.IsOpen = true;
        }

        private void MapListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (e.OriginalSource is ScrollViewer)
                mapListBox.SelectedItem = null;
            UpdateContextMenu();
            ((ListBox)sender).ContextMenu.PreviewMouseUp += ItemContextMenu_PreviewMouseUp;
        }

        private async void ItemContextMenu_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is not MenuItem) return;
            var selMenuItem = (MenuItem)e.Source;


            e.Handled = true; //avoid running this code more than once
            switch (selMenuItem.Header)
            {
                case "Open this folder in file explorer":
                    FileOperations.OpenInExplorer(_curFolder);
                    break;
                case "Refresh":
                    await UpdateMapListAsync(_curFolder);
                    break;
                case "Paste":
                    try
                    {
                        await PasteItemsFromMemoryAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case "New folder":
                    NewFolderWindow newFolderWindow = new();
                    newFolderWindow.ShowDialog();

                    if (string.IsNullOrEmpty(newFolderWindow.NewName)) return;
                    Directory.CreateDirectory(_curFolder + '\\' + newFolderWindow.NewName);
                    await UpdateMapListAsync(_curFolder);
                    break;
            }

            if (_selectedItems.Count == 0)
            {
                await Task.Delay(100);
                ((ContextMenu)sender).IsOpen = false;
                return;
            }
            var selItem = _selectedItems[0];
            string path = selItem.FullPath;

            switch (selMenuItem.Header)
            {

                case "Launch or open (all items)":
                case "Launch":
                case "Open":
                    await LaunchItemsAsync(_selectedItems);
                    break;
                case "Copy":
                    CopySelectedItemsToMemory();
                    break;
                case "Delete":
                    await DeleteSelectedItemsAsync();
                    await UpdateMapListAsync(_curFolder);
                    break;
                case "Rename file":
                    var oldMapName = selItem.FullPath;
                    FileOperations.RenameFile(oldMapName);
                    await UpdateMapListAsync(_curFolder);
                    break;
                case "Rename folder":
                    var oldName = selItem.FullPath;
                    FileOperations.RenameFolder(oldName);
                    await UpdateMapListAsync(_curFolder);
                    break;
                case "Rename map":
                    MapOperations.RenameMap(selItem as MapInfo);
                    await UpdateMapListAsync(_curFolder);
                    break;
                case "Move to skipped":
                    await MoveMapToSkippedFolderAsync(selItem as MapInfo);
                    break;
                case "Properties":
                    FileOperations.ShowFileProperties(path);
                    break;
            }
            await Task.Delay(100);
            ((ContextMenu)sender).IsOpen = false;
        }

        private void GameLibraryItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            ((StackPanel)sender).ContextMenu.PreviewMouseUp += GameLibraryItemContextMenu_PreviewMouseUp;
        }

        private async void GameLibraryItemContextMenu_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is not MenuItem)
                return;

            var selMenuItem = (MenuItem)e.Source;
            e.Handled = true; //avoid running this multiple times
            if (selMenuItem == null)
                return;
            var selGame = gamesListMenu.SelectedItem;
            if (selGame is GbxGame game)
            {
                if (selMenuItem.Header.ToString() == "Hide from the game library")
                {
                    game.IsVisibleInGameList = false;
                    await Task.Run(() => SettingsManager.SaveAllSettings(_gbxGameViewModel));
                }
                else
                {
                    await Task.Run(game.Launch);
                }
                ((ContextMenu)sender).IsOpen = false;
                await Task.Delay(100);
            }

        }
        #endregion

        #region Search

        private async void SearchMapsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await Task.Delay(200);
            string text = searchMapsTextBox.Text;
            if (text == "search for a map...")
                return;
            if (string.IsNullOrEmpty(text))
            {
                await UpdateMapListAsync(_curFolder);
                return;
            }

            _selectedItems.Clear();
            mapListBox.SelectedItems.Clear();
            await _mapInfoViewModel.FindMapsAsync(text);
            mapListBox.Items.Refresh();
        }

        private void SearchMapsTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (searchMapsTextBox.Text == "search for a map...")
                searchMapsTextBox.Text = "";
            searchMapsTextBox.Opacity = .9;
        }

        private void SearchMapsTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Set the color back
            searchMapsTextBox.Opacity = .5;

            // Restore default view if searchbox is empty
            if (!string.IsNullOrWhiteSpace(searchMapsTextBox.Text) || searchMapsTextBox.Text == "search for a map...")
                return;
            searchMapsTextBox.Text = "search for a map...";
        }
        #endregion

        #region Sorting
        private async void SortMapsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _mapInfoViewModel.SortKind = (Sorting.Kind)sortMapsComboBox.SelectedIndex;
            _gbxGameViewModel.SelectedGbxGame.DefaultSortKind = _mapInfoViewModel.SortKind;
            SettingsManager.SaveAllSettings(_gbxGameViewModel);
            await UpdateMapListAsync(_curFolder);
        }
        private void LoadSorting()
        {
            sortMapsComboBox.DataContext = _mapInfoViewModel;
            sortMapsComboBox.ItemsSource = Sorting.Kinds;
        }
        #endregion

    }
}
