using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using GbxMapBrowser.Services.TrackmaniaRecords;

namespace GbxMapBrowser.Windows
{
    public partial class TrackmaniaAccountIdWindow : Window
    {
        private readonly TrackmaniaAccountIdService _accountIdService = new();
        private readonly TrackmaniaDedicatedServerCredentialService _credentialService = new();

        public TrackmaniaAccountIdWindow()
        {
            InitializeComponent();

            LoadSavedAccountId();
            LoadSavedDedicatedServerCredentials();
            UpdateOpenplanetStatus();
        }

        private void LoadSavedAccountId()
        {
            string savedAccountId = _accountIdService.Load();

            if (!string.IsNullOrWhiteSpace(savedAccountId))
            {
                accountIdTextBox.Text = savedAccountId;
                statusTextBlock.Text = "Loaded saved Account ID.";
            }
        }

        private void LoadSavedDedicatedServerCredentials()
        {
            TrackmaniaDedicatedServerCredentialService.DedicatedServerCredentials? credentials = _credentialService.Load();

            if (credentials == null)
            {
                return;
            }

            dedicatedServerLoginTextBox.Text = credentials.Login;
            dedicatedServerPasswordBox.Password = credentials.Password;

            statusTextBlock.Text = "Loaded saved setup information.";
        }

        private void UpdateOpenplanetStatus()
        {
            if (_accountIdService.CanUseOpenplanetAutoDetection())
            {
                findAutomaticallyButton.IsEnabled = true;
                openplanetDownloadTextBlock.Visibility = Visibility.Collapsed;

                openplanetHelpTextBlock.Text =
                    "Openplanet logs were detected. Click the automatic button to search the Openplanet logs.";
            }
            else
            {
                findAutomaticallyButton.IsEnabled = false;
                openplanetDownloadTextBlock.Visibility = Visibility.Visible;

                openplanetHelpTextBlock.Text =
                    "If you do not know your Account ID, we can detect it from Openplanet logs. " +
                    "Install Openplanet, launch Trackmania once, close Trackmania, and then reopen this page.";
            }
        }

        private void FindAutomaticallyButton_Click(object sender, RoutedEventArgs e)
        {
            findAutomaticallyButton.IsEnabled = false;
            saveAccountIdButton.IsEnabled = false;
            statusTextBlock.Text = "Searching Openplanet logs...";

            try
            {
                string accountId = _accountIdService.FindAccountIdFromOpenplanetLogs();

                accountIdTextBox.Text = accountId;
                statusTextBlock.Text = "Detected Account ID from Openplanet logs. Click Save Account ID.";
            }
            catch (Exception ex)
            {
                statusTextBlock.Text = "Could not detect Account ID from Openplanet logs.";

                MessageBox.Show(
                    ex.Message,
                    "Account ID detection failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            finally
            {
                saveAccountIdButton.IsEnabled = true;
                UpdateOpenplanetStatus();
            }
        }

        private void SaveAccountIdButton_Click(object sender, RoutedEventArgs e)
        {
            string accountId = accountIdTextBox.Text.Trim();

            if (!_accountIdService.IsValidAccountId(accountId))
            {
                MessageBox.Show(
                    "The Account ID must be a valid GUID, for example:\n\n08242041-438c-4d60-bd98-230335bd678b",
                    "Invalid Account ID",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            _accountIdService.Save(accountId);

            statusTextBlock.Text = "Saved Account ID.";

            MessageBox.Show(
                "Saved Account ID to:\n\n" + _accountIdService.GetStoragePath(),
                "Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void SaveDedicatedServerCredentialsButton_Click(object sender, RoutedEventArgs e)
        {
            string login = dedicatedServerLoginTextBox.Text.Trim();
            string password = dedicatedServerPasswordBox.Password.Trim();

            try
            {
                _credentialService.Save(login, password);

                statusTextBlock.Text = "Saved dedicated server credentials.";

                MessageBox.Show(
                    "Saved dedicated server credentials securely for this Windows user.\n\n" +
                    "These are stored locally and encrypted with Windows Data Protection.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Could not save credentials",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void DeleteDedicatedServerCredentialsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Delete the saved dedicated server credentials?",
                "Delete credentials",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            _credentialService.Delete();

            dedicatedServerLoginTextBox.Text = "";
            dedicatedServerPasswordBox.Password = "";

            statusTextBlock.Text = "Deleted saved dedicated server credentials.";
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });

            e.Handled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}