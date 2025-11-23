using Octockup.Server.Models;
using Octockup.Server.Abstractions;
using EasyExtensions.Crypto.Abstractions;

namespace Octockup.Server.Services
{
    public class UserDataStorage(IStreamCipher _crypto) : IUserDataStorage
    {
        public bool ChangePassword(ChangePasswordRequest request)
        {
            throw new NotImplementedException();
        }

        public bool ValidateUserCredentials(string username, string password)
        {
            throw new NotImplementedException();
        }
    }
}
