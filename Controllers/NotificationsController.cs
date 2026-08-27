using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Models;

namespace HawassaUnifiedCampusEventManagementSystem.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(ApplicationDbContext db, ILogger<NotificationsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        private ulong? GetCurrentUserId()
        {
            var claimVal = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           User.FindFirstValue("UserId") ??
                           User.FindFirstValue("sub");

            if (!string.IsNullOrEmpty(claimVal) && ulong.TryParse(claimVal, out ulong uid))
            {
                return uid;
            }

            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                var user = _db.users.AsNoTracking().FirstOrDefault(u => u.username == username || u.email == username);
                if (user != null) return user.id;
            }

            return null;
        }

        private static string FormatTimeAgo(DateTime dt)
        {
            var span = DateTime.UtcNow - dt;
            if (span.TotalSeconds < 60) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 2) return "Yesterday";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return dt.ToString("MMM dd, yyyy");
        }

        // =========================================================================
        // 1. GET: /Notifications or /Notifications/Index
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Index(string? filter = "ALL", string? search = null)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var user = await _db.users
                .Include(u => u.department)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.id == userId.Value);

            var vm = new NotificationCenterViewModel
            {
                UserId = userId.Value,
                UserName = user != null ? $"{user.first_name} {user.last_name}".Trim() : (User.Identity?.Name ?? "Member"),
                UserRole = user?.account_type ?? "Student",
                DepartmentName = user?.department?.name,
                ActiveFilter = string.IsNullOrWhiteSpace(filter) ? "ALL" : filter.ToUpperInvariant(),
                SearchTerm = search
            };

            try
            {
                var baseQuery = _db.notifications
                    .AsNoTracking()
                    .Where(n => n.user_id == userId.Value);

                // Compute KPI metrics across all user notifications
                vm.TotalCount = await baseQuery.CountAsync();
                vm.UnreadCount = await baseQuery.CountAsync(n => !n.is_read);
                vm.AnnouncementAlertsCount = await baseQuery.CountAsync(n => n.notification_type == "ANNOUNCEMENT");
                vm.EventAlertsCount = await baseQuery.CountAsync(n => n.notification_type == "EVENT");
                vm.RegistrationAlertsCount = await baseQuery.CountAsync(n => n.notification_type == "REGISTRATION");
                vm.SystemAlertsCount = await baseQuery.CountAsync(n => n.notification_type == "SYSTEM");
                vm.ClubAlertsCount = await baseQuery.CountAsync(n => n.notification_type == "CLUB");

                var query = baseQuery;

                // Apply Tab Filters
                query = vm.ActiveFilter switch
                {
                    "UNREAD" => query.Where(n => !n.is_read),
                    "ANNOUNCEMENT" => query.Where(n => n.notification_type == "ANNOUNCEMENT"),
                    "EVENT" => query.Where(n => n.notification_type == "EVENT"),
                    "REGISTRATION" => query.Where(n => n.notification_type == "REGISTRATION"),
                    "SYSTEM" => query.Where(n => n.notification_type == "SYSTEM"),
                    "CLUB" => query.Where(n => n.notification_type == "CLUB"),
                    _ => query
                };

                // Apply Search query
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(n => n.title.ToLower().Contains(s) || n.message.ToLower().Contains(s));
                }

                var entities = await query
                    .OrderByDescending(n => n.created_at)
                    .Take(100)
                    .ToListAsync();

                vm.Notifications = entities.Select(n => new NotificationItemDto
                {
                    Id = n.id,
                    Title = n.title,
                    Message = n.message,
                    NotificationType = n.notification_type ?? "SYSTEM",
                    RelatedEntityType = n.related_entity_type,
                    RelatedEntityId = n.related_entity_id,
                    ActionUrl = n.action_url,
                    IsRead = n.is_read,
                    ReadAt = n.read_at,
                    CreatedAt = n.created_at,
                    TimeAgoFormatted = FormatTimeAgo(n.created_at)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load notifications for user {UserId}", userId.Value);
            }

            ViewData["Title"] = "Notification & Alert Center | HUCEMS";
            return View(vm);
        }

        // =========================================================================
        // 2. GET: /Notifications/GetUnreadCount (JSON API for Top-Nav Bell)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Json(new { success = false, unreadCount = 0 });

            try
            {
                var unreadCount = await _db.notifications
                    .AsNoTracking()
                    .CountAsync(n => n.user_id == userId.Value && !n.is_read);

                var totalCount = await _db.notifications
                    .AsNoTracking()
                    .CountAsync(n => n.user_id == userId.Value);

                var recent = await _db.notifications
                    .AsNoTracking()
                    .Where(n => n.user_id == userId.Value)
                    .OrderByDescending(n => n.created_at)
                    .Take(5)
                    .Select(n => new
                    {
                        id = n.id,
                        title = n.title,
                        message = n.message.Length > 85 ? n.message.Substring(0, 82) + "..." : n.message,
                        type = n.notification_type,
                        actionUrl = n.action_url ?? "/Notifications",
                        isRead = n.is_read,
                        timeAgo = FormatTimeAgo(n.created_at)
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    unreadCount,
                    totalCount,
                    recent
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get unread count");
                return Json(new { success = false, unreadCount = 0 });
            }
        }

        // =========================================================================
        // 3. POST: /Notifications/MarkAsRead/{id}
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(ulong id, string? returnUrl = null)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var notif = await _db.notifications.FirstOrDefaultAsync(n => n.id == id && n.user_id == userId.Value);
            if (notif != null && !notif.is_read)
            {
                notif.is_read = true;
                notif.read_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var unreadLeft = await _db.notifications.CountAsync(n => n.user_id == userId.Value && !n.is_read);
                return Json(new { success = true, unreadCount = unreadLeft });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 4. POST: /Notifications/MarkAllAsRead
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead(string? returnUrl = null)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var unreadNotifs = await _db.notifications
                .Where(n => n.user_id == userId.Value && !n.is_read)
                .ToListAsync();

            if (unreadNotifs.Any())
            {
                var now = DateTime.UtcNow;
                foreach (var n in unreadNotifs)
                {
                    n.is_read = true;
                    n.read_at = now;
                }
                await _db.SaveChangesAsync();
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "All notifications marked as read.", unreadCount = 0 });
            }

            TempData["SuccessMessage"] = "All notifications marked as read.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 5. POST: /Notifications/Delete/{id}
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(ulong id, string? returnUrl = null)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var notif = await _db.notifications.FirstOrDefaultAsync(n => n.id == id && n.user_id == userId.Value);
            if (notif != null)
            {
                _db.notifications.Remove(notif);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Notification deleted.";
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true });
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // 6. POST: /Notifications/SendDirect (Admins/SuperAdmins/Authorized Users)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDirect([FromForm] SendDirectNotificationRequest request)
        {
            var senderId = GetCurrentUserId();
            if (!senderId.HasValue) return Unauthorized();

            var isSuperOrAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
                                 User.IsInRole("ADMIN") || User.IsInRole("SUPERADMIN");

            if (!isSuperOrAdmin)
            {
                TempData["ErrorMessage"] = "Unauthorized: Only administrators can dispatch direct user alerts.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
            {
                TempData["ErrorMessage"] = "Title and message are required.";
                return RedirectToAction("Notifications", "Admin");
            }

            ulong? targetUid = request.TargetUserId;
            if (!targetUid.HasValue && !string.IsNullOrWhiteSpace(request.TargetUsername))
            {
                var targetUser = await _db.users
                    .FirstOrDefaultAsync(u => u.username == request.TargetUsername.Trim() || u.email == request.TargetUsername.Trim());
                if (targetUser != null)
                {
                    targetUid = targetUser.id;
                }
            }

            if (!targetUid.HasValue)
            {
                TempData["ErrorMessage"] = "Target recipient user was not found.";
                return RedirectToAction("Notifications", "Admin");
            }

            var notification = new Notification
            {
                user_id = targetUid.Value,
                title = request.Title.Trim(),
                message = request.Message.Trim(),
                notification_type = !string.IsNullOrWhiteSpace(request.NotificationType) ? request.NotificationType.ToUpperInvariant() : "SYSTEM",
                related_entity_type = "DIRECT",
                action_url = request.ActionUrl,
                is_read = false,
                created_at = DateTime.UtcNow
            };

            _db.notifications.Add(notification);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Direct notification successfully delivered to recipient (User ID: {targetUid.Value}).";
            return RedirectToAction("Notifications", "Admin");
        }
    }
}
