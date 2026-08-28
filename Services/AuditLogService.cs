using System;
using System.Threading.Tasks;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HawassaUnifiedCampusEventManagementSystem.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(
            ApplicationDbContext db,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditLogService> logger)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(
            ulong? userId,
            string action,
            string? entityType = null,
            ulong? entityId = null,
            string? description = null,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var resolvedIp = ipAddress 
                    ?? httpContext?.Connection?.RemoteIpAddress?.ToString() 
                    ?? "127.0.0.1";
                var resolvedAgent = userAgent 
                    ?? httpContext?.Request?.Headers["User-Agent"].ToString() 
                    ?? "System";

                var audit = new audit_log
                {
                    user_id = userId,
                    action = action,
                    entity_type = entityType,
                    entity_id = entityId,
                    description = description,
                    ip_address = resolvedIp,
                    user_agent = resolvedAgent,
                    created_at = DateTime.UtcNow
                };

                _db.audit_logs.Add(audit);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist audit log entry for action: {Action}", action);
            }
        }
    }
}
