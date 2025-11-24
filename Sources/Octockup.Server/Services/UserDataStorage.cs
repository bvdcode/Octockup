using System.Text.Json;
using Octockup.Server.Models;
using EasyExtensions.Extensions;
using EasyExtensions.Abstractions;
using Octockup.Server.Abstractions;
using System.Collections.Concurrent;

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
            if (_cache.TryGetValue(username, out UserData? userData))
            {
                userData.PasswordPhc = _passwords.Hash(newPassword);
                SaveUserData(userData);
                return true;
            }
            return false;
        }

        public bool ValidateUserCredentials(string username, string password)
        {
            string path = Path.Combine(_userDataFilePath, $"{username}.oct");
            if (!File.Exists(path))
            {
                UserData newUserData = new()
                {
                    Username = username,
                    PasswordPhc = _passwords.Hash(password)
                };
                SaveUserData(newUserData);
                _cache[username] = newUserData;
                return true;
            }
            byte[] content = File.ReadAllBytes(path);
            string json = _crypto.Decrypt(content);
            UserData userData = JsonSerializer.Deserialize<UserData>(json)
                ?? throw new Exception("Deserialized user data is null");
            _cache[username] = userData;
            bool valid = _passwords.Verify(password, userData.PasswordPhc, out bool needsUpgrade);
            if (valid && needsUpgrade)
            {
                userData.PasswordPhc = _passwords.Hash(password);
                SaveUserData(userData);
            }
            return valid;
        }

        private void SaveUserData(UserData? userData)
        {
            ArgumentNullException.ThrowIfNull(userData);
            string json = JsonSerializer.Serialize(userData);
            byte[] encrypted = _crypto.Encrypt(json);
            string path = Path.Combine(_userDataFilePath, $"{userData.Username}.oct");
            Directory.CreateDirectory(_userDataFilePath);
            File.WriteAllBytes(path, encrypted);
        }
    }
}
