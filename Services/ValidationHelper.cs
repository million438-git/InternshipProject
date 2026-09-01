using System;
using System.Text.RegularExpressions;

namespace HawassaUnifiedCampusEventManagementSystem.Services
{
    public static class ValidationHelper
    {
        // 1. Strict Name Regex: only letters (a-z, A-Z), spaces, hyphens, and apostrophes
        private static readonly Regex NameRegex = new(
            @"^[a-zA-Z\s\-']{2,50}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 2. Strict Email Regex: standard RFC-compliant email structure with valid TLD (.com, .edu.et, .org, etc.)
        private static readonly Regex EmailRegex = new(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsValidName(string? name, string fieldLabel, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                errorMessage = $"{fieldLabel} is required.";
                return false;
            }

            var trimmed = name.Trim();
            if (trimmed.Length < 2)
            {
                errorMessage = $"{fieldLabel} must be at least 2 characters long.";
                return false;
            }

            if (trimmed.Length > 50)
            {
                errorMessage = $"{fieldLabel} cannot exceed 50 characters.";
                return false;
            }

            if (!NameRegex.IsMatch(trimmed))
            {
                errorMessage = $"{fieldLabel} ('{trimmed}') must contain only alphabetic letters. Numbers (0-9) and special characters are not allowed.";
                return false;
            }

            return true;
        }

        public static bool IsValidEmail(string? email, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(email))
            {
                errorMessage = "Email address is required.";
                return false;
            }

            var trimmed = email.Trim();
            if (!EmailRegex.IsMatch(trimmed))
            {
                errorMessage = "Please enter a valid official email address with a valid domain (e.g. name@hawassa.edu.et or name@gmail.com).";
                return false;
            }

            return true;
        }

        public static bool IsStrongPassword(string? password, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Password is required.";
                return false;
            }

            if (password.Length < 8)
            {
                errorMessage = "Password must be at least 8 characters long.";
                return false;
            }

            bool hasUpper = false;
            bool hasLower = false;
            bool hasDigit = false;
            bool hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else hasSpecial = true;
            }

            if (!hasUpper)
            {
                errorMessage = "Password must contain at least one uppercase letter (A-Z).";
                return false;
            }

            if (!hasLower)
            {
                errorMessage = "Password must contain at least one lowercase letter (a-z).";
                return false;
            }

            if (!hasDigit)
            {
                errorMessage = "Password must contain at least one numeric digit (0-9).";
                return false;
            }

            if (!hasSpecial)
            {
                errorMessage = "Password must contain at least one special character (!@#$%^&*_-+= etc.).";
                return false;
            }

            return true;
        }
    }
}
