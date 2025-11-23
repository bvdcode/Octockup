using System.Text;
using System.Text.Json;
using EasyExtensions.Crypto;
using Octockup.Server.Models;
using EasyExtensions.Extensions;
using Octockup.Server.Abstractions;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using EasyExtensions.Abstractions;

namespace Octockup.Server.Services
{
    public class UserDataStorage(IStreamCipher _crypto, IPasswordHashService _passwords) : IUserDataStorage
    {
        private readonly string _userDataFilePath = Path.Combine(AppContext.BaseDirectory, "data");
        private readonly ConcurrentDictionary<string, UserData> _cache = new();

        public bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (!_cache.TryGetValue(username, out UserData? userData))
            {
                bool isValid = ValidateUserCredentials(username, oldPassword);
                if (!isValid)
                {
                    return false;
                }
            }
            SaveUserData(userData, newPassword);
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
                return true;
            }
            byte[] content = File.ReadAllBytes(path);
            string json = _crypto.Decrypt(content);
            UserData userData = JsonSerializer.Deserialize<UserData>(json)
                ?? throw new Exception("Deserialized user data is null");
            _cache[username] = userData;
        }
    }
}
