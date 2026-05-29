using System;
using System.IO;
using System.Windows.Input;

namespace GbxMapBrowser.Services.Hotkeys
{
    public sealed class MapNavigationHotkeySettings
    {
        public bool IsEnabled { get; set; }
        public HotkeyGesture Forward { get; set; } = HotkeyGesture.Empty;
        public HotkeyGesture Backward { get; set; } = HotkeyGesture.Empty;

        public bool HasBothHotkeys => Forward.IsValidCombination && Backward.IsValidCombination;

        public MapNavigationHotkeySettings Clone()
        {
            return new MapNavigationHotkeySettings
            {
                IsEnabled = IsEnabled,
                Forward = Forward,
                Backward = Backward
            };
        }

        public static MapNavigationHotkeySettings Load()
        {
            string settingsPath = GetSettingsPath();
            MapNavigationHotkeySettings settings = new();

            try
            {
                if (!File.Exists(settingsPath))
                {
                    return settings;
                }

                foreach (string line in File.ReadAllLines(settingsPath))
                {
                    string[] parts = line.Split('=', 2);

                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    switch (parts[0])
                    {
                        case "Enabled":
                            settings.IsEnabled = bool.TryParse(parts[1], out bool enabled) && enabled;
                            break;
                        case "Forward":
                            settings.Forward = HotkeyGesture.Parse(parts[1]);
                            break;
                        case "Backward":
                            settings.Backward = HotkeyGesture.Parse(parts[1]);
                            break;
                    }
                }
            }
            catch
            {
                return new MapNavigationHotkeySettings();
            }

            return settings;
        }

        public void Save()
        {
            string settingsPath = GetSettingsPath();
            string settingsDirectory = Path.GetDirectoryName(settingsPath);

            if (!string.IsNullOrWhiteSpace(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            File.WriteAllLines(
                settingsPath,
                [
                    $"Enabled={IsEnabled}",
                    $"Forward={Forward.ToStorageString()}",
                    $"Backward={Backward.ToStorageString()}"
                ]
            );
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GbxMapBrowser",
                "map-navigation-hotkeys.txt"
            );
        }
    }

    public readonly struct HotkeyGesture : IEquatable<HotkeyGesture>
    {
        public static HotkeyGesture Empty => new(Key.None, ModifierKeys.None);

        public HotkeyGesture(Key key, ModifierKeys modifiers)
        {
            Key = key;
            Modifiers = modifiers;
        }

        public Key Key { get; }
        public ModifierKeys Modifiers { get; }
        public bool IsEmpty => Key == Key.None;
        public bool IsValidCombination => !IsEmpty && Modifiers != ModifierKeys.None;

        public override string ToString()
        {
            if (IsEmpty)
            {
                return "Not set";
            }

            string text = "";

            if (Modifiers.HasFlag(ModifierKeys.Control))
            {
                text += "Ctrl + ";
            }

            if (Modifiers.HasFlag(ModifierKeys.Alt))
            {
                text += "Alt + ";
            }

            if (Modifiers.HasFlag(ModifierKeys.Shift))
            {
                text += "Shift + ";
            }

            if (Modifiers.HasFlag(ModifierKeys.Windows))
            {
                text += "Win + ";
            }

            return text + Key;
        }

        public string ToStorageString()
        {
            return IsEmpty ? "" : $"{Modifiers}|{Key}";
        }

        public static HotkeyGesture Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Empty;
            }

            string[] parts = value.Split('|', 2);

            if (parts.Length != 2 ||
                !Enum.TryParse(parts[0], out ModifierKeys modifiers) ||
                !Enum.TryParse(parts[1], out Key key))
            {
                return Empty;
            }

            return new HotkeyGesture(key, modifiers);
        }

        public bool Equals(HotkeyGesture other)
        {
            return Key == other.Key && Modifiers == other.Modifiers;
        }

        public override bool Equals(object obj)
        {
            return obj is HotkeyGesture other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, Modifiers);
        }

        public static bool operator ==(HotkeyGesture left, HotkeyGesture right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HotkeyGesture left, HotkeyGesture right)
        {
            return !left.Equals(right);
        }
    }
}
