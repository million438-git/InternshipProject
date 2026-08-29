using System.Security.Claims;
using HawassaUnifiedCampusEventManagementSystem.Services;
using Xunit;

namespace HawassaUnifiedCampusEventManagementSystem.Tests
{
    public class RoleClaimsTests
    {
        private static ClaimsPrincipal PrincipalWithRole(string role)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Role, role) },
                authenticationType: "Test");
            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public void IsSuperAdmin_WithSuperAdminRole_ShouldReturnTrue()
        {
            Assert.True(RoleClaims.IsSuperAdmin(PrincipalWithRole("SuperAdmin")));
            Assert.True(RoleClaims.IsSuperAdmin(PrincipalWithRole("SUPERADMIN")));
        }

        [Fact]
        public void IsSuperAdmin_WithAdminOrStudent_ShouldReturnFalse()
        {
            Assert.False(RoleClaims.IsSuperAdmin(PrincipalWithRole("Admin")));
            Assert.False(RoleClaims.IsSuperAdmin(PrincipalWithRole("Student")));
            Assert.False(RoleClaims.IsSuperAdmin(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        [Fact]
        public void IsAdmin_WithAdminOrSuperAdmin_ShouldReturnTrue()
        {
            Assert.True(RoleClaims.IsAdmin(PrincipalWithRole("Admin")));
            Assert.True(RoleClaims.IsAdmin(PrincipalWithRole("ADMIN")));
            Assert.True(RoleClaims.IsAdmin(PrincipalWithRole("SuperAdmin")));
        }

        [Fact]
        public void IsAdmin_WithStudent_ShouldReturnFalse()
        {
            Assert.False(RoleClaims.IsAdmin(PrincipalWithRole("Student")));
            Assert.False(RoleClaims.IsAdmin(new ClaimsPrincipal(new ClaimsIdentity())));
        }
    }
}
