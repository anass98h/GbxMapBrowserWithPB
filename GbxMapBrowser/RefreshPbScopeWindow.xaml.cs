using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GbxMapBrowser
{
    public partial class RefreshPbScopeWindow : Window
    {
        public enum RefreshPbScope
        {
            AllFolders,
            DefaultMapsOnly,
            SelectedFolders
        }

        public RefreshPbScope SelectedScope { get; private set; } = RefreshPbScope.AllFolders;
        public IReadOnlyCollection<string> SelectedFolderNames { get; private set; } = [];
        private readonly Dictionary<CheckBox, string> _folderNamesByCheckBox = [];

        public RefreshPbScopeWindow(
            string defaultMapFolder,
            string title = "Refresh PB Scope",
            string description = "Choose which map folders should be refreshed.",
            string confirmButtonText = "Refresh PB"
        )
        {
            InitializeComponent();
            Title = title;
            descriptionTextBlock.Text = description;
            confirmButton.Content = confirmButtonText;
            LoadFolderCheckBoxes(defaultMapFolder);
            SetAllCheckBoxes(true);
            SetFolderCheckBoxesEnabled(false);
        }

        private void RefreshModeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (defaultMapsOnlyRadioButton == null)
            {
                return;
            }

            if (allFoldersRadioButton.IsChecked == true)
            {
                SetAllCheckBoxes(true);
                SetFolderCheckBoxesEnabled(false);
                return;
            }

            if (defaultMapsOnlyRadioButton.IsChecked == true)
            {
                SetAllCheckBoxes(false);
                SetCheckBoxByFolderName(RefreshPbFolderNames.DefaultMaps, true);
                SetFolderCheckBoxesEnabled(false);
                return;
            }

            SetFolderCheckBoxesEnabled(true);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedScope = GetSelectedScope();
            List<string> selectedFolders = GetCheckedFolderNames();

            if (selectedFolders.Count == 0)
            {
                MessageBox.Show(
                    "Choose at least one folder.",
                    "No folders selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            SelectedFolderNames = selectedFolders;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private RefreshPbScope GetSelectedScope()
        {
            if (defaultMapsOnlyRadioButton.IsChecked == true)
            {
                return RefreshPbScope.DefaultMapsOnly;
            }

            if (selectedFoldersRadioButton.IsChecked == true)
            {
                return RefreshPbScope.SelectedFolders;
            }

            return RefreshPbScope.AllFolders;
        }

        private List<string> GetCheckedFolderNames()
        {
            return _folderNamesByCheckBox
                .Where(pair => pair.Key.IsChecked == true)
                .Select(pair => pair.Value)
                .ToList();
        }

        private void SetAllCheckBoxes(bool isChecked)
        {
            foreach (CheckBox checkBox in _folderNamesByCheckBox.Keys)
            {
                checkBox.IsChecked = isChecked;
            }
        }

        private void SetFolderCheckBoxesEnabled(bool isEnabled)
        {
            foreach (CheckBox checkBox in _folderNamesByCheckBox.Keys)
            {
                checkBox.IsEnabled = isEnabled;
            }
        }

        private void SetCheckBoxByFolderName(string folderName, bool isChecked)
        {
            foreach (var pair in _folderNamesByCheckBox)
            {
                if (string.Equals(pair.Value, folderName, System.StringComparison.OrdinalIgnoreCase))
                {
                    pair.Key.IsChecked = isChecked;
                    return;
                }
            }
        }

        private void LoadFolderCheckBoxes(string defaultMapFolder)
        {
            AddFolderCheckBox(RefreshPbFolderNames.DefaultMaps);

            if (!Directory.Exists(defaultMapFolder))
            {
                return;
            }

            string[] folderNames = Directory
                .GetDirectories(defaultMapFolder, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(folderName => !string.IsNullOrWhiteSpace(folderName))
                .OrderBy(folderName => folderName)
                .ToArray();

            foreach (string folderName in folderNames)
            {
                AddFolderCheckBox(folderName);
            }
        }

        private void AddFolderCheckBox(string folderName)
        {
            CheckBox checkBox = new()
            {
                Content = folderName,
                Margin = new Thickness(0, 4, 0, 4),
                FontSize = 15
            };

            checkBox.SetResourceReference(Control.ForegroundProperty, "MahApps.Brushes.Text");

            folderCheckBoxesPanel.Children.Add(checkBox);
            _folderNamesByCheckBox[checkBox] = folderName;
        }
    }

    public static class RefreshPbFolderNames
    {
        public const string DefaultMaps = "Default maps";
    }
}
