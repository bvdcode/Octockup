using Octockup.Server.Models;

namespace Octockup.Server.Abstractions
{
    public interface IUserDataStorage
    {
        void AddSavedSource(SavedBackupModule newSource);
        void AddSavedStorage(SavedBackupModule newStorage);
        bool ChangePassword(string username, string oldPassword, string newPassword);
        UserData? FindUserData(string username);
        UserData GetUserData(string username);
        bool ValidateUserCredentials(string username, string password);
    }
}
