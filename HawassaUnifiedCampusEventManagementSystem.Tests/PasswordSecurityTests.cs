using System;
using HawassaUnifiedCampusEventManagementSystem.Controllers;
using HawassaUnifiedCampusEventManagementSystem.Models;
using Xunit;

namespace HawassaUnifiedCampusEventManagementSystem.Tests
{
    public class PasswordSecurityTests
    {
        [Fact]
        public void HashPassword_ShouldReturnValidNonNullHash()
        {
            // Arrange
            string rawPassword = "HawassaUniversitySecurePass@2026";

            // Act
            string hash = AccountController.HashPassword(rawPassword);

            // Assert
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
            Assert.NotEqual(rawPassword, hash);
        }

        [Fact]
        public void HashPassword_ShouldProduceUniqueSaltedHashesForSamePassword()
        {
            // Arrange
            string rawPassword = "CampusEventPass2026!";

            // Act
            string hash1 = AccountController.HashPassword(rawPassword);
            string hash2 = AccountController.HashPassword(rawPassword);

            // Assert
            Assert.NotEqual(hash1, hash2); // Salts must differ
        }

        [Theory]
        [InlineData("Admin@123456")]
        [InlineData("StudentP@ssw0rd!")]
        [InlineData("SuperAdmin#Secure99")]
        public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue(string password)
        {
            // Arrange
            var user = new User { username = "testuser", email = "test@hu.edu.et" };
            string storedHash = AccountController.HashPassword(password);

            // Act
            bool isValid = AccountController.VerifyPassword(user, password, storedHash);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("WrongPassword123")]
        [InlineData("admin123")]
        [InlineData(" ")]
        public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse(string wrongPassword)
        {
            // Arrange
            var user = new User { username = "testuser", email = "test@hu.edu.et" };
            string storedHash = AccountController.HashPassword("CorrectPassword@2026");

            // Act
            bool isValid = AccountController.VerifyPassword(user, wrongPassword, storedHash);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void VerifyPassword_WithEmptyOrNullInputs_ShouldReturnFalse()
        {
            // Arrange
            var user = new User();

            // Act & Assert
            Assert.False(AccountController.VerifyPassword(user, "", "somehash"));
            Assert.False(AccountController.VerifyPassword(user, "password", ""));
            Assert.False(AccountController.VerifyPassword(user, null!, "somehash"));
            Assert.False(AccountController.VerifyPassword(user, "password", null!));
        }
    }
}
