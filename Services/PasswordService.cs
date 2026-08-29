using System.Security.Cryptography;
using System.Text;
using HawassaUnifiedCampusEventManagementSystem.Models;
using Microsoft.AspNetCore.Identity;

namespace HawassaUnifiedCampusEventManagementSystem.Services
{
    public class PasswordService : IPasswordService
    {
        private static readonly PasswordHasher<User> Hasher = new();

        public string HashPassword(string password)
        {
            var dummyUser = new User();
            return Hasher.HashPassword(dummyUser, password);
        }

        public bool VerifyPassword(User dbUser, string inputPassword, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(inputPassword))
                return false;

            try
            {
                var result = Hasher.VerifyHashedPassword(dbUser, storedHash, inputPassword);
                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    return true;
                }
            }
            catch
            {
                // Fall through to legacy hash-format checks
            }

            using var sha256 = SHA256.Create();
            var saltedBytes = Encoding.UTF8.GetBytes(inputPassword + "HUCEMS_SALT_2026");
            var computedSaltedHash = Convert.ToHexString(sha256.ComputeHash(saltedBytes)).ToLowerInvariant();
            if (string.Equals(computedSaltedHash, storedHash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var rawBytes = Encoding.UTF8.GetBytes(inputPassword);
            var computedRawHash = Convert.ToHexString(sha256.ComputeHash(rawBytes)).ToLowerInvariant();
            if (string.Equals(computedRawHash, storedHash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public bool IsLegacyHash(string storedHash)
        {
            return string.IsNullOrWhiteSpace(storedHash) || !storedHash.StartsWith("AQAAAA", StringComparison.Ordinal);
        }
    }
}
