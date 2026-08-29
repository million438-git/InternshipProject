using HawassaUnifiedCampusEventManagementSystem.Models;

namespace HawassaUnifiedCampusEventManagementSystem.Services
{
    public interface IPasswordService
    {
        string HashPassword(string password);
        bool VerifyPassword(User dbUser, string inputPassword, string storedHash);
        bool IsLegacyHash(string storedHash);
    }
}
