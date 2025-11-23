using Octockup.Server.Models;

namespace Octockup.Server.Abstractions
{
    public interface IUserDataStorage
    {
        bool ChangePassword(ChangePasswordRequest request);
        bool ValidateUserCredentials(string username, string password);
    }
}
