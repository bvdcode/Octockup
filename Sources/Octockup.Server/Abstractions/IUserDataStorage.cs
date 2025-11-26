using Octockup.Server.Database;

namespace Octockup.Server.Abstractions
{
    public interface IUserDataStorage
    {
        void AddSavedSource(Module newSource);
        void AddSavedStorage(Module newStorage);
        bool ChangePassword(string username, string oldPassword, string newPassword);
        User? FindUserData(string username);
        User GetUser(string username);
        void RemoveSavedSource(Module foundSource);
        void RemoveSavedStorage(Module foundStorage);
        bool ValidateUserCredentials(string username, string password);
    }
}
