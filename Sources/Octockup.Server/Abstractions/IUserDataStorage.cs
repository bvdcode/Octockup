using Octockup.Server.Database;

namespace Octockup.Server.Abstractions
{
    public interface IUserDataStorage
    {
        void AddSavedSource(SavedBackupModule newSource);
        void AddSavedStorage(SavedBackupModule newStorage);
        bool ChangePassword(string username, string oldPassword, string newPassword);
        UserData? FindUserData(string username);
        UserData GetUserData(string username);
        void RemoveSavedSource(SavedBackupModule foundSource);
        void RemoveSavedStorage(SavedBackupModule foundStorage);
        bool ValidateUserCredentials(string username, string password);
    }
}
