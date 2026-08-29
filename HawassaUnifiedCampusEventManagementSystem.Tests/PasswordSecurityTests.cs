using HawassaUnifiedCampusEventManagementSystem.Models;
using HawassaUnifiedCampusEventManagementSystem.Services;
using Xunit;

namespace HawassaUnifiedCampusEventManagementSystem.Tests
{
    public class PasswordSecurityTests
    {
        private readonly PasswordService _passwords = new();

        [Fact]
        public void HashPassword_ShouldReturnValidNonNullHash()
        {
            string rawPassword = "HawassaUniversitySecurePass@2026";

            string hash = _passwords.HashPassword(rawPassword);

            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
            Assert.NotEqual(rawPassword, hash);
        }

        [Fact]
        public void HashPassword_ShouldProduceUniqueSaltedHashesForSamePassword()
        {
            string rawPassword = "CampusEventPass2026!";

            string hash1 = _passwords.HashPassword(rawPassword);
            string hash2 = _passwords.HashPassword(rawPassword);

            Assert.NotEqual(hash1, hash2);
        }

        [Theory]
        [InlineData("Admin@123456")]
        [InlineData("StudentP@ssw0rd!")]
        [InlineData("SuperAdmin#Secure99")]
        public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue(string password)
        {
            var user = new User { username = "testuser", email = "test@hu.edu.et" };
            string storedHash = _passwords.HashPassword(password);

            bool isValid = _passwords.VerifyPassword(user, password, storedHash);

            Assert.True(isValid);
        }

        [Theory]
        [InlineData("WrongPassword123")]
        [InlineData("admin123")]
        [InlineData(" ")]
        public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse(string wrongPassword)
        {
            var user = new User { username = "testuser", email = "test@hu.edu.et" };
            string storedHash = _passwords.HashPassword("CorrectPassword@2026");

            bool isValid = _passwords.VerifyPassword(user, wrongPassword, storedHash);

            Assert.False(isValid);
        }

        [Fact]
        public void VerifyPassword_WithEmptyOrNullInputs_ShouldReturnFalse()
        {
            var user = new User();

            Assert.False(_passwords.VerifyPassword(user, "", "somehash"));
            Assert.False(_passwords.VerifyPassword(user, "password", ""));
            Assert.False(_passwords.VerifyPassword(user, null!, "somehash"));
            Assert.False(_passwords.VerifyPassword(user, "password", null!));
        }

        [Theory]
        [InlineData("123456")]
        [InlineData("Admin@2026")]
        [InlineData("Admin@2026!")]
        [InlineData("SuperAdmin@2026!")]
        public void VerifyPassword_NamedSeedBackdoorPasswords_ShouldReturnFalse(string backdoorPassword)
        {
            var user = new User { username = "superadmin", email = "superadmin@hawassa.edu.et" };
            const string legacySeedHash = "b4a0980c619b02a24c96be11311b70c9c7f66e04d4dd266ec56cb04f9dfc0aa1";

            bool isValid = _passwords.VerifyPassword(user, backdoorPassword, legacySeedHash);

            Assert.False(isValid);
        }

        [Fact]
        public void VerifyPassword_LegacySaltedSha256_ShouldReturnTrue()
        {
            var user = new User { username = "legacy", email = "legacy@hu.edu.et" };
            const string password = "LegacyCampusPass@2026";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var saltedBytes = System.Text.Encoding.UTF8.GetBytes(password + "HUCEMS_SALT_2026");
            var storedHash = Convert.ToHexString(sha256.ComputeHash(saltedBytes)).ToLowerInvariant();

            Assert.True(_passwords.VerifyPassword(user, password, storedHash));
            Assert.True(_passwords.IsLegacyHash(storedHash));
        }
    }
}
