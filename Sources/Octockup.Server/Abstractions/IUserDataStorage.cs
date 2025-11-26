using Octockup.Server.Models;

namespace Octockup.Server.Abstractions
{
    public interface IUserDataStorage
    {
        void AddBackupSource(UserBackupSource newSource);
        bool ChangePassword(string username, string oldPassword, string newPassword);
        UserData? FindUserData(string username);
        UserData GetUserData(string username);
        bool ValidateUserCredentials(string username, string password);
    }
}
