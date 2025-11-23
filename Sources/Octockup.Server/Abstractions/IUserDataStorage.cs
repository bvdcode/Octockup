namespace Octockup.Server.Abstractions
{
    public interface IUserDataStorage
    {
        bool ChangePassword(string username, string oldPassword, string newPassword);
        bool ValidateUserCredentials(string username, string password);
    }
}
