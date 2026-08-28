using System.Threading.Tasks;

namespace HawassaUnifiedCampusEventManagementSystem.Services
{
    /// <summary>
    /// Service contract for recording secure audit log trails across the platform.
    /// </summary>
    public interface IAuditLogService
    {
        Task LogAsync(
            ulong? userId,
            string action,
            string? entityType = null,
            ulong? entityId = null,
            string? description = null,
            string? ipAddress = null,
            string? userAgent = null);
    }
}
