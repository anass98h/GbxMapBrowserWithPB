using System.Windows;
using System.Windows.Input;
using GbxMapBrowser.Services.Hotkeys;

namespace GbxMapBrowser
{
    public partial class MapNavigationHotkeyWindow : Window
    {
        private enum CaptureTarget
        {
            None,
            Forward,
            Backward
        }

        private CaptureTarget _captureTarget = CaptureTarget.None;

        public MapNavigationHotkeySettings Settings { get; }

        public MapNavigationHotkeyWindow(MapNavigationHotkeySettings settings)
        {
            InitializeComponent();

            Settings = settings?.Clone() ?? new MapNavigationHotkeySettings();
            enabledCheckBox.IsChecked = Settings.IsEnabled;
            UpdateHotkeyText();
            captureStatusTextBlock.Text = "Click Set, then press the key combination you want to use.";
        }

        private void SetForwardButton_Click(object sender, RoutedEventArgs e)
        {
            BeginCapture(CaptureTarget.Forward, "forward");
        }

        private void SetBackwardButton_Click(object sender, RoutedEventArgs e)
        {
            BeginCapture(CaptureTarget.Backward, "backward");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_captureTarget != CaptureTarget.None)
            {
                MessageBox.Show(
                    "Finish setting the current hotkey before saving.",
                    "Hotkey capture active",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            Settings.IsEnabled = enabledCheckBox.IsChecked == true;

            if (Settings.IsEnabled && !Settings.HasBothHotkeys)
            {
                MessageBox.Show(
                    "Set both forward and backward hotkeys as key combinations, or disable map navigation hotkeys.",
                    "Missing hotkey",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            if (Settings.IsEnabled && Settings.Forward == Settings.Backward)
            {
                MessageBox.Show(
                    "Forward and backward hotkeys must be different.",
                    "Duplicate hotkey",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_captureTarget == CaptureTarget.None)
            {
                return;
            }

            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                _captureTarget = CaptureTarget.None;
                captureStatusTextBlock.Text = "Hotkey capture cancelled.";
                UpdateCaptureButtonState();
                return;
            }

            Key key = GetActualKey(e);

            if (IsModifierOnly(key))
            {
                return;
            }

            HotkeyGesture hotkey = new(key, Keyboard.Modifiers);

            if (!hotkey.IsValidCombination)
            {
                captureStatusTextBlock.Text = "Hotkeys must use at least one modifier plus one key, for example Ctrl + Alt + N.";
                return;
            }

            if (_captureTarget == CaptureTarget.Forward)
            {
                Settings.Forward = hotkey;
            }
            else
            {
                Settings.Backward = hotkey;
            }

            _captureTarget = CaptureTarget.None;
            UpdateHotkeyText();
            UpdateCaptureButtonState();
            captureStatusTextBlock.Text = "Hotkey set. Save to apply it.";
        }

        private void BeginCapture(CaptureTarget target, string label)
        {
            _captureTarget = target;
            captureStatusTextBlock.Text = $"Press the {label} hotkey now. Press Esc to cancel.";
            UpdateCaptureButtonState();
            Focus();
        }

        private void UpdateHotkeyText()
        {
            forwardHotkeyTextBlock.Text = Settings.Forward.ToString();
            backwardHotkeyTextBlock.Text = Settings.Backward.ToString();
        }

        private void UpdateCaptureButtonState()
        {
            setForwardButton.Content = _captureTarget == CaptureTarget.Forward ? "Listening..." : "Set";
            setBackwardButton.Content = _captureTarget == CaptureTarget.Backward ? "Listening..." : "Set";
            setForwardButton.IsEnabled = _captureTarget is CaptureTarget.None or CaptureTarget.Forward;
            setBackwardButton.IsEnabled = _captureTarget is CaptureTarget.None or CaptureTarget.Backward;
        }

        private static Key GetActualKey(KeyEventArgs e)
        {
            if (e.Key == Key.System)
            {
                return e.SystemKey;
            }

            if (e.Key == Key.ImeProcessed)
            {
                return e.ImeProcessedKey;
            }

            return e.Key;
        }

        private static bool IsModifierOnly(Key key)
        {
            return key is Key.LeftCtrl or Key.RightCtrl or
                Key.LeftAlt or Key.RightAlt or
                Key.LeftShift or Key.RightShift or
                Key.LWin or Key.RWin or
                Key.None;
        }
    }
}
