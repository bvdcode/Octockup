using Mapster;
using System.Text.Json;
using EasyExtensions.Extensions;
using EasyExtensions.Abstractions;
using Octockup.Server.Abstractions;
using System.Collections.Concurrent;
using Octockup.Server.Models.Database;

namespace Octockup.Server.Services
{
    public class UserDataStorage(IStreamCipher _crypto, IPasswordHashService _passwords) : IUserDataStorage
    {
        private readonly string _userDataFilePath = Path.Combine(AppContext.BaseDirectory, "userdata");
        private static readonly ConcurrentDictionary<string, UserData> _cache = new();

        public bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            bool isValid = ValidateUserCredentials(username, oldPassword);
            if (!isValid)
            {
                return false;
            }
            UserData? userData = FindUserData(username);
            if (userData != null)
            {
                userData.PasswordPhc = _passwords.Hash(newPassword);
                SaveUserData(userData);
                return true;
            }
            return false;
        }

        public bool ValidateUserCredentials(string username, string password)
        {
            UserData? savedData = FindUserData(username);
            if (savedData == null)
            {
                savedData = new()
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    CreatedAt = DateTime.UtcNow,
                    PasswordPhc = _passwords.Hash(password)
                };
                SaveUserData(savedData);
                return true;
            }
            bool valid = _passwords.Verify(password, savedData.PasswordPhc, out bool needsUpgrade);
            if (valid && needsUpgrade)
            {
                savedData.PasswordPhc = _passwords.Hash(password);
                SaveUserData(savedData);
            }
            return valid;
        }

        private void SaveUserData(UserData? userData)
        {
            ArgumentNullException.ThrowIfNull(userData);
            userData.UpdatedAt = DateTime.UtcNow;
            string json = JsonSerializer.Serialize(userData);
            byte[] encrypted = _crypto.Encrypt(json);
            string path = Path.Combine(_userDataFilePath, $"{userData.Username}.oct.tmp");
            Directory.CreateDirectory(_userDataFilePath);
            File.WriteAllBytes(path, encrypted);
            string finalPath = Path.Combine(_userDataFilePath, $"{userData.Username}.oct");
            File.Move(path, finalPath, true);
            _cache[userData.Username] = userData;
        }

        public void AddSavedSource(SavedBackupModule newSource)
        {
            UserData? userData = FindUserData(newSource.Username)
                ?? throw new Exception("User data not found");
            bool exists = userData.SavedSources.Any(source => source.Tag.Equals(newSource.Tag, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                throw new Exception("Backup source with the same tag already exists");
            }
            newSource.Id = Guid.NewGuid();
            newSource.CreatedAt = DateTime.UtcNow;
            newSource.UpdatedAt = DateTime.UtcNow;
            userData.SavedSources.Add(newSource);
            SaveUserData(userData);
        }

        public void AddSavedStorage(SavedBackupModule newSource)
        {
            UserData? userData = FindUserData(newSource.Username)
                ?? throw new Exception("User data not found");
            bool exists = userData.SavedStorages.Any(source => source.Tag.Equals(newSource.Tag, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                throw new Exception("Backup source with the same tag already exists");
            }
            newSource.Id = Guid.NewGuid();
            newSource.CreatedAt = DateTime.UtcNow;
            newSource.UpdatedAt = DateTime.UtcNow;
            userData.SavedStorages.Add(newSource);
            SaveUserData(userData);
        }

        public UserData GetUserData(string username)
        {
            return FindUserData(username) ?? throw new Exception("User data not found");
        }

        public UserData? FindUserData(string username)
        {
            if (_cache.TryGetValue(username, out UserData? userData))
            {
                return userData.Adapt<UserData>();
            }
            string path = Path.Combine(_userDataFilePath, $"{username}.oct");
            if (!File.Exists(path))
            {
                return null;
            }
            byte[] content = File.ReadAllBytes(path);
            string json = _crypto.Decrypt(content);
            return JsonSerializer.Deserialize<UserData>(json)
                ?? throw new Exception("Deserialized user data is null");
        }

        public void RemoveSavedSource(SavedBackupModule foundSource)
        {
            var userData = GetUserData(foundSource.Username);
            var source = userData.SavedSources.First(x => x.Id == foundSource.Id);
            userData.SavedSources.Remove(source);
            SaveUserData(userData);
        }

        public void RemoveSavedStorage(SavedBackupModule foundStorage)
        {
            var userData = GetUserData(foundStorage.Username);
            var storage = userData.SavedStorages.First(x => x.Id == foundStorage.Id);
            userData.SavedStorages.Remove(storage);
            SaveUserData(userData);
        }
    }
}
