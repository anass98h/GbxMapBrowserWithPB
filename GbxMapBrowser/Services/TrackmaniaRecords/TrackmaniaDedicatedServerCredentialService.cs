#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GbxMapBrowser.Services.TrackmaniaRecords
{
    public sealed class TrackmaniaDedicatedServerCredentialService
    {
        public sealed class DedicatedServerCredentials
        {
            public string Login { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public string GetStoragePath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return Path.Combine(
                localAppData,
                "GbxMapBrowser",
                "trackmania-dedicated-server-credentials.dat"
            );
        }

        public bool HasSavedCredentials()
        {
            return File.Exists(GetStoragePath());
        }

        public DedicatedServerCredentials? Load()
        {
            string path = GetStoragePath();

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(path);

                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser
                );

                string json = Encoding.UTF8.GetString(decryptedBytes);

                return JsonSerializer.Deserialize<DedicatedServerCredentials>(json);
            }
            catch
            {
                return null;
            }
        }

        public void Save(string login, string password)
        {
            string cleanLogin = login.Trim();
            string cleanPassword = password.Trim();

            if (string.IsNullOrWhiteSpace(cleanLogin))
            {
                throw new InvalidOperationException("Dedicated server login cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(cleanPassword))
            {
                throw new InvalidOperationException("Dedicated server password cannot be empty.");
            }

            DedicatedServerCredentials credentials = new()
            {
                Login = cleanLogin,
                Password = cleanPassword
            };

            string json = JsonSerializer.Serialize(credentials);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);

            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser
            );

            string path = GetStoragePath();
            string directory = Path.GetDirectoryName(path) ?? "";

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, encryptedBytes);
        }

        public void Delete()
        {
            string path = GetStoragePath();

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}