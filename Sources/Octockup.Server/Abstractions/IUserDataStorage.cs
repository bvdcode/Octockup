using Octockup.Server.Database;

namespace Octockup.Server.Abstractions
{
    public interface IUserDataStorage
    {
        void AddSavedSource(SavedBackupModule newSource);
        void AddSavedStorage(SavedBackupModule newStorage);
        bool ChangePassword(string username, string oldPassword, string newPassword);
        User? FindUserData(string username);
        User GetUser(string username);
        void RemoveSavedSource(SavedBackupModule foundSource);
        void RemoveSavedStorage(SavedBackupModule foundStorage);
        bool ValidateUserCredentials(string username, string password);
    }
}
