using System.Security.Claims;

namespace HawassaUnifiedCampusEventManagementSystem.Services
{
    public static class RoleClaims
    {
        public static bool IsSuperAdmin(ClaimsPrincipal user)
        {
            return user.IsInRole("SuperAdmin") || user.IsInRole("SUPERADMIN");
        }

        public static bool IsAdmin(ClaimsPrincipal user)
        {
            return user.IsInRole("Admin") || user.IsInRole("ADMIN") || IsSuperAdmin(user);
        }
    }
}
