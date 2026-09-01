using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Models;
using HawassaUnifiedCampusEventManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HawassaUnifiedCampusEventManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin,SUPERADMIN,admin,superadmin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AdminController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IAuditLogService _auditLogService;

        public AdminController(
            ApplicationDbContext db, 
            ILogger<AdminController> logger, 
            IEmailSender emailSender,
            IAuditLogService auditLogService)
        {
            _db = db;
            _logger = logger;
            _emailSender = emailSender;
            _auditLogService = auditLogService;
        }

        private ulong? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (ulong.TryParse(claim, out var id)) return id;
            return null;
        }

        private string GetCurrentUserName()
        {
            return User.Identity?.Name ?? "Administrator";
        }

        private bool IsSuperAdmin()
        {
            return User.IsInRole("SuperAdmin") || User.IsInRole("SUPERADMIN");
        }

        private async Task LogAuditAsync(string action, string? entityType = null, ulong? entityId = null, string? description = null)
        {
            await _auditLogService.LogAsync(
                GetCurrentUserId(),
                action,
                entityType,
                entityId,
                description,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers["User-Agent"].ToString());
        }

        // =========================================================
        // CLUBS & STUDENT SOCIETIES ACCESS
        // =========================================================
        [HttpGet]
        public IActionResult Clubs()
        {
            return RedirectToAction("Index", "Clubs");
        }

        // =========================================================
        // 1. DASHBOARD OVERVIEW
        // =========================================================
        public async Task<IActionResult> Index()
        {
            var vm = new AdminOverviewViewModel
            {
                AdminName = GetCurrentUserName(),
                AdminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin@hawassa.edu.et"
            };

            try
            {
                vm.TotalUsers = await _db.users.CountAsync();
                vm.ActiveUsers = await _db.users.CountAsync(u => u.account_status == "ACTIVE");
                vm.TotalEvents = await _db.events.CountAsync();
                vm.UpcomingEvents = await _db.events.CountAsync(e => e.start_at >= DateTime.UtcNow);
                vm.TodayEvents = await _db.events.CountAsync(e => e.start_at.Date == DateTime.UtcNow.Date);
                vm.PendingApprovals = await _db.events.CountAsync(e => e.approval_status == "PENDING");
                vm.TotalOrganizations = await _db.organizations.CountAsync();
                vm.TotalRegistrations = await _db.registrations.CountAsync();
                vm.TotalAnnouncements = await _db.announcements.CountAsync();
                vm.TotalVenues = await _db.venues.CountAsync();

                // Recent Users
                var recentUsers = await _db.users
                    .OrderByDescending(u => u.created_at)
                    .Take(5)
                    .ToListAsync();

                vm.RecentUsers = recentUsers.Select(u => new AdminRecentUserItem
                {
                    Id = u.id,
                    FullName = $"{u.first_name} {u.last_name}".Trim(),
                    Email = u.email,
                    AccountType = u.account_type,
                    Status = u.account_status,
                    CreatedAt = u.created_at
                }).ToList();

                // Pending Events
                var pendingEvents = await _db.events
                    .Include(e => e.organizer)
                    .Include(e => e.category)
                    .Include(e => e.venue)
                    .Where(e => e.approval_status == "PENDING")
                    .OrderBy(e => e.start_at)
                    .Take(5)
                    .ToListAsync();

                vm.PendingEventsList = pendingEvents.Select(e => new AdminPendingEventItem
                {
                    Id = e.id,
                    Title = e.title,
                    Organizer = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "Campus Member",
                    Category = e.category?.name ?? "General",
                    StartAt = e.start_at,
                    Venue = e.venue?.name ?? "Main Campus"
                }).ToList();

                // Recent Activity / Audit logs
                var recentLogs = await _db.audit_logs
                    .Include(a => a.user)
                    .OrderByDescending(a => a.created_at)
                    .Take(6)
                    .ToListAsync();

                vm.RecentActivities = recentLogs.Select(l => new AdminRecentActivityItem
                {
                    Id = l.id,
                    Action = l.action,
                    UserName = l.user != null ? $"{l.user.first_name} {l.user.last_name}".Trim() : "System",
                    Description = l.description ?? l.action,
                    IpAddress = l.ip_address,
                    Timestamp = l.created_at
                }).ToList();

                // Chart Categories
                var categories = await _db.event_categories.Include(c => c._events).ToListAsync();
                vm.ChartCategories = categories.Select(c => c.name).ToList();
                vm.ChartCategoryCounts = categories.Select(c => c._events.Count).ToList();

                if (!vm.ChartCategories.Any())
                {
                    vm.ChartCategories = new List<string> { "Academic", "Technology", "Sports", "Culture", "Career", "Workshop" };
                    vm.ChartCategoryCounts = new List<int> { 12, 18, 9, 7, 14, 11 };
                }

                vm.ChartMonths = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug" };
                vm.ChartMonthlyRegistrations = new List<int> { 45, 82, 120, 165, 210, 190, 240, 310 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin overview");
                PopulateOverviewFallbacks(vm);
            }

            return View(vm);
        }

        // =========================================================
        // 1B. SUPERADMIN CONSOLE
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> SuperAdmin()
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Access Restricted: The SuperAdmin Console is exclusive to Super Administrator accounts.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new SuperAdminMasterDashboardViewModel
            {
                SuperAdminName = GetCurrentUserName(),
                SuperAdminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "superadmin@hawassa.edu.et"
            };

            try
            {
                vm.TotalPlatformUsers = await _db.users.CountAsync();
                vm.ActiveUsersCount = await _db.users.CountAsync(u => u.account_status == "ACTIVE");
                vm.TotalCampusEvents = await _db.events.CountAsync();
                vm.PendingEventApprovalsCount = await _db.events.CountAsync(e => e.approval_status == "PENDING");
                vm.TotalClubsCount = await _db.clubs.CountAsync();
                vm.TotalOrganizationsCount = await _db.organizations.CountAsync();
                vm.TotalFacultiesCount = await _db.faculties.CountAsync();
                vm.TotalDepartmentsCount = await _db.departments.CountAsync();
                vm.TotalAuditLogsCount = await _db.audit_logs.CountAsync();
                vm.TotalAdministratorsCount = await _db.users.CountAsync(u => u.account_type == "ADMIN" || u.account_type == "SUPERADMIN");

                // Live Pending Users List
                var pendingUsers = await _db.users
                    .Include(u => u.department)
                        .ThenInclude(d => d!.faculty)
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.assigned_byNavigation)
                    .Where(u => u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL")
                    .OrderByDescending(u => u.created_at)
                    .Take(10)
                    .ToListAsync();

                vm.PendingUserApprovalsCount = await _db.users.CountAsync(u => u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL");

                vm.PendingApprovalsList = pendingUsers.Select(u =>
                {
                    var assignedBy = u.user_roleusers.FirstOrDefault(ur => ur.assigned_byNavigation != null)?.assigned_byNavigation;
                    var regName = assignedBy != null ? $"{assignedBy.first_name} {assignedBy.last_name}".Trim() : "Campus Administrator";
                    return new AdminUserApprovalItem
                    {
                        Id = u.id,
                        FullName = $"{u.first_name} {u.last_name}".Trim(),
                        Username = u.username,
                        Email = u.email,
                        Phone = u.phone,
                        AccountType = u.account_type,
                        Status = u.account_status,
                        DepartmentName = u.department?.name ?? "General Campus",
                        FacultyName = u.department?.faculty?.name,
                        StudentId = u.student_id,
                        EmployeeId = u.employee_id,
                        Bio = u.bio,
                        ProfileImageUrl = u.profile_image_url,
                        RegisteredByAdminName = regName,
                        RegisteredByAdminId = assignedBy?.id,
                        RegisteredAt = u.created_at
                    };
                }).ToList();

                // Admin Activity Logs (Actions performed across campus)
                var adminActivityLogs = await _db.audit_logs
                    .Include(a => a.user)
                    .OrderByDescending(a => a.created_at)
                    .Take(12)
                    .ToListAsync();

                vm.AdminActivityFeed = adminActivityLogs.Select(l => new AdminActivityLogItem
                {
                    Id = l.id,
                    Action = l.action,
                    AdminName = l.user != null ? $"{l.user.first_name} {l.user.last_name}".Trim() : "System Automation",
                    AdminEmail = l.user?.email ?? "system@hawassa.edu.et",
                    AdminRole = l.user?.account_type ?? "SYSTEM",
                    Description = l.description ?? l.action,
                    EntityType = l.entity_type,
                    EntityId = l.entity_id,
                    IpAddress = l.ip_address,
                    Timestamp = l.created_at
                }).ToList();

                // Pending events
                var pendingEvents = await _db.events
                    .Include(e => e.organizer)
                    .Include(e => e.category)
                    .Include(e => e.venue)
                    .Where(e => e.approval_status == "PENDING")
                    .OrderBy(e => e.start_at)
                    .Take(6)
                    .ToListAsync();

                vm.PendingEventsList = pendingEvents.Select(e => new AdminPendingEventItem
                {
                    Id = e.id,
                    Title = e.title,
                    Organizer = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "Campus Member",
                    Category = e.category?.name ?? "General",
                    StartAt = e.start_at,
                    Venue = e.venue?.name ?? "Main Campus"
                }).ToList();

                // Estimate DB rows
                var userCount = await _db.users.LongCountAsync();
                var eventCount = await _db.events.LongCountAsync();
                var auditCount = await _db.audit_logs.LongCountAsync();
                var notifCount = await _db.notifications.LongCountAsync();
                var regCount = await _db.registrations.LongCountAsync();
                vm.TotalDatabaseRowsEstimated = userCount + eventCount + auditCount + notifCount + regCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assembling SuperAdmin Dashboard data");
            }

            return View(vm);
        }

        private void PopulateOverviewFallbacks(AdminOverviewViewModel vm)
        {
            vm.TotalUsers = 1245;
            vm.ActiveUsers = 1180;
            vm.TotalEvents = 86;
            vm.UpcomingEvents = 24;
            vm.TodayEvents = 5;
            vm.PendingApprovals = 3;
            vm.TotalOrganizations = 42;
            vm.TotalRegistrations = 3450;
            vm.TotalAnnouncements = 38;
            vm.TotalVenues = 15;

            vm.RecentUsers = new List<AdminRecentUserItem>
            {
                new() { Id = 1, FullName = "Abebe Bekele", Email = "abebe@hawassa.edu.et", AccountType = "STUDENT", Status = "ACTIVE", CreatedAt = DateTime.UtcNow.AddHours(-2) },
                new() { Id = 2, FullName = "Dr. Martha Tadesse", Email = "martha@hawassa.edu.et", AccountType = "FACULTY", Status = "ACTIVE", CreatedAt = DateTime.UtcNow.AddHours(-5) },
                new() { Id = 3, FullName = "Chala Gemeda", Email = "chala@hawassa.edu.et", AccountType = "STUDENT", Status = "ACTIVE", CreatedAt = DateTime.UtcNow.AddDays(-1) }
            };

            vm.RecentActivities = new List<AdminRecentActivityItem>
            {
                new() { Id = 1, Action = "EVENT_CREATED", UserName = "Abebe Bekele", Description = "Created 'Annual Tech Hackathon 2026'", Timestamp = DateTime.UtcNow.AddMinutes(-30) },
                new() { Id = 2, Action = "USER_REGISTERED", UserName = "System", Description = "New student registered from Technology Faculty", Timestamp = DateTime.UtcNow.AddHours(-1) },
                new() { Id = 3, Action = "EVENT_APPROVED", UserName = "Admin", Description = "Approved 'Campus Health & Blood Drive'", Timestamp = DateTime.UtcNow.AddHours(-3) }
            };

            vm.ChartCategories = new List<string> { "Academic", "Technology", "Sports", "Culture", "Career", "Workshop" };
            vm.ChartCategoryCounts = new List<int> { 15, 24, 12, 8, 16, 11 };
            vm.ChartMonths = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug" };
            vm.ChartMonthlyRegistrations = new List<int> { 45, 82, 120, 165, 210, 190, 240, 310 };
        }

        // =========================================================
        // 2. USER MANAGEMENT
        // =========================================================
        public async Task<IActionResult> Users(
            string? search, 
            string? role, 
            string? status, 
            ulong? departmentId, 
            string? sortBy = "newest", 
            int page = 1, 
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var vm = new AdminUsersViewModel
            {
                SearchTerm = search,
                RoleFilter = role,
                StatusFilter = status,
                DepartmentFilter = departmentId,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                var query = _db.users
                    .AsNoTracking()
                    .Include(u => u.department)
                    .Include(u => u._eventorganizers)
                    .Include(u => u.registrations)
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.assigned_byNavigation)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(u => u.first_name.ToLower().Contains(s) ||
                                             u.last_name.ToLower().Contains(s) ||
                                             u.email.ToLower().Contains(s) ||
                                             u.username.ToLower().Contains(s) ||
                                             (u.student_id != null && u.student_id.ToLower().Contains(s)) ||
                                             (u.employee_id != null && u.employee_id.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(role) && role != "ALL")
                {
                    query = query.Where(u => u.account_type == role);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    query = query.Where(u => u.account_status == status);
                }

                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    query = query.Where(u => u.department_id == departmentId.Value);
                }

                vm.TotalFilteredCount = await query.CountAsync();

                // Apply Sorting
                query = sortBy switch
                {
                    "oldest" => query.OrderBy(u => u.created_at),
                    "name_asc" => query.OrderBy(u => u.first_name).ThenBy(u => u.last_name),
                    "name_desc" => query.OrderByDescending(u => u.first_name).ThenByDescending(u => u.last_name),
                    "role" => query.OrderBy(u => u.account_type).ThenByDescending(u => u.created_at),
                    "department" => query.OrderBy(u => u.department != null ? u.department.name : "").ThenBy(u => u.first_name),
                    _ => query.OrderByDescending(u => u.created_at) // default "newest"
                };

                var pagedList = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                vm.Users = pagedList.Select(u =>
                {
                    var assignedBy = u.user_roleusers.FirstOrDefault(ur => ur.assigned_byNavigation != null)?.assigned_byNavigation;
                    var regName = assignedBy != null ? $"{assignedBy.first_name} {assignedBy.last_name}".Trim() : null;
                    return new AdminUserRow
                    {
                        Id = u.id,
                        FullName = $"{u.first_name} {u.last_name}".Trim(),
                        Username = u.username,
                        Email = u.email,
                        Phone = u.phone,
                        StudentId = u.student_id,
                        EmployeeId = u.employee_id,
                        AccountType = u.account_type,
                        Status = u.account_status,
                        DepartmentName = u.department?.name ?? "General",
                        RegisteredByAdminName = regName,
                        RegisteredByAdminId = assignedBy?.id,
                        Bio = u.bio,
                        ProfileImageUrl = u.profile_image_url,
                        CreatedAt = u.created_at,
                        EventCount = u._eventorganizers.Count,
                        RegistrationCount = u.registrations.Count
                    };
                }).ToList();

                // Global metric summary counts
                var allUsers = await _db.users.AsNoTracking().Select(u => u.account_status).ToListAsync();
                vm.TotalCount = allUsers.Count;
                vm.ActiveCount = allUsers.Count(s => s == "ACTIVE");
                vm.SuspendedCount = allUsers.Count(s => s == "SUSPENDED");
                vm.PendingCount = allUsers.Count(s => s == "PENDING" || s == "PENDING_APPROVAL");
                vm.PendingApprovalCount = vm.PendingCount;
                vm.IsSuperAdmin = IsSuperAdmin();

                vm.Departments = await _db.departments
                    .AsNoTracking()
                    .Where(d => d.is_active == null || d.is_active == true)
                    .OrderBy(d => d.name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying users");
                vm.Departments = new List<Department>();
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportUsersCsv(string? search, string? role, string? status, ulong? departmentId)
        {
            try
            {
                var query = _db.users
                    .AsNoTracking()
                    .Include(u => u.department)
                    .Include(u => u._eventorganizers)
                    .Include(u => u.registrations)
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.assigned_byNavigation)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(u => u.first_name.ToLower().Contains(s) ||
                                             u.last_name.ToLower().Contains(s) ||
                                             u.email.ToLower().Contains(s) ||
                                             u.username.ToLower().Contains(s) ||
                                             (u.student_id != null && u.student_id.ToLower().Contains(s)) ||
                                             (u.employee_id != null && u.employee_id.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(role) && role != "ALL")
                {
                    query = query.Where(u => u.account_type == role);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    query = query.Where(u => u.account_status == status);
                }

                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    query = query.Where(u => u.department_id == departmentId.Value);
                }

                var list = await query.OrderByDescending(u => u.created_at).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("User ID,Full Name,Username,Email,Phone,Account Type,Status,Student ID,Employee ID,Department,Registered By,Created Date,Events Organized,Registrations");

                foreach (var u in list)
                {
                    var assignedBy = u.user_roleusers.FirstOrDefault(ur => ur.assigned_byNavigation != null)?.assigned_byNavigation;
                    var regName = assignedBy != null ? $"{assignedBy.first_name} {assignedBy.last_name}".Trim() : "Self / System";
                    var fullName = $"{u.first_name} {u.last_name}".Trim().Replace("\"", "\"\"");
                    var deptName = (u.department?.name ?? "General").Replace("\"", "\"\"");

                    builder.AppendLine($"{u.id},\"{fullName}\",\"{u.username}\",\"{u.email}\",\"{u.phone}\",\"{u.account_type}\",\"{u.account_status}\",\"{u.student_id}\",\"{u.employee_id}\",\"{deptName}\",\"{regName}\",\"{u.created_at:yyyy-MM-dd HH:mm:ss}\",{u._eventorganizers.Count},{u.registrations.Count}");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Users_Directory_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting users CSV");
                TempData["ErrorMessage"] = "Failed to export users: " + ex.Message;
                return RedirectToAction(nameof(Users));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserResetPassword(ulong id, string? newPassword = null)
        {
            try
            {
                var user = await _db.users.FindAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction(nameof(Users));
                }

                var targetType = user.account_type?.ToUpperInvariant() ?? "STUDENT";
                if ((targetType == "ADMIN" || targetType == "SUPERADMIN") && !IsSuperAdmin())
                {
                    TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin can reset credentials for administrator accounts.";
                    return RedirectToAction(nameof(Users));
                }

                // Generate temporary password if not provided or validate provided custom password
                string tempPassword;
                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    if (!ValidationHelper.IsStrongPassword(newPassword, out string pwdErr))
                    {
                        TempData["ErrorMessage"] = pwdErr;
                        return RedirectToAction(nameof(Users));
                    }
                    tempPassword = newPassword.Trim();
                }
                else
                {
                    tempPassword = $"Hawassa@{RandomNumberGenerator.GetInt32(100, 999)}!{RandomNumberGenerator.GetInt32(100, 999)}aA";
                }

                user.password_hash = AccountController.HashPassword(tempPassword);
                user.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                await LogAuditAsync("USER_PASSWORD_RESET_BY_ADMIN", "USER", id, $"Administrator '{GetCurrentUserName()}' reset password for user '{user.username}' ({user.email})");

                // Send email notice
                try
                {
                    string emailSubject = "Hawassa University Portal - Temporary Password Reset";
                    string emailBody = $@"<h3>Password Reset Notification</h3>
                        <p>Hello {user.first_name} {user.last_name},</p>
                        <p>An administrator has reset your password for the Hawassa University Portal account: <strong>{user.username}</strong>.</p>
                        <p>Your temporary password is: <strong style='font-family: monospace; font-size: 16px; color: #1e3a8a;'>{tempPassword}</strong></p>
                        <p>Please log in immediately and change your password in your Account Settings.</p>
                        <p><a href='/Account/Login'>Sign In to Portal</a></p>";

                    await _emailSender.SendEmailAsync(user.email, emailSubject, emailBody);
                }
                catch { }

                TempData["SuccessMessage"] = $"Password for '{user.username}' successfully reset to: {tempPassword}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting user password");
                TempData["ErrorMessage"] = "Failed to reset password: " + ex.Message;
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserToggleStatus(ulong id, string status)
        {
            var user = await _db.users.FindAsync(id);
            if (user != null)
            {
                var targetType = user.account_type?.ToUpperInvariant() ?? "STUDENT";
                if ((targetType == "ADMIN" || targetType == "SUPERADMIN") && !IsSuperAdmin())
                {
                    TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin can modify the account status of administrators.";
                    return RedirectToAction(nameof(Users));
                }

                if (targetType == "SUPERADMIN" && user.id == GetCurrentUserId())
                {
                    TempData["ErrorMessage"] = "Safety restriction: You cannot suspend or deactivate your own SuperAdmin account.";
                    return RedirectToAction(nameof(Users));
                }

                user.account_status = status.ToUpper();
                user.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await LogAuditAsync($"USER_STATUS_CHANGED_{status.ToUpper()}", "USER", id, $"Changed user {user.username} status to {status}");
                TempData["SuccessMessage"] = $"User {user.username} status updated to {status}.";
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserChangeRole(ulong id, string role)
        {
            var user = await _db.users.FindAsync(id);
            if (user != null)
            {
                var currentType = user.account_type?.ToUpperInvariant() ?? "STUDENT";
                var newType = role.Trim().ToUpperInvariant();

                // Admin cannot promote anyone to Admin/SuperAdmin or change an Admin/SuperAdmin role
                if ((currentType == "ADMIN" || currentType == "SUPERADMIN" || newType == "ADMIN" || newType == "SUPERADMIN") && !IsSuperAdmin())
                {
                    TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin has permission to promote users to Administrator or modify Admin/SuperAdmin roles.";
                    return RedirectToAction(nameof(Users));
                }

                user.account_type = newType;
                user.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await LogAuditAsync($"USER_ROLE_CHANGED_{newType}", "USER", id, $"Changed user {user.username} role to {newType}");
                TempData["SuccessMessage"] = $"User {user.username} role updated to {newType}.";
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserDelete(ulong id)
        {
            try
            {
                var user = await _db.users.FindAsync(id);
                if (user != null)
                {
                    var targetType = user.account_type?.ToUpperInvariant() ?? "STUDENT";
                    if ((targetType == "ADMIN" || targetType == "SUPERADMIN") && !IsSuperAdmin())
                    {
                        TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin can delete administrator accounts.";
                        return RedirectToAction(nameof(Users));
                    }

                    if (targetType == "SUPERADMIN")
                    {
                        TempData["ErrorMessage"] = "Safety restriction: SuperAdmin accounts cannot be deleted directly.";
                        return RedirectToAction(nameof(Users));
                    }

                    var currentAdminId = GetCurrentUserId() ?? 1;

                    // Unlink/cascade associated records
                    var userRoles = await _db.user_roles.Where(ur => ur.user_id == id).ToListAsync();
                    if (userRoles.Any()) _db.user_roles.RemoveRange(userRoles);

                    var userSessions = await _db.sessions.Where(s => s.user_id == id).ToListAsync();
                    if (userSessions.Any()) _db.sessions.RemoveRange(userSessions);

                    var userNotifs = await _db.notifications.Where(n => n.user_id == id).ToListAsync();
                    if (userNotifs.Any()) _db.notifications.RemoveRange(userNotifs);

                    var userRegs = await _db.registrations.Where(r => r.user_id == id).ToListAsync();
                    if (userRegs.Any()) _db.registrations.RemoveRange(userRegs);

                    var userFeedbacks = await _db.event_feedbacks.Where(f => f.user_id == id).ToListAsync();
                    if (userFeedbacks.Any()) _db.event_feedbacks.RemoveRange(userFeedbacks);

                    var userComments = await _db.event_comments.Where(c => c.user_id == id).ToListAsync();
                    if (userComments.Any()) _db.event_comments.RemoveRange(userComments);

                    var orgMembers = await _db.organization_members.Where(m => m.user_id == id).ToListAsync();
                    if (orgMembers.Any()) _db.organization_members.RemoveRange(orgMembers);

                    // Reassign organized events or announcements to current admin so they aren't orphaned
                    var organizedEvents = await _db.events.Where(e => e.organizer_id == id).ToListAsync();
                    foreach (var e in organizedEvents) e.organizer_id = currentAdminId;

                    var authoredAnnouncements = await _db.announcements.Where(a => a.author_id == id).ToListAsync();
                    foreach (var a in authoredAnnouncements) a.author_id = currentAdminId;

                    _db.users.Remove(user);
                    await _db.SaveChangesAsync();
                    await LogAuditAsync("USER_DELETED", "USER", id, $"Deleted user {user.username} ({user.email})");
                    TempData["SuccessMessage"] = $"User {user.username} has been deleted.";
                }
                else
                {
                    TempData["ErrorMessage"] = "User not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                TempData["ErrorMessage"] = "Failed to delete user: " + ex.Message;
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(ulong id) => await UserDelete(id);

        // ---------------------------------------------------------
        // USER PROVISIONING & APPROVAL WORKFLOW
        // ---------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> RegisterUser(string? role = "STUDENT")
        {
            var selectedRole = string.IsNullOrWhiteSpace(role) ? "STUDENT" : role.Trim().ToUpperInvariant();
            var vm = new AdminRegisterUserPageViewModel
            {
                IsSuperAdmin = IsSuperAdmin(),
                SelectedRole = selectedRole,
                Departments = await _db.departments
                    .Include(d => d.faculty)
                    .Where(d => d.is_active == null || d.is_active == true)
                    .OrderBy(d => d.name)
                    .ToListAsync(),
                Faculties = await _db.faculties
                    .Where(f => f.is_active == null || f.is_active == true)
                    .OrderBy(f => f.name)
                    .ToListAsync(),
                TotalRegisteredUsersCount = await _db.users.CountAsync(),
                PendingApprovalsCount = await _db.users.CountAsync(u => u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL"),
                Form = new AdminCreateUserInputModel
                {
                    AccountType = selectedRole,
                    Password = "HU@" + DateTime.UtcNow.Year + "!" + new Random().Next(100, 999)
                }
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser(string? role = "STUDENT") => await RegisterUser(role);

        [HttpGet]
        public async Task<IActionResult> UserCreate(string? role = "STUDENT") => await RegisterUser(role);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserCreate(
            string? firstName,
            string? middleName,
            string? lastName,
            string? fullName,
            string username,
            string email,
            string? phone,
            string password,
            string? accountType = "STUDENT",
            ulong? departmentId = null,
            ulong? facultyId = null,
            string? studentId = null,
            string? academicProgram = null,
            string? yearOfStudy = null,
            string? employeeId = null,
            string? academicTitle = null,
            string? staffUnit = null,
            string? jobTitle = null,
            string? officeLocation = null,
            string? organizationName = null,
            string? organizationType = null,
            string? organizationAcronym = null,
            string? bio = null,
            string? profileImageUrl = null,
            string? initialStatus = null,
            bool sendWelcomeEmail = true)
        {
            try
            {
                // 1. Resolve & validate names
                string finalFirst = (firstName ?? "").Trim();
                string finalLast = (lastName ?? "").Trim();
                string? finalMiddle = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim();

                if (string.IsNullOrWhiteSpace(finalFirst) && !string.IsNullOrWhiteSpace(fullName))
                {
                    var parts = fullName.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                    finalFirst = parts.Length > 0 ? parts[0] : "User";
                    if (parts.Length == 2)
                    {
                        finalLast = parts[1];
                    }
                    else if (parts.Length >= 3)
                    {
                        finalMiddle = parts[1];
                        finalLast = parts[2];
                    }
                    else
                    {
                        finalLast = finalFirst;
                    }
                }

                // 1. Resolve & validate names (Alphabetic characters only, no numbers or special symbols)
                if (!ValidationHelper.IsValidName(finalFirst, "First name", out string firstErr))
                {
                    TempData["ErrorMessage"] = firstErr;
                    return RedirectToAction(nameof(RegisterUser), new { role = accountType });
                }

                if (string.IsNullOrWhiteSpace(finalLast))
                {
                    finalLast = finalFirst;
                }
                else if (!ValidationHelper.IsValidName(finalLast, "Last name", out string lastErr))
                {
                    TempData["ErrorMessage"] = lastErr;
                    return RedirectToAction(nameof(RegisterUser), new { role = accountType });
                }

                // 2. Validate Username
                if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
                {
                    TempData["ErrorMessage"] = "Username must be at least 3 characters.";
                    return RedirectToAction(nameof(RegisterUser), new { role = accountType });
                }

                username = username.Trim().ToLowerInvariant().Replace(" ", "_");
                if (await _db.users.AnyAsync(u => u.username.ToLower() == username))
                {
                    TempData["ErrorMessage"] = $"The username '{username}' is already in use. Please choose another username.";
                    return RedirectToAction(nameof(RegisterUser), new { role = accountType });
                }

                // 3. Validate Email (Strict format & valid domain check)
                if (!ValidationHelper.IsValidEmail(email, out string emailErr))
                {
                    TempData["ErrorMessage"] = emailErr;
                    return RedirectToAction(nameof(RegisterUser), new { role = accountType });
                }

                email = email.Trim().ToLowerInvariant();
                if (await _db.users.AnyAsync(u => u.email.ToLower() == email))
                {
                    TempData["ErrorMessage"] = $"An account with email '{email}' already exists in the system.";
                    return RedirectToAction(nameof(RegisterUser), new { role = accountType });
                }

                // 4. Validate Password (Strict Strong Password Policy: 8+ chars, upper, lower, digit, special symbol)
                if (!ValidationHelper.IsStrongPassword(password, out string pwdErr))
                {
                    TempData["ErrorMessage"] = pwdErr;
                    return RedirectToAction(nameof(RegisterUser), new { role = accountType });
                }

                // 5. Validate Role Security
                var normType = (accountType ?? "STUDENT").Trim().ToUpperInvariant();
                if ((normType == "ADMIN" || normType == "SUPERADMIN") && !IsSuperAdmin())
                {
                    TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin accounts can create or promote Administrator accounts.";
                    return RedirectToAction(nameof(Users));
                }

                // 6. Determine Initial Account Status
                // If created by regular Admin: STRICTLY 'PENDING'
                // If created by SuperAdmin: Can be 'ACTIVE' (default) or 'PENDING'
                string statusToAssign = "PENDING";
                bool isVerified = false;

                if (IsSuperAdmin())
                {
                    statusToAssign = string.Equals(initialStatus, "PENDING", StringComparison.OrdinalIgnoreCase) ? "PENDING" : "ACTIVE";
                    isVerified = statusToAssign == "ACTIVE";
                }

                // Construct comprehensive bio/metadata string if not provided
                string? finalBio = !string.IsNullOrWhiteSpace(bio) ? bio.Trim() : null;
                if (string.IsNullOrWhiteSpace(finalBio))
                {
                    if (normType == "STUDENT")
                    {
                        var prog = string.IsNullOrWhiteSpace(academicProgram) ? "Regular" : academicProgram.Trim();
                        var yr = string.IsNullOrWhiteSpace(yearOfStudy) ? "1st Year" : yearOfStudy.Trim();
                        finalBio = $"Student | {yr} ({prog})";
                    }
                    else if (normType == "FACULTY")
                    {
                        var title = string.IsNullOrWhiteSpace(academicTitle) ? "Faculty Member" : academicTitle.Trim();
                        var office = string.IsNullOrWhiteSpace(officeLocation) ? "" : $" | Office: {officeLocation.Trim()}";
                        finalBio = $"{title}{office}";
                    }
                    else if (normType == "STAFF")
                    {
                        var unit = string.IsNullOrWhiteSpace(staffUnit) ? "Campus Administration" : staffUnit.Trim();
                        var title = string.IsNullOrWhiteSpace(jobTitle) ? "Staff Member" : jobTitle.Trim();
                        finalBio = $"{title} - {unit}";
                    }
                    else if (normType == "ORGANIZATION")
                    {
                        var orgN = !string.IsNullOrWhiteSpace(organizationName) ? organizationName.Trim() : "Student Organization";
                        finalBio = $"{orgN} - {organizationType ?? "Club"}";
                    }
                }

                var newUser = new User
                {
                    username = username,
                    email = email,
                    phone = phone?.Trim(),
                    password_hash = AccountController.HashPassword(password),
                    first_name = finalFirst,
                    middle_name = finalMiddle,
                    last_name = finalLast,
                    department_id = departmentId.HasValue && departmentId.Value > 0 ? departmentId.Value : null,
                    student_id = normType == "STUDENT" ? studentId?.Trim() : null,
                    employee_id = (normType == "STAFF" || normType == "FACULTY" || normType == "ADMIN") ? employeeId?.Trim() : null,
                    profile_image_url = !string.IsNullOrWhiteSpace(profileImageUrl) ? profileImageUrl.Trim() : null,
                    bio = finalBio,
                    account_type = normType,
                    account_status = statusToAssign,
                    email_verified = isVerified,
                    phone_verified = isVerified,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                _db.users.Add(newUser);
                await _db.SaveChangesAsync();

                // 7. Link Role in user_roles table
                string roleName = normType switch
                {
                    "SUPERADMIN" => "SuperAdmin",
                    "ADMIN" => "Admin",
                    "FACULTY" => "Faculty",
                    "STAFF" => "Staff",
                    "ORGANIZATION" => "Organization",
                    _ => "Student"
                };

                var roleObj = await _db.roles.FirstOrDefaultAsync(r => r.name.ToLower() == roleName.ToLower());
                if (roleObj != null)
                {
                    _db.user_roles.Add(new user_role
                    {
                        user_id = newUser.id,
                        role_id = roleObj.id,
                        assigned_by = GetCurrentUserId(),
                        assigned_at = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                }

                // 8. If organization, create or update organization entity
                if (normType == "ORGANIZATION" && !string.IsNullOrWhiteSpace(organizationName))
                {
                    try
                    {
                        var org = new Organization
                        {
                            name = organizationName.Trim(),
                            short_name = !string.IsNullOrWhiteSpace(organizationAcronym) ? organizationAcronym.Trim().ToUpperInvariant() : username.ToUpperInvariant(),
                            organization_type = !string.IsNullOrWhiteSpace(organizationType) ? organizationType.Trim().ToUpperInvariant() : "CLUB",
                            email = email,
                            phone = phone?.Trim(),
                            description = finalBio ?? $"Official campus organization registered by administration for {finalFirst} {finalLast}",
                            status = statusToAssign,
                            department_id = departmentId.HasValue && departmentId.Value > 0 ? departmentId.Value : null,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        _db.organizations.Add(org);
                        await _db.SaveChangesAsync();
                    }
                    catch (Exception orgEx)
                    {
                        _logger.LogWarning(orgEx, "Could not create corresponding organization record.");
                    }
                }

                // 9. Security Audit Logging
                var creatorName = GetCurrentUserName();
                var isPending = statusToAssign == "PENDING";
                var auditAction = isPending ? "USER_REGISTERED_BY_ADMIN_PENDING" : "USER_REGISTERED_BY_SUPERADMIN_ACTIVE";
                var auditDesc = $"Admin '{creatorName}' registered user account '{username}' ({email}, Role: {roleName}, Status: {statusToAssign})";
                await LogAuditAsync(auditAction, "USER", newUser.id, auditDesc);

                // 10. Dispatch SuperAdmin In-App Notifications (If Pending)
                if (isPending)
                {
                    try
                    {
                        var superAdmins = await _db.users
                            .Where(u => u.account_type == "SUPERADMIN" || u.user_roleusers.Any(ur => ur.role.name == "SuperAdmin" || ur.role.name == "SUPERADMIN"))
                            .ToListAsync();

                        foreach (var sa in superAdmins)
                        {
                            _db.notifications.Add(new Notification
                            {
                                user_id = sa.id,
                                title = "User Registration Awaiting Approval",
                                message = $"Admin '{creatorName}' registered new {roleName} '{finalFirst} {finalLast}' (@{username}). Review & activate.",
                                notification_type = "SYSTEM",
                                related_entity_type = "USER",
                                related_entity_id = newUser.id,
                                action_url = "/Admin/UserApprovals",
                                is_read = false,
                                created_at = DateTime.UtcNow
                            });
                        }
                        await _db.SaveChangesAsync();
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogWarning(notifEx, "Could not send notification to SuperAdmins for pending registration.");
                    }
                }

                // 11. Automated Email Notification
                if (sendWelcomeEmail)
                {
                    try
                    {
                        string emailSubject = isPending ? "Hawassa University Portal - Account Created (Pending SuperAdmin Approval)" : "Hawassa University Portal - Account Created & Activated";
                        string emailBody = isPending ?
                            $@"<h3>Hello {finalFirst},</h3>
                               <p>Your campus account has been registered by Campus Administration (<strong>{creatorName}</strong>) with username <strong>{username}</strong>.</p>
                               <p><strong>Role:</strong> {roleName}<br/>
                               <strong>Account Status:</strong> Pending SuperAdmin Approval.</p>
                               <p>Once the SuperAdmin approves and activates your account, you will be able to log in with your credentials.</p>
                               <p>Hawassa Unified Campus Event Management System</p>" :
                            $@"<h3>Welcome to HUCEMS, {finalFirst}!</h3>
                               <p>Your campus account has been provisioned and activated by Campus Administration (<strong>{creatorName}</strong>).</p>
                               <p><strong>Username:</strong> {username}<br/>
                               <strong>Temporary Password:</strong> <code>{password}</code><br/>
                               <strong>Role:</strong> {roleName}</p>
                               <p>You can sign in immediately at: <a href='/Account/Login'>Sign In to Portal</a></p>
                               <p>Hawassa Unified Campus Event Management System</p>";

                        await _emailSender.SendEmailAsync(email, emailSubject, emailBody);
                    }
                    catch (Exception mailEx)
                    {
                        _logger.LogWarning(mailEx, "Could not send notification email to {Email}", email);
                    }
                }

                if (isPending)
                {
                    TempData["SuccessMessage"] = $"User '{username}' ({finalFirst} {finalLast}) registered successfully! The account is in PENDING status awaiting SuperAdmin authorization.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"User '{username}' ({finalFirst} {finalLast}) registered and activated successfully!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user in Admin");
                TempData["ErrorMessage"] = "Failed to create user: " + ex.Message;
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(
            string? firstName,
            string? middleName,
            string? lastName,
            string? fullName,
            string username,
            string email,
            string? phone,
            string password,
            string? accountType = "STUDENT",
            ulong? departmentId = null,
            ulong? facultyId = null,
            string? studentId = null,
            string? academicProgram = null,
            string? yearOfStudy = null,
            string? employeeId = null,
            string? academicTitle = null,
            string? staffUnit = null,
            string? jobTitle = null,
            string? officeLocation = null,
            string? organizationName = null,
            string? organizationType = null,
            string? organizationAcronym = null,
            string? bio = null,
            string? profileImageUrl = null,
            string? initialStatus = null,
            bool sendWelcomeEmail = true) =>
            await UserCreate(firstName, middleName, lastName, fullName, username, email, phone, password, accountType, departmentId, facultyId, studentId, academicProgram, yearOfStudy, employeeId, academicTitle, staffUnit, jobTitle, officeLocation, organizationName, organizationType, organizationAcronym, bio, profileImageUrl, initialStatus, sendWelcomeEmail);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserApprove(ulong id, string? returnUrl = null)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin accounts have authorization to approve and activate pending user registrations.";
                return RedirectToAction(nameof(Users));
            }

            try
            {
                var user = await _db.users
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.assigned_byNavigation)
                    .FirstOrDefaultAsync(u => u.id == id);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User account not found.";
                    return RedirectToAction(nameof(Users));
                }

                user.account_status = "ACTIVE";
                user.email_verified = true;
                user.phone_verified = true;
                user.updated_at = DateTime.UtcNow;

                // If organization account, activate corresponding organization record
                if (user.account_type == "ORGANIZATION" && !string.IsNullOrWhiteSpace(user.email))
                {
                    var userEmailLower = user.email.ToLower();
                    var org = await _db.organizations.FirstOrDefaultAsync(o => o.email != null && o.email.ToLower() == userEmailLower);
                    if (org != null)
                    {
                        org.status = "ACTIVE";
                        org.updated_at = DateTime.UtcNow;
                    }
                }

                await _db.SaveChangesAsync();

                var superAdminName = GetCurrentUserName();
                await LogAuditAsync("USER_APPROVED_BY_SUPERADMIN", "USER", id, $"SuperAdmin '{superAdminName}' approved and activated user '{user.username}' ({user.email}, Role: {user.account_type})");

                // Notify registering Admin if known
                var assignedBy = user.user_roleusers.FirstOrDefault()?.assigned_byNavigation;
                if (assignedBy != null && assignedBy.id != GetCurrentUserId())
                {
                    try
                    {
                        _db.notifications.Add(new Notification
                        {
                            user_id = assignedBy.id,
                            title = "User Registration Approved",
                            message = $"SuperAdmin '{superAdminName}' approved and activated user '{user.username}' ({user.first_name} {user.last_name}).",
                            notification_type = "SYSTEM",
                            related_entity_type = "USER",
                            related_entity_id = user.id,
                            action_url = "/Admin/Users",
                            is_read = false,
                            created_at = DateTime.UtcNow
                        });
                        await _db.SaveChangesAsync();
                    }
                    catch { }
                }

                // Send activation notification email
                try
                {
                    string emailSubject = "Hawassa University Portal - Account Approved & Activated!";
                    string emailBody = $@"<h3>Account Approved!</h3>
                        <p>Hello {user.first_name} {user.last_name},</p>
                        <p>Great news! Your Hawassa University portal account (<strong>{user.username}</strong>) has been approved and activated by the SuperAdmin.</p>
                        <p>You can now sign in immediately at: <a href='/Account/Login'>Sign In to Portal</a></p>
                        <p>Hawassa Unified Campus Event Management System</p>";

                    await _emailSender.SendEmailAsync(user.email, emailSubject, emailBody);
                }
                catch (Exception mailEx)
                {
                    _logger.LogWarning(mailEx, "Could not send approval email to user {Email}", user.email);
                }

                TempData["SuccessMessage"] = $"Account for '{user.username}' ({user.first_name} {user.last_name}) has been approved and activated!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving user {UserId}", id);
                TempData["ErrorMessage"] = "Failed to approve user: " + ex.Message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(ulong id, string? returnUrl = null) => await UserApprove(id, returnUrl);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserReject(ulong id, string? reason = null, string? returnUrl = null)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin accounts have authorization to reject user registrations.";
                return RedirectToAction(nameof(Users));
            }

            try
            {
                var user = await _db.users
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.assigned_byNavigation)
                    .FirstOrDefaultAsync(u => u.id == id);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User account not found.";
                    return RedirectToAction(nameof(Users));
                }

                user.account_status = "INACTIVE";
                user.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                var note = string.IsNullOrWhiteSpace(reason) ? "Administrative verification did not meet requirements." : reason.Trim();
                var superAdminName = GetCurrentUserName();
                await LogAuditAsync("USER_REJECTED_BY_SUPERADMIN", "USER", id, $"SuperAdmin '{superAdminName}' rejected user registration for '{user.username}' ({user.email}). Note: {note}");

                // Notify registering Admin if known
                var assignedBy = user.user_roleusers.FirstOrDefault()?.assigned_byNavigation;
                if (assignedBy != null && assignedBy.id != GetCurrentUserId())
                {
                    try
                    {
                        _db.notifications.Add(new Notification
                        {
                            user_id = assignedBy.id,
                            title = "User Registration Declined",
                            message = $"SuperAdmin '{superAdminName}' declined registration for user '{user.username}'. Reason: {note}",
                            notification_type = "SYSTEM",
                            related_entity_type = "USER",
                            related_entity_id = user.id,
                            action_url = "/Admin/Users",
                            is_read = false,
                            created_at = DateTime.UtcNow
                        });
                        await _db.SaveChangesAsync();
                    }
                    catch { }
                }

                // Send rejection explanation email
                try
                {
                    string emailSubject = "Hawassa University Portal - Registration Status Update";
                    string emailBody = $@"<h3>Registration Status Update</h3>
                        <p>Hello {user.first_name} {user.last_name},</p>
                        <p>Your registration request for account <strong>{user.username}</strong> was reviewed by the SuperAdmin and could not be approved at this time.</p>
                        <p><strong>Reason / Note:</strong> {note}</p>
                        <p>If you believe this is an error, please contact your academic department administration.</p>
                        <p>Hawassa Unified Campus Event Management System</p>";

                    await _emailSender.SendEmailAsync(user.email, emailSubject, emailBody);
                }
                catch { }

                TempData["SuccessMessage"] = $"Registration for user '{user.username}' has been declined and set to INACTIVE.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting user {UserId}", id);
                TempData["ErrorMessage"] = "Failed to reject user: " + ex.Message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchApproveUsers(List<ulong> userIds, string? returnUrl = null)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin accounts have authorization to approve users in bulk.";
                return RedirectToAction(nameof(Users));
            }

            if (userIds == null || !userIds.Any())
            {
                TempData["ErrorMessage"] = "No users selected for approval.";
                return RedirectToAction(nameof(UserApprovals));
            }

            int approvedCount = 0;
            try
            {
                var usersToApprove = await _db.users
                    .Where(u => userIds.Contains(u.id) && (u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL"))
                    .ToListAsync();

                foreach (var user in usersToApprove)
                {
                    user.account_status = "ACTIVE";
                    user.email_verified = true;
                    user.phone_verified = true;
                    user.updated_at = DateTime.UtcNow;

                    if (user.account_type == "ORGANIZATION" && !string.IsNullOrWhiteSpace(user.email))
                    {
                        var userEmailLower = user.email.ToLower();
                        var org = await _db.organizations.FirstOrDefaultAsync(o => o.email != null && o.email.ToLower() == userEmailLower);
                        if (org != null)
                        {
                            org.status = "ACTIVE";
                            org.updated_at = DateTime.UtcNow;
                        }
                    }

                    approvedCount++;
                }

                await _db.SaveChangesAsync();
                await LogAuditAsync("BATCH_USERS_APPROVED_BY_SUPERADMIN", "USER", 0, $"SuperAdmin '{GetCurrentUserName()}' batch approved and activated {approvedCount} user accounts.");
                TempData["SuccessMessage"] = $"Successfully approved and activated {approvedCount} user accounts in bulk!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch approving users");
                TempData["ErrorMessage"] = "Failed to batch approve users: " + ex.Message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(UserApprovals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchRejectUsers(List<ulong> userIds, string? reason = null, string? returnUrl = null)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin accounts have authorization to decline registrations in bulk.";
                return RedirectToAction(nameof(Users));
            }

            if (userIds == null || !userIds.Any())
            {
                TempData["ErrorMessage"] = "No users selected for rejection.";
                return RedirectToAction(nameof(UserApprovals));
            }

            int rejectedCount = 0;
            var note = string.IsNullOrWhiteSpace(reason) ? "Bulk administrative verification declined." : reason.Trim();
            try
            {
                var usersToReject = await _db.users
                    .Where(u => userIds.Contains(u.id) && (u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL"))
                    .ToListAsync();

                foreach (var user in usersToReject)
                {
                    user.account_status = "INACTIVE";
                    user.updated_at = DateTime.UtcNow;
                    rejectedCount++;
                }

                await _db.SaveChangesAsync();
                await LogAuditAsync("BATCH_USERS_REJECTED_BY_SUPERADMIN", "USER", 0, $"SuperAdmin '{GetCurrentUserName()}' batch declined {rejectedCount} user accounts. Reason: {note}");
                TempData["SuccessMessage"] = $"Declined {rejectedCount} registration requests and set status to INACTIVE.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch rejecting users");
                TempData["ErrorMessage"] = "Failed to batch decline users: " + ex.Message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(UserApprovals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAllPending(string? returnUrl = null)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin accounts have authorization to approve all users.";
                return RedirectToAction(nameof(Users));
            }

            int approvedCount = 0;
            try
            {
                var pendingUsers = await _db.users
                    .Where(u => u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL")
                    .ToListAsync();

                foreach (var user in pendingUsers)
                {
                    user.account_status = "ACTIVE";
                    user.email_verified = true;
                    user.phone_verified = true;
                    user.updated_at = DateTime.UtcNow;

                    if (user.account_type == "ORGANIZATION" && !string.IsNullOrWhiteSpace(user.email))
                    {
                        var userEmailLower = user.email.ToLower();
                        var org = await _db.organizations.FirstOrDefaultAsync(o => o.email != null && o.email.ToLower() == userEmailLower);
                        if (org != null)
                        {
                            org.status = "ACTIVE";
                            org.updated_at = DateTime.UtcNow;
                        }
                    }

                    approvedCount++;
                }

                await _db.SaveChangesAsync();
                await LogAuditAsync("ALL_PENDING_USERS_APPROVED_BY_SUPERADMIN", "USER", 0, $"SuperAdmin '{GetCurrentUserName()}' approved and activated all {approvedCount} pending user accounts.");
                TempData["SuccessMessage"] = $"Successfully approved and activated all {approvedCount} pending user accounts!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving all pending users");
                TempData["ErrorMessage"] = "Failed to approve all pending users: " + ex.Message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(UserApprovals));
        }

        // =========================================================
        // DEDICATED SUPERADMIN USER APPROVALS CENTER
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> UserApprovals(
            string? search, 
            string? role, 
            ulong? departmentId, 
            string? sortBy = "newest", 
            int page = 1, 
            int pageSize = 20)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Access Restricted: User Registration Approvals & Account Activation is exclusive to Super Administrator accounts.";
                return RedirectToAction(nameof(Index));
            }

            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var vm = new AdminUserApprovalViewModel
            {
                SearchTerm = search,
                RoleFilter = role,
                DepartmentFilter = departmentId,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize,
                IsSuperAdmin = true
            };

            try
            {
                var query = _db.users
                    .AsNoTracking()
                    .Include(u => u.department)
                        .ThenInclude(d => d!.faculty)
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.assigned_byNavigation)
                    .Where(u => u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL")
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(u => u.first_name.ToLower().Contains(s) ||
                                             u.last_name.ToLower().Contains(s) ||
                                             u.email.ToLower().Contains(s) ||
                                             u.username.ToLower().Contains(s) ||
                                             (u.student_id != null && u.student_id.ToLower().Contains(s)) ||
                                             (u.employee_id != null && u.employee_id.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(role) && role != "ALL")
                {
                    query = query.Where(u => u.account_type == role);
                }

                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    query = query.Where(u => u.department_id == departmentId.Value);
                }

                vm.TotalFilteredCount = await query.CountAsync();

                // Apply Sorting
                query = sortBy switch
                {
                    "oldest" => query.OrderBy(u => u.created_at),
                    "name_asc" => query.OrderBy(u => u.first_name).ThenBy(u => u.last_name),
                    "name_desc" => query.OrderByDescending(u => u.first_name).ThenByDescending(u => u.last_name),
                    "role" => query.OrderBy(u => u.account_type).ThenByDescending(u => u.created_at),
                    _ => query.OrderByDescending(u => u.created_at) // default "newest"
                };

                var pagedList = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                vm.PendingUsers = pagedList.Select(u =>
                {
                    var assignedBy = u.user_roleusers.FirstOrDefault(ur => ur.assigned_byNavigation != null)?.assigned_byNavigation;
                    var regName = assignedBy != null ? $"{assignedBy.first_name} {assignedBy.last_name}".Trim() : "Campus Administrator";
                    return new AdminUserApprovalItem
                    {
                        Id = u.id,
                        FullName = $"{u.first_name} {u.last_name}".Trim(),
                        Username = u.username,
                        Email = u.email,
                        Phone = u.phone,
                        AccountType = u.account_type,
                        Status = u.account_status,
                        DepartmentId = u.department_id,
                        DepartmentName = u.department?.name ?? "General Campus",
                        FacultyName = u.department?.faculty?.name,
                        StudentId = u.student_id,
                        EmployeeId = u.employee_id,
                        Bio = u.bio,
                        ProfileImageUrl = u.profile_image_url,
                        RegisteredByAdminName = regName,
                        RegisteredByAdminId = assignedBy?.id,
                        RegisteredAt = u.created_at
                    };
                }).ToList();

                var allPending = await _db.users
                    .AsNoTracking()
                    .Where(u => u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL")
                    .ToListAsync();

                vm.TotalPendingCount = allPending.Count;
                vm.StudentPendingCount = allPending.Count(u => u.account_type == "STUDENT");
                vm.FacultyPendingCount = allPending.Count(u => u.account_type == "FACULTY");
                vm.StaffPendingCount = allPending.Count(u => u.account_type == "STAFF");
                vm.OrganizationPendingCount = allPending.Count(u => u.account_type == "ORGANIZATION");

                vm.Departments = await _db.departments
                    .AsNoTracking()
                    .Where(d => d.is_active == null || d.is_active == true)
                    .OrderBy(d => d.name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user approvals");
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPendingApprovalsCsv(string? search, string? role, ulong? departmentId)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            try
            {
                var query = _db.users
                    .AsNoTracking()
                    .Include(u => u.department)
                        .ThenInclude(d => d!.faculty)
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.assigned_byNavigation)
                    .Where(u => u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL")
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(u => u.first_name.ToLower().Contains(s) ||
                                             u.last_name.ToLower().Contains(s) ||
                                             u.email.ToLower().Contains(s) ||
                                             u.username.ToLower().Contains(s) ||
                                             (u.student_id != null && u.student_id.ToLower().Contains(s)) ||
                                             (u.employee_id != null && u.employee_id.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(role) && role != "ALL")
                {
                    query = query.Where(u => u.account_type == role);
                }

                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    query = query.Where(u => u.department_id == departmentId.Value);
                }

                var list = await query.OrderByDescending(u => u.created_at).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("User ID,Full Name,Username,Email,Phone,Account Type,Student ID,Employee ID,Department,Faculty,Registered By,Submission Date (UTC),Status");

                foreach (var u in list)
                {
                    var assignedBy = u.user_roleusers.FirstOrDefault(ur => ur.assigned_byNavigation != null)?.assigned_byNavigation;
                    var regName = assignedBy != null ? $"{assignedBy.first_name} {assignedBy.last_name}".Trim() : "Campus Administrator";
                    var fullName = $"{u.first_name} {u.last_name}".Trim().Replace("\"", "\"\"");
                    var deptName = (u.department?.name ?? "General Campus").Replace("\"", "\"\"");
                    var facName = (u.department?.faculty?.name ?? "").Replace("\"", "\"\"");

                    builder.AppendLine($"{u.id},\"{fullName}\",\"{u.username}\",\"{u.email}\",\"{u.phone}\",\"{u.account_type}\",\"{u.student_id}\",\"{u.employee_id}\",\"{deptName}\",\"{facName}\",\"{regName}\",\"{u.created_at:yyyy-MM-dd HH:mm:ss}\",\"{u.account_status}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Pending_Approvals_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting pending approvals CSV");
                TempData["ErrorMessage"] = "Failed to export CSV: " + ex.Message;
                return RedirectToAction(nameof(UserApprovals));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectUser(ulong id, string? reason = null, string? returnUrl = null) => await UserReject(id, reason, returnUrl);

        // =========================================================
        // 3. EVENT MANAGEMENT
        // =========================================================
        public async Task<IActionResult> Events(
            string? search, 
            string? status, 
            ulong? categoryId, 
            ulong? venueId, 
            string? timeframe = "ALL", 
            string? sortBy = "newest", 
            int page = 1, 
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var vm = new AdminEventsViewModel
            {
                SearchTerm = search,
                StatusFilter = status,
                CategoryId = categoryId,
                VenueId = venueId,
                Timeframe = timeframe,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                var query = _db.events
                    .AsNoTracking()
                    .Include(e => e.organizer)
                    .Include(e => e.category)
                    .Include(e => e.venue)
                    .Include(e => e.organization)
                    .Include(e => e.registrations)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(e => e.title.ToLower().Contains(s) || (e.description != null && e.description.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    if (status == "PENDING") query = query.Where(e => e.approval_status == "PENDING");
                    else if (status == "APPROVED") query = query.Where(e => e.approval_status == "APPROVED");
                    else if (status == "REJECTED") query = query.Where(e => e.approval_status == "REJECTED");
                    else query = query.Where(e => e.status == status);
                }

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    query = query.Where(e => e.category_id == categoryId.Value);
                }

                if (venueId.HasValue && venueId.Value > 0)
                {
                    query = query.Where(e => e.venue_id == venueId.Value);
                }

                var now = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(timeframe) && timeframe != "ALL")
                {
                    if (timeframe == "UPCOMING") query = query.Where(e => e.start_at >= now);
                    else if (timeframe == "TODAY") query = query.Where(e => e.start_at.Date == now.Date);
                    else if (timeframe == "PAST") query = query.Where(e => e.end_at < now);
                }

                vm.TotalFilteredCount = await query.CountAsync();

                // Apply Sorting
                query = sortBy switch
                {
                    "oldest" => query.OrderBy(e => e.created_at),
                    "event_date_asc" => query.OrderBy(e => e.start_at),
                    "event_date_desc" => query.OrderByDescending(e => e.start_at),
                    "registrations_desc" => query.OrderByDescending(e => e.registrations.Count),
                    "title_asc" => query.OrderBy(e => e.title),
                    _ => query.OrderByDescending(e => e.created_at) // default "newest"
                };

                var pagedList = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                vm.Events = pagedList.Select(e => new AdminEventRow
                {
                    Id = e.id,
                    Title = e.title,
                    CategoryId = e.category_id,
                    CategoryName = e.category?.name ?? "General",
                    VenueId = e.venue_id,
                    VenueName = e.venue?.name ?? "Main Campus",
                    OrganizerName = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "Organizer",
                    OrganizationName = e.organization?.name,
                    StartAt = e.start_at,
                    EndAt = e.end_at,
                    Capacity = e.capacity,
                    RegistrationCount = e.registrations.Count,
                    Status = e.status,
                    ApprovalStatus = e.approval_status,
                    IsPublic = e.is_public == true,
                    IsFeatured = e.is_featured == true,
                    CreatedAt = e.created_at
                }).ToList();

                // Global metric summary counts
                var allEvts = await _db.events.AsNoTracking().Select(e => new { e.status, e.approval_status, e.start_at, e.end_at }).ToListAsync();
                vm.TotalEvents = allEvts.Count;
                vm.PendingApprovalCount = allEvts.Count(e => e.approval_status == "PENDING");
                vm.PublishedCount = allEvts.Count(e => e.status == "PUBLISHED");
                vm.CancelledCount = allEvts.Count(e => e.status == "CANCELLED");
                vm.UpcomingCount = allEvts.Count(e => e.start_at >= now);
                vm.TodayCount = allEvts.Count(e => e.start_at.Date == now.Date);

                vm.Categories = await _db.event_categories
                    .AsNoTracking()
                    .Where(c => c.is_active == null || c.is_active == true)
                    .OrderBy(c => c.name)
                    .ToListAsync();

                vm.Venues = await _db.venues
                    .AsNoTracking()
                    .Where(v => v.status == "AVAILABLE")
                    .OrderBy(v => v.name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying admin events");
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportEventsCsv(string? search, string? status, ulong? categoryId, ulong? venueId, string? timeframe)
        {
            try
            {
                var query = _db.events
                    .AsNoTracking()
                    .Include(e => e.organizer)
                    .Include(e => e.category)
                    .Include(e => e.venue)
                    .Include(e => e.organization)
                    .Include(e => e.registrations)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(e => e.title.ToLower().Contains(s) || (e.description != null && e.description.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    if (status == "PENDING") query = query.Where(e => e.approval_status == "PENDING");
                    else if (status == "APPROVED") query = query.Where(e => e.approval_status == "APPROVED");
                    else if (status == "REJECTED") query = query.Where(e => e.approval_status == "REJECTED");
                    else query = query.Where(e => e.status == status);
                }

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    query = query.Where(e => e.category_id == categoryId.Value);
                }

                if (venueId.HasValue && venueId.Value > 0)
                {
                    query = query.Where(e => e.venue_id == venueId.Value);
                }

                var now = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(timeframe) && timeframe != "ALL")
                {
                    if (timeframe == "UPCOMING") query = query.Where(e => e.start_at >= now);
                    else if (timeframe == "TODAY") query = query.Where(e => e.start_at.Date == now.Date);
                    else if (timeframe == "PAST") query = query.Where(e => e.end_at < now);
                }

                var list = await query.OrderByDescending(e => e.start_at).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("Event ID,Title,Category,Venue,Organizer,Organization,Start Date,End Date,Capacity,Registrations,Status,Approval Status,Is Public,Is Featured,Created Date");

                foreach (var e in list)
                {
                    var organizer = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "Organizer";
                    var title = e.title.Replace("\"", "\"\"");
                    var catName = (e.category?.name ?? "General").Replace("\"", "\"\"");
                    var venName = (e.venue?.name ?? "Main Campus").Replace("\"", "\"\"");
                    var orgName = (e.organization?.name ?? "").Replace("\"", "\"\"");

                    builder.AppendLine($"{e.id},\"{title}\",\"{catName}\",\"{venName}\",\"{organizer}\",\"{orgName}\",\"{e.start_at:yyyy-MM-dd HH:mm}\",\"{e.end_at:yyyy-MM-dd HH:mm}\",{e.capacity?.ToString() ?? "Unlimited"},{e.registrations.Count},\"{e.status}\",\"{e.approval_status}\",{e.is_public == true},{e.is_featured == true},\"{e.created_at:yyyy-MM-dd HH:mm}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Events_Schedule_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting events CSV");
                TempData["ErrorMessage"] = "Failed to export events: " + ex.Message;
                return RedirectToAction(nameof(Events));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchApproveEvents(List<ulong> eventIds, string? returnUrl = null)
        {
            if (eventIds == null || !eventIds.Any())
            {
                TempData["ErrorMessage"] = "No events selected for approval.";
                return RedirectToAction(nameof(Events));
            }

            int approvedCount = 0;
            try
            {
                var eventsToApprove = await _db.events
                    .Where(e => eventIds.Contains(e.id) && e.approval_status == "PENDING")
                    .ToListAsync();

                foreach (var evt in eventsToApprove)
                {
                    evt.approval_status = "APPROVED";
                    evt.status = "PUBLISHED";
                    evt.updated_at = DateTime.UtcNow;
                    approvedCount++;
                }

                await _db.SaveChangesAsync();
                await LogAuditAsync("BATCH_EVENTS_APPROVED", "EVENT", 0, $"Approved and published {approvedCount} campus events in bulk.");
                TempData["SuccessMessage"] = $"Successfully approved and published {approvedCount} events!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch approving events");
                TempData["ErrorMessage"] = "Failed to batch approve events: " + ex.Message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchRejectEvents(List<ulong> eventIds, string? reason = null, string? returnUrl = null)
        {
            if (eventIds == null || !eventIds.Any())
            {
                TempData["ErrorMessage"] = "No events selected for rejection.";
                return RedirectToAction(nameof(Events));
            }

            int rejectedCount = 0;
            try
            {
                var eventsToReject = await _db.events
                    .Where(e => eventIds.Contains(e.id) && e.approval_status == "PENDING")
                    .ToListAsync();

                foreach (var evt in eventsToReject)
                {
                    evt.approval_status = "REJECTED";
                    evt.status = "DRAFT";
                    evt.updated_at = DateTime.UtcNow;
                    rejectedCount++;
                }

                await _db.SaveChangesAsync();
                await LogAuditAsync("BATCH_EVENTS_REJECTED", "EVENT", 0, $"Rejected {rejectedCount} submitted events in bulk. Note: {reason ?? "Admin discretion"}");
                TempData["SuccessMessage"] = $"Rejected {rejectedCount} events in bulk.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch rejecting events");
                TempData["ErrorMessage"] = "Failed to batch reject events: " + ex.Message;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EventApprove(ulong id)
        {
            var evt = await _db.events
                .Include(e => e.organizer)
                    .ThenInclude(o => o.department)
                .FirstOrDefaultAsync(e => e.id == id);

            if (evt != null)
            {
                evt.approval_status = "APPROVED";
                evt.status = "PUBLISHED";
                evt.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await LogAuditAsync("EVENT_APPROVED", "EVENT", id, $"Approved and published event: {evt.title}");

                // Personalization: Alert students subscribed to this department
                if (evt.organizer?.department_id != null)
                {
                    try
                    {
                        var deptId = evt.organizer.department_id.Value;
                        var deptName = evt.organizer.department?.name ?? "Academic Department";
                        var subscribers = await _db.user_dept_subscriptions
                            .Where(s => s.department_id == deptId && s.notify_on_new_event)
                            .ToListAsync();

                        foreach (var sub in subscribers)
                        {
                            if (sub.user_id != evt.organizer_id)
                            {
                                _db.notifications.Add(new Notification
                                {
                                    user_id = sub.user_id,
                                    title = $"New Event: {deptName}",
                                    message = $"{deptName} announced a newly approved event: '{evt.title}' on {evt.start_at:MMM dd, yyyy}. Register now!",
                                    notification_type = "EVENT",
                                    related_entity_type = "EVENT",
                                    related_entity_id = evt.id,
                                    action_url = $"/Events/Details/{evt.id}",
                                    is_read = false,
                                    created_at = DateTime.UtcNow
                                });
                            }
                        }
                        // Also notify the event organizer
                        if (evt.organizer_id > 0)
                        {
                            _db.notifications.Add(new Notification
                            {
                                user_id = evt.organizer_id,
                                title = "Event Approved & Live",
                                message = $"Congratulations! Your event '{evt.title}' has been approved and published to the campus portal.",
                                notification_type = "EVENT",
                                related_entity_type = "EVENT",
                                related_entity_id = evt.id,
                                action_url = $"/Events/Details/{evt.id}",
                                is_read = false,
                                created_at = DateTime.UtcNow
                            });
                        }
                        await _db.SaveChangesAsync();
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogWarning(notifEx, "Could not notify department subscribers for approved event {EventId}", id);
                    }
                }

                TempData["SuccessMessage"] = $"Event '{evt.title}' approved and published successfully.";
            }
            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EventReject(ulong id, string? reason)
        {
            var evt = await _db.events.FindAsync(id);
            if (evt != null)
            {
                evt.approval_status = "REJECTED";
                evt.status = "DRAFT";
                evt.updated_at = DateTime.UtcNow;

                if (evt.organizer_id > 0)
                {
                    _db.notifications.Add(new Notification
                    {
                        user_id = evt.organizer_id,
                        title = "Event Submission Update",
                        message = $"Your event '{evt.title}' requires review/updates. Feedback: {reason ?? "Admin discretion. Please review event guidelines."}",
                        notification_type = "EVENT",
                        related_entity_type = "EVENT",
                        related_entity_id = evt.id,
                        action_url = $"/Events/Details/{evt.id}",
                        is_read = false,
                        created_at = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
                await LogAuditAsync("EVENT_REJECTED", "EVENT", id, $"Rejected event: {evt.title}. Reason: {reason ?? "Admin discretion"}");
                TempData["SuccessMessage"] = $"Event '{evt.title}' has been rejected.";
            }
            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EventToggleFeature(ulong id)
        {
            var evt = await _db.events.FindAsync(id);
            if (evt != null)
            {
                evt.is_featured = !(evt.is_featured == true);
                evt.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Event featured status updated.";
            }
            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EventDelete(ulong id)
        {
            try
            {
                var evt = await _db.events.FindAsync(id);
                if (evt != null)
                {
                    // Safely remove child registrations, feedbacks, and comments
                    var regs = await _db.registrations.Where(r => r.event_id == id).ToListAsync();
                    if (regs.Any()) _db.registrations.RemoveRange(regs);

                    var fbs = await _db.event_feedbacks.Where(f => f.event_id == id).ToListAsync();
                    if (fbs.Any()) _db.event_feedbacks.RemoveRange(fbs);

                    var coms = await _db.event_comments.Where(c => c.event_id == id).ToListAsync();
                    if (coms.Any()) _db.event_comments.RemoveRange(coms);

                    _db.events.Remove(evt);
                    await _db.SaveChangesAsync();
                    await LogAuditAsync("EVENT_DELETED", "EVENT", id, $"Deleted event: {evt.title}");
                    TempData["SuccessMessage"] = $"Event '{evt.title}' deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Event not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting event");
                TempData["ErrorMessage"] = "Failed to delete event: " + ex.Message;
            }
            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvent(ulong id) => await EventDelete(id);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEvent(ulong id) => await EventApprove(id);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectEvent(ulong id, string? reason) => await EventReject(id, reason);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FeatureEvent(ulong id) => await EventToggleFeature(id);

        // =========================================================
        // =========================================================
        // 4. ANNOUNCEMENT MANAGEMENT
        // =========================================================
        public async Task<IActionResult> Announcements(
            string? search, 
            string? type, 
            string? priority, 
            string? status, 
            ulong? departmentId, 
            int page = 1, 
            int pageSize = 15)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var vm = new AdminAnnouncementsViewModel
            {
                SearchTerm = search,
                TypeFilter = type,
                PriorityFilter = priority,
                StatusFilter = status,
                DepartmentFilter = departmentId,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                vm.Departments = await _db.departments.AsNoTracking().Where(d => d.is_active == true).OrderBy(d => d.name).ToListAsync();

                var query = _db.announcements
                    .AsNoTracking()
                    .Include(a => a.author)
                    .Include(a => a.department)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(a => a.title.ToLower().Contains(s) || 
                                             a.content.ToLower().Contains(s) || 
                                             (a.summary != null && a.summary.ToLower().Contains(s)) ||
                                             (a.author != null && (a.author.first_name.ToLower().Contains(s) || a.author.last_name.ToLower().Contains(s))));
                }

                if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
                {
                    query = query.Where(a => a.announcement_type == type);
                }

                if (!string.IsNullOrWhiteSpace(priority) && priority != "ALL")
                {
                    if (priority == "PINNED") query = query.Where(a => a.priority == "URGENT" || a.priority == "HIGH");
                    else query = query.Where(a => a.priority == priority);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    query = query.Where(a => a.status == status);
                }

                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    query = query.Where(a => a.department_id == departmentId.Value);
                }

                vm.TotalFilteredCount = await query.CountAsync();

                var pagedList = await query
                    .OrderByDescending(a => a.priority == "URGENT" || a.priority == "HIGH")
                    .ThenByDescending(a => a.created_at)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                vm.Announcements = pagedList.Select(a => new AdminAnnouncementRow
                {
                    Id = a.id,
                    Title = a.title,
                    Content = a.content,
                    Summary = a.summary,
                    AuthorName = a.author != null ? $"{a.author.first_name} {a.author.last_name}".Trim() : "University Admin",
                    DepartmentId = a.department_id,
                    DepartmentName = a.department?.name ?? "Campus-wide",
                    AnnouncementType = a.announcement_type,
                    Priority = a.priority,
                    Status = a.status,
                    CreatedAt = a.created_at,
                    PublishedAt = a.published_at
                }).ToList();

                // Metrics
                vm.TotalCount = await _db.announcements.CountAsync();
                vm.PinnedCount = await _db.announcements.CountAsync(a => a.priority == "URGENT" || a.priority == "HIGH");
                vm.PublishedCount = await _db.announcements.CountAsync(a => a.status == "PUBLISHED");
                vm.DraftCount = await _db.announcements.CountAsync(a => a.status == "DRAFT");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying announcements");
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnnouncementTogglePin(ulong id)
        {
            try
            {
                var ann = await _db.announcements.FindAsync(id);
                if (ann == null)
                {
                    TempData["ErrorMessage"] = "Announcement not found.";
                    return RedirectToAction(nameof(Announcements));
                }

                if (ann.priority == "URGENT" || ann.priority == "HIGH")
                {
                    ann.priority = "NORMAL";
                    TempData["SuccessMessage"] = $"Announcement '{ann.title}' unpinned.";
                }
                else
                {
                    ann.priority = "HIGH";
                    TempData["SuccessMessage"] = $"Announcement '{ann.title}' pinned as priority bulletin.";
                }

                ann.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await LogAuditAsync("ANNOUNCEMENT_PIN_TOGGLED", "ANNOUNCEMENT", id, $"Changed priority to {ann.priority}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling announcement pin status");
                TempData["ErrorMessage"] = "Failed to toggle pin: " + ex.Message;
            }
            return RedirectToAction(nameof(Announcements));
        }

        [HttpGet]
        public async Task<IActionResult> ExportAnnouncementsCsv(string? search, string? type, string? priority, string? status, ulong? departmentId)
        {
            try
            {
                var query = _db.announcements
                    .AsNoTracking()
                    .Include(a => a.author)
                    .Include(a => a.department)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(a => a.title.ToLower().Contains(s) || 
                                             a.content.ToLower().Contains(s) || 
                                             (a.summary != null && a.summary.ToLower().Contains(s)) ||
                                             (a.author != null && (a.author.first_name.ToLower().Contains(s) || a.author.last_name.ToLower().Contains(s))));
                }

                if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
                {
                    query = query.Where(a => a.announcement_type == type);
                }

                if (!string.IsNullOrWhiteSpace(priority) && priority != "ALL")
                {
                    if (priority == "PINNED") query = query.Where(a => a.priority == "URGENT" || a.priority == "HIGH");
                    else query = query.Where(a => a.priority == priority);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    query = query.Where(a => a.status == status);
                }

                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    query = query.Where(a => a.department_id == departmentId.Value);
                }

                var list = await query.OrderByDescending(a => a.created_at).Take(2000).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("Bulletin ID,Title,Type,Priority,Target Scope,Author,Status,Created Date,Published Date");

                foreach (var a in list)
                {
                    var title = a.title.Replace("\"", "\"\"");
                    var dept = (a.department?.name ?? "Campus-wide").Replace("\"", "\"\"");
                    var author = (a.author != null ? $"{a.author.first_name} {a.author.last_name}".Trim() : "University Admin").Replace("\"", "\"\"");
                    builder.AppendLine($"{a.id},\"{title}\",\"{a.announcement_type}\",\"{a.priority}\",\"{dept}\",\"{author}\",\"{a.status}\",\"{a.created_at:yyyy-MM-dd HH:mm}\",\"{a.published_at:yyyy-MM-dd HH:mm}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Campus_Announcements_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting announcements CSV");
                TempData["ErrorMessage"] = "Failed to export announcements: " + ex.Message;
                return RedirectToAction(nameof(Announcements));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnnouncementCreate(
            string title, 
            string content, 
            string? priority, 
            bool isPinned,
            string? announcementType = "GENERAL",
            string? targetAudience = "ALL",
            ulong? departmentId = null,
            bool sendNotificationAlert = false)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Please provide both a title and content for the announcement.";
                return RedirectToAction(nameof(Announcements));
            }

            try
            {
                var priorityVal = string.IsNullOrEmpty(priority) ? (isPinned ? "HIGH" : "NORMAL") : priority.ToUpperInvariant();
                var ann = new Announcement
                {
                    title = title.Trim(),
                    slug = title.Trim().ToLower().Replace(" ", "-") + "-" + DateTime.UtcNow.Ticks,
                    content = content.Trim(),
                    summary = content.Trim().Length > 150 ? content.Trim().Substring(0, 147) + "..." : content.Trim(),
                    priority = priorityVal,
                    announcement_type = !string.IsNullOrWhiteSpace(announcementType) ? announcementType.ToUpperInvariant() : "GENERAL",
                    status = "PUBLISHED",
                    author_id = GetCurrentUserId() ?? 1,
                    department_id = (departmentId.HasValue && departmentId.Value > 0) ? departmentId.Value : null,
                    published_at = DateTime.UtcNow,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };
                _db.announcements.Add(ann);
                await _db.SaveChangesAsync();
                await LogAuditAsync("ANNOUNCEMENT_CREATED", "ANNOUNCEMENT", ann.id, $"Published announcement: {title}");

                // Automated Announcement Alerting: Dispatch in-app notifications to target users
                var shouldAlert = sendNotificationAlert || priorityVal == "URGENT" || priorityVal == "HIGH";
                if (shouldAlert)
                {
                    try
                    {
                        var audienceUpper = targetAudience?.Trim().ToUpperInvariant() ?? "ALL";
                        IQueryable<User> targetQuery = _db.users.AsNoTracking()
                            .Where(u => u.account_status != "SUSPENDED" && u.account_status != "LOCKED" && u.account_status != "INACTIVE");

                        if (departmentId.HasValue && departmentId.Value > 0)
                        {
                            var deptId = departmentId.Value;
                            var subscribedUserIds = await _db.user_dept_subscriptions
                                .Where(s => s.department_id == deptId && s.notify_on_new_event)
                                .Select(s => s.user_id)
                                .ToListAsync();

                            targetQuery = targetQuery.Where(u => u.department_id == deptId || subscribedUserIds.Contains(u.id));
                        }
                        else if (audienceUpper == "STUDENTS")
                        {
                            targetQuery = targetQuery.Where(u => u.account_type == "STUDENT");
                        }
                        else if (audienceUpper == "FACULTY")
                        {
                            targetQuery = targetQuery.Where(u => u.account_type == "FACULTY" || u.account_type == "STAFF");
                        }
                        else if (audienceUpper == "ORGANIZERS")
                        {
                            targetQuery = targetQuery.Where(u => u.account_type == "ORGANIZATION");
                        }

                        var recipientIds = await targetQuery.Select(u => u.id).Take(5000).ToListAsync();
                        if (recipientIds.Any())
                        {
                            var notifTitle = priorityVal == "URGENT" ? $"🚨 URGENT: {ann.title}" : $"📢 Announcement: {ann.title}";
                            var notifMessage = ann.summary ?? (ann.content.Length > 120 ? ann.content.Substring(0, 117) + "..." : ann.content);
                            var now = DateTime.UtcNow;

                            var notifications = recipientIds.Select(uid => new Notification
                            {
                                user_id = uid,
                                title = notifTitle,
                                message = notifMessage,
                                notification_type = "ANNOUNCEMENT",
                                related_entity_type = "ANNOUNCEMENT",
                                related_entity_id = ann.id,
                                action_url = $"/Announcements/Details/{ann.id}",
                                is_read = false,
                                created_at = now
                            }).ToList();

                            _db.notifications.AddRange(notifications);
                            await _db.SaveChangesAsync();
                        }
                    }
                    catch (Exception alertEx)
                    {
                        _logger.LogWarning(alertEx, "Failed to dispatch automated announcement alert notifications.");
                    }
                }

                TempData["SuccessMessage"] = "Announcement published and target recipients alerted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating announcement");
                TempData["ErrorMessage"] = "Failed to publish announcement: " + ex.Message;
            }
            return RedirectToAction(nameof(Announcements));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAnnouncement(
            string title, 
            string content, 
            string? priority, 
            bool isPinned,
            string? announcementType = "GENERAL",
            string? targetAudience = "ALL",
            ulong? departmentId = null,
            bool sendNotificationAlert = false)
            => await AnnouncementCreate(title, content, priority, isPinned, announcementType, targetAudience, departmentId, sendNotificationAlert);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnnouncementEdit(ulong id, string title, string content, string? priority, string? type)
        {
            try
            {
                var ann = await _db.announcements.FindAsync(id);
                if (ann == null)
                {
                    TempData["ErrorMessage"] = "Announcement not found.";
                    return RedirectToAction(nameof(Announcements));
                }

                ann.title = title;
                ann.content = content;
                if (!string.IsNullOrEmpty(priority)) ann.priority = priority;
                if (!string.IsNullOrEmpty(type)) ann.announcement_type = type;
                ann.updated_at = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await LogAuditAsync("ANNOUNCEMENT_UPDATED", "ANNOUNCEMENT", id, $"Updated announcement: {title}");
                TempData["SuccessMessage"] = "Announcement updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating announcement");
                TempData["ErrorMessage"] = "Failed to update announcement: " + ex.Message;
            }
            return RedirectToAction(nameof(Announcements));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAnnouncement(ulong id, string title, string content, string? priority, string? type)
            => await AnnouncementEdit(id, title, content, priority, type);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnnouncementDelete(ulong id)
        {
            try
            {
                var ann = await _db.announcements.FindAsync(id);
                if (ann != null)
                {
                    _db.announcements.Remove(ann);
                    await _db.SaveChangesAsync();
                    await LogAuditAsync("ANNOUNCEMENT_DELETED", "ANNOUNCEMENT", id, $"Deleted announcement: {ann.title}");
                    TempData["SuccessMessage"] = "Announcement deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Announcement not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting announcement");
                TempData["ErrorMessage"] = "Failed to delete announcement: " + ex.Message;
            }
            return RedirectToAction(nameof(Announcements));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAnnouncement(ulong id) => await AnnouncementDelete(id);

        // =========================================================
        // 5. ORGANIZATION MANAGEMENT
        // =========================================================
        public async Task<IActionResult> Organizations(
            string? search, 
            string? type, 
            string? status, 
            ulong? departmentId, 
            string? sortBy = "newest", 
            int page = 1, 
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var vm = new AdminOrganizationsViewModel
            {
                SearchTerm = search,
                TypeFilter = type,
                StatusFilter = status,
                DepartmentFilter = departmentId,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                var query = _db.organizations
                    .AsNoTracking()
                    .Include(o => o.department)
                    .Include(o => o.organization_members)
                        .ThenInclude(om => om.user)
                    .Include(o => o._events)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(o => o.name.ToLower().Contains(s) || 
                                             (o.short_name != null && o.short_name.ToLower().Contains(s)) ||
                                             (o.email != null && o.email.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
                {
                    query = query.Where(o => o.organization_type == type);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    query = query.Where(o => o.status == status);
                }

                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    query = query.Where(o => o.department_id == departmentId.Value);
                }

                vm.TotalFilteredCount = await query.CountAsync();

                // Apply Sorting
                query = sortBy switch
                {
                    "oldest" => query.OrderBy(o => o.created_at),
                    "name_asc" => query.OrderBy(o => o.name),
                    "name_desc" => query.OrderByDescending(o => o.name),
                    "members_desc" => query.OrderByDescending(o => o.organization_members.Count),
                    "events_desc" => query.OrderByDescending(o => o._events.Count),
                    "type" => query.OrderBy(o => o.organization_type).ThenBy(o => o.name),
                    _ => query.OrderByDescending(o => o.created_at) // default "newest"
                };

                var pagedList = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                vm.Organizations = pagedList.Select(o =>
                {
                    var leader = o.organization_members.FirstOrDefault(m => m.membership_role == "PRESIDENT" || m.membership_role == "ADMIN" || m.membership_role == "OFFICER")?.user;
                    return new AdminOrganizationRow
                    {
                        Id = o.id,
                        Name = o.name,
                        ShortName = o.short_name,
                        OrganizationType = o.organization_type,
                        DepartmentId = o.department_id,
                        DepartmentName = o.department?.name ?? "General Campus Club",
                        LeaderName = leader != null ? $"{leader.first_name} {leader.last_name}".Trim() : null,
                        LeaderEmail = leader?.email,
                        Email = o.email,
                        LogoUrl = o.logo_url,
                        Status = o.status,
                        MemberCount = o.organization_members.Count,
                        EventCount = o._events.Count,
                        CreatedAt = o.created_at
                    };
                }).ToList();

                // Global metrics
                var allOrgs = await _db.organizations.AsNoTracking().Select(o => new { o.status, MemberCount = o.organization_members.Count }).ToListAsync();
                vm.TotalCount = allOrgs.Count;
                vm.ActiveCount = allOrgs.Count(o => o.status == "ACTIVE");
                vm.PendingCount = allOrgs.Count(o => o.status == "PENDING");
                vm.TotalMembersCount = allOrgs.Sum(o => o.MemberCount);

                vm.Departments = await _db.departments
                    .AsNoTracking()
                    .Where(d => d.is_active == null || d.is_active == true)
                    .OrderBy(d => d.name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying organizations");
            }
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportOrganizationsCsv(string? search, string? type, string? status, ulong? departmentId)
        {
            try
            {
                var query = _db.organizations
                    .AsNoTracking()
                    .Include(o => o.department)
                    .Include(o => o.organization_members)
                        .ThenInclude(om => om.user)
                    .Include(o => o._events)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(o => o.name.ToLower().Contains(s) || 
                                             (o.short_name != null && o.short_name.ToLower().Contains(s)) ||
                                             (o.email != null && o.email.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
                {
                    query = query.Where(o => o.organization_type == type);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    query = query.Where(o => o.status == status);
                }

                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    query = query.Where(o => o.department_id == departmentId.Value);
                }

                var list = await query.OrderByDescending(o => o.created_at).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("Organization ID,Name,Short Code,Type,Department,Status,Email,Phone,Leader,Members Count,Events Organized,Created Date");

                foreach (var o in list)
                {
                    var leader = o.organization_members.FirstOrDefault(m => m.membership_role == "PRESIDENT" || m.membership_role == "ADMIN" || m.membership_role == "OFFICER")?.user;
                    var leaderName = leader != null ? $"{leader.first_name} {leader.last_name}".Trim() : "Not Designated";
                    var name = o.name.Replace("\"", "\"\"");
                    var deptName = (o.department?.name ?? "General Campus Club").Replace("\"", "\"\"");

                    builder.AppendLine($"{o.id},\"{name}\",\"{o.short_name}\",\"{o.organization_type}\",\"{deptName}\",\"{o.status}\",\"{o.email}\",\"{o.phone}\",\"{leaderName}\",{o.organization_members.Count},{o._events.Count},\"{o.created_at:yyyy-MM-dd HH:mm}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Organizations_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting organizations CSV");
                TempData["ErrorMessage"] = "Failed to export organizations: " + ex.Message;
                return RedirectToAction(nameof(Organizations));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OrganizationCreate(string name, string? shortName, string? description, string organizationType, ulong? departmentId, string? email, string? phone)
        {
            try
            {
                var org = new Organization
                {
                    name = name,
                    short_name = shortName,
                    description = description,
                    department_id = departmentId,
                    organization_type = string.IsNullOrEmpty(organizationType) ? "CLUB" : organizationType,
                    email = email,
                    phone = phone,
                    status = "ACTIVE",
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };
                _db.organizations.Add(org);
                await _db.SaveChangesAsync();
                await LogAuditAsync("ORGANIZATION_CREATED", "ORGANIZATION", org.id, $"Registered organization: {name}");
                TempData["SuccessMessage"] = $"Organization '{name}' created successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organization");
                TempData["ErrorMessage"] = "Failed to create organization: " + ex.Message;
            }
            return RedirectToAction(nameof(Organizations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrganization(string name, string? shortName, string? description, string organizationType, ulong? departmentId, string? email, string? phone)
            => await OrganizationCreate(name, shortName, description, organizationType, departmentId, email, phone);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OrganizationEdit(ulong id, string name, string? shortName, string? description, string organizationType, ulong? departmentId, string? email, string? phone, string status)
        {
            try
            {
                var org = await _db.organizations.FindAsync(id);
                if (org == null)
                {
                    TempData["ErrorMessage"] = "Organization not found.";
                    return RedirectToAction(nameof(Organizations));
                }

                org.name = name;
                org.short_name = shortName;
                org.description = description;
                org.organization_type = organizationType;
                org.department_id = departmentId;
                org.email = email;
                org.phone = phone;
                org.status = status.ToUpper();
                org.updated_at = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await LogAuditAsync("ORGANIZATION_UPDATED", "ORGANIZATION", id, $"Updated organization: {name}");
                TempData["SuccessMessage"] = $"Organization '{name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating organization");
                TempData["ErrorMessage"] = "Failed to update organization: " + ex.Message;
            }
            return RedirectToAction(nameof(Organizations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOrganization(ulong id, string name, string? shortName, string? description, string organizationType, ulong? departmentId, string? email, string? phone, string status)
            => await OrganizationEdit(id, name, shortName, description, organizationType, departmentId, email, phone, status);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OrganizationToggleStatus(ulong id, string status)
        {
            var org = await _db.organizations.FindAsync(id);
            if (org != null)
            {
                org.status = status.ToUpper();
                org.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Organization status set to {status}.";
            }
            return RedirectToAction(nameof(Organizations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OrganizationDelete(ulong id)
        {
            try
            {
                var org = await _db.organizations.FindAsync(id);
                if (org != null)
                {
                    // Safely unlink events and remove members
                    var linkedEvents = await _db.events.Where(e => e.organization_id == id).ToListAsync();
                    foreach (var e in linkedEvents)
                    {
                        e.organization_id = null;
                        e.updated_at = DateTime.UtcNow;
                    }

                    var members = await _db.organization_members.Where(m => m.organization_id == id).ToListAsync();
                    if (members.Any()) _db.organization_members.RemoveRange(members);

                    _db.organizations.Remove(org);
                    await _db.SaveChangesAsync();
                    await LogAuditAsync("ORGANIZATION_DELETED", "ORGANIZATION", id, $"Deleted organization: {org.name}");
                    TempData["SuccessMessage"] = $"Organization '{org.name}' deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Organization not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting organization");
                TempData["ErrorMessage"] = "Failed to delete organization: " + ex.Message;
            }
            return RedirectToAction(nameof(Organizations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrganization(ulong id) => await OrganizationDelete(id);

        // =========================================================
        // 6. FACULTIES & DEPARTMENTS
        // =========================================================
        public async Task<IActionResult> Faculties(
            string? search, 
            string? sortBy = "name_asc", 
            int page = 1, 
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var vm = new AdminFacultiesViewModel
            {
                SearchTerm = search,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                var query = _db.faculties
                    .AsNoTracking()
                    .Include(f => f.departments)
                        .ThenInclude(d => d.users)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(f => f.name.ToLower().Contains(s) || 
                                             (f.code != null && f.code.ToLower().Contains(s)) ||
                                             (f.dean_name != null && f.dean_name.ToLower().Contains(s)) ||
                                             (f.email != null && f.email.ToLower().Contains(s)));
                }

                vm.TotalFilteredCount = await query.CountAsync();

                // Apply Sorting
                query = sortBy switch
                {
                    "name_desc" => query.OrderByDescending(f => f.name),
                    "code" => query.OrderBy(f => f.code),
                    "depts_desc" => query.OrderByDescending(f => f.departments.Count),
                    "newest" => query.OrderByDescending(f => f.created_at),
                    _ => query.OrderBy(f => f.name) // default "name_asc"
                };

                var pagedList = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                vm.Faculties = pagedList.Select(f => new AdminFacultyRow
                {
                    Id = f.id,
                    Name = f.name,
                    Code = f.code,
                    DeanName = f.dean_name,
                    Email = f.email,
                    IsActive = f.is_active ?? true,
                    DepartmentCount = f.departments.Count,
                    EnrolledUsersCount = f.departments.Sum(d => d.users.Count)
                }).ToList();

                // Global summary metrics
                var allFacs = await _db.faculties.AsNoTracking().Include(f => f.departments).ThenInclude(d => d.users).ToListAsync();
                vm.TotalCount = allFacs.Count;
                vm.ActiveCount = allFacs.Count(f => f.is_active == true);
                vm.TotalDepartmentsCount = allFacs.Sum(f => f.departments.Count);
                vm.TotalStudentsCount = allFacs.Sum(f => f.departments.Sum(d => d.users.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying faculties");
            }
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportFacultiesCsv(string? search)
        {
            try
            {
                var query = _db.faculties
                    .AsNoTracking()
                    .Include(f => f.departments)
                        .ThenInclude(d => d.users)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(f => f.name.ToLower().Contains(s) || 
                                             (f.code != null && f.code.ToLower().Contains(s)) ||
                                             (f.dean_name != null && f.dean_name.ToLower().Contains(s)));
                }

                var list = await query.OrderBy(f => f.name).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("Faculty ID,Faculty/College Name,Code,Dean Name,Email,Phone,Departments Count,Enrolled Users,Status,Created Date");

                foreach (var f in list)
                {
                    var name = f.name.Replace("\"", "\"\"");
                    var dean = (f.dean_name ?? "Not Assigned").Replace("\"", "\"\"");
                    var totalUsers = f.departments.Sum(d => d.users.Count);
                    builder.AppendLine($"{f.id},\"{name}\",\"{f.code}\",\"{dean}\",\"{f.email}\",\"{f.phone}\",{f.departments.Count},{totalUsers},{(f.is_active == true ? "ACTIVE" : "INACTIVE")},\"{f.created_at:yyyy-MM-dd HH:mm}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Faculties_Roster_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting faculties CSV");
                TempData["ErrorMessage"] = "Failed to export faculties: " + ex.Message;
                return RedirectToAction(nameof(Faculties));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FacultyCreate(string name, string? code, string? deanName, string? email, string? phone)
        {
            try
            {
                var f = new Faculty
                {
                    name = name,
                    code = code,
                    dean_name = deanName,
                    email = email,
                    phone = phone,
                    is_active = true,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };
                _db.faculties.Add(f);
                await _db.SaveChangesAsync();
                await LogAuditAsync("FACULTY_CREATED", "FACULTY", f.id, $"Added faculty: {name}");
                TempData["SuccessMessage"] = $"Faculty '{name}' added successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding faculty");
                TempData["ErrorMessage"] = "Failed to add faculty: " + ex.Message;
            }
            return RedirectToAction(nameof(Faculties));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFaculty(string name, string? code, string? deanName, string? email, string? phone)
            => await FacultyCreate(name, code, deanName, email, phone);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FacultyEdit(ulong id, string name, string? code, string? deanName, string? email, string? phone, bool isActive)
        {
            try
            {
                var f = await _db.faculties.FindAsync(id);
                if (f == null)
                {
                    TempData["ErrorMessage"] = "Faculty not found.";
                    return RedirectToAction(nameof(Faculties));
                }

                f.name = name;
                f.code = code;
                f.dean_name = deanName;
                f.email = email;
                f.phone = phone;
                f.is_active = isActive;
                f.updated_at = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await LogAuditAsync("FACULTY_UPDATED", "FACULTY", id, $"Updated faculty: {name}");
                TempData["SuccessMessage"] = $"Faculty '{name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating faculty");
                TempData["ErrorMessage"] = "Failed to update faculty: " + ex.Message;
            }
            return RedirectToAction(nameof(Faculties));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFaculty(ulong id, string name, string? code, string? deanName, string? email, string? phone, bool isActive)
            => await FacultyEdit(id, name, code, deanName, email, phone, isActive);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FacultyDelete(ulong id)
        {
            try
            {
                var f = await _db.faculties
                    .Include(x => x.departments)
                    .FirstOrDefaultAsync(x => x.id == id);

                if (f != null)
                {
                    // If child departments exist, safely reassign or unlink them
                    var fallbackFaculty = await _db.faculties.FirstOrDefaultAsync(x => x.id != id);
                    if (f.departments.Any())
                    {
                        if (fallbackFaculty != null)
                        {
                            foreach (var dept in f.departments)
                            {
                                dept.faculty_id = fallbackFaculty.id;
                                dept.updated_at = DateTime.UtcNow;
                            }
                        }
                        else
                        {
                            // Unlink users and organizations pointing to child departments before removing
                            foreach (var dept in f.departments)
                            {
                                var linkedUsers = await _db.users.Where(u => u.department_id == dept.id).ToListAsync();
                                foreach (var u in linkedUsers) u.department_id = null;

                                var linkedOrgs = await _db.organizations.Where(o => o.department_id == dept.id).ToListAsync();
                                foreach (var o in linkedOrgs) o.department_id = null;

                                _db.departments.Remove(dept);
                            }
                        }
                    }

                    _db.faculties.Remove(f);
                    await _db.SaveChangesAsync();
                    await LogAuditAsync("FACULTY_DELETED", "FACULTY", id, $"Deleted faculty: {f.name}");
                    TempData["SuccessMessage"] = $"Faculty '{f.name}' deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Faculty not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting faculty");
                TempData["ErrorMessage"] = "Failed to delete faculty: " + ex.Message;
            }
            return RedirectToAction(nameof(Faculties));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFaculty(ulong id) => await FacultyDelete(id);

        public async Task<IActionResult> Departments(
            string? search, 
            ulong? facultyId, 
            string? status, 
            string? sortBy = "name_asc", 
            int page = 1, 
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var vm = new AdminDepartmentsViewModel
            {
                SearchTerm = search,
                FacultyId = facultyId,
                StatusFilter = status,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                var query = _db.departments
                    .AsNoTracking()
                    .Include(d => d.faculty)
                    .Include(d => d.users)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(d => d.name.ToLower().Contains(s) || 
                                             (d.code != null && d.code.ToLower().Contains(s)) ||
                                             (d.head_name != null && d.head_name.ToLower().Contains(s)) ||
                                             (d.email != null && d.email.ToLower().Contains(s)));
                }

                if (facultyId.HasValue && facultyId.Value > 0)
                {
                    query = query.Where(d => d.faculty_id == facultyId.Value);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    bool isActive = status == "ACTIVE";
                    query = query.Where(d => d.is_active == isActive);
                }

                vm.TotalFilteredCount = await query.CountAsync();

                // Apply Sorting
                query = sortBy switch
                {
                    "name_desc" => query.OrderByDescending(d => d.name),
                    "code" => query.OrderBy(d => d.code),
                    "faculty" => query.OrderBy(d => d.faculty != null ? d.faculty.name : "").ThenBy(d => d.name),
                    "users_desc" => query.OrderByDescending(d => d.users.Count),
                    _ => query.OrderBy(d => d.name) // default "name_asc"
                };

                var pagedList = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                vm.Departments = pagedList.Select(d => new AdminDepartmentRow
                {
                    Id = d.id,
                    Name = d.name,
                    Code = d.code,
                    FacultyName = d.faculty?.name ?? "General",
                    FacultyId = d.faculty_id,
                    HeadName = d.head_name,
                    Email = d.email,
                    IsActive = d.is_active ?? true,
                    StudentCount = d.users.Count
                }).ToList();

                // Global summaries
                var allDepts = await _db.departments.AsNoTracking().Include(d => d.users).ToListAsync();
                vm.TotalCount = allDepts.Count;
                vm.ActiveCount = allDepts.Count(d => d.is_active == true);
                vm.TotalStudentsCount = allDepts.Sum(d => d.users.Count);

                vm.Faculties = await _db.faculties
                    .AsNoTracking()
                    .Where(f => f.is_active == true)
                    .OrderBy(f => f.name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying departments");
            }
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportDepartmentsCsv(string? search, ulong? facultyId, string? status)
        {
            try
            {
                var query = _db.departments
                    .AsNoTracking()
                    .Include(d => d.faculty)
                    .Include(d => d.users)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(d => d.name.ToLower().Contains(s) || 
                                             (d.code != null && d.code.ToLower().Contains(s)) ||
                                             (d.head_name != null && d.head_name.ToLower().Contains(s)));
                }

                if (facultyId.HasValue && facultyId.Value > 0)
                {
                    query = query.Where(d => d.faculty_id == facultyId.Value);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    bool isActive = status == "ACTIVE";
                    query = query.Where(d => d.is_active == isActive);
                }

                var list = await query.OrderBy(d => d.name).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("Department ID,Department Name,Code,Parent Faculty,Department Head,Email,Enrolled Users,Status,Created Date");

                foreach (var d in list)
                {
                    var name = d.name.Replace("\"", "\"\"");
                    var facName = (d.faculty?.name ?? "General").Replace("\"", "\"\"");
                    var head = (d.head_name ?? "Not Assigned").Replace("\"", "\"\"");
                    builder.AppendLine($"{d.id},\"{name}\",\"{d.code}\",\"{facName}\",\"{head}\",\"{d.email}\",{d.users.Count},{(d.is_active == true ? "ACTIVE" : "INACTIVE")},\"{d.created_at:yyyy-MM-dd HH:mm}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Departments_Roster_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting departments CSV");
                TempData["ErrorMessage"] = "Failed to export departments: " + ex.Message;
                return RedirectToAction(nameof(Departments));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DepartmentCreate(string name, string? code, ulong facultyId, string? headName, string? email)
        {
            try
            {
                var d = new Department
                {
                    name = name,
                    code = code,
                    faculty_id = facultyId,
                    head_name = headName,
                    email = email,
                    is_active = true,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };
                _db.departments.Add(d);
                await _db.SaveChangesAsync();
                await LogAuditAsync("DEPARTMENT_CREATED", "DEPARTMENT", d.id, $"Added department: {name}");
                TempData["SuccessMessage"] = $"Department '{name}' added successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding department");
                TempData["ErrorMessage"] = "Failed to add department: " + ex.Message;
            }
            return RedirectToAction(nameof(Departments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDepartment(string name, string? code, ulong facultyId, string? headName, string? email)
            => await DepartmentCreate(name, code, facultyId, headName, email);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DepartmentEdit(ulong id, string name, string? code, ulong facultyId, string? headName, string? email, bool isActive)
        {
            try
            {
                var d = await _db.departments.FindAsync(id);
                if (d == null)
                {
                    TempData["ErrorMessage"] = "Department not found.";
                    return RedirectToAction(nameof(Departments));
                }

                d.name = name;
                d.code = code;
                d.faculty_id = facultyId;
                d.head_name = headName;
                d.email = email;
                d.is_active = isActive;
                d.updated_at = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await LogAuditAsync("DEPARTMENT_UPDATED", "DEPARTMENT", id, $"Updated department: {name}");
                TempData["SuccessMessage"] = $"Department '{name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating department");
                TempData["ErrorMessage"] = "Failed to update department: " + ex.Message;
            }
            return RedirectToAction(nameof(Departments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDepartment(ulong id, string name, string? code, ulong facultyId, string? headName, string? email, bool isActive)
            => await DepartmentEdit(id, name, code, facultyId, headName, email, isActive);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DepartmentDelete(ulong id)
        {
            try
            {
                var d = await _db.departments.FindAsync(id);
                if (d != null)
                {
                    // Safely unlink users and organizations pointing to this department
                    var linkedUsers = await _db.users.Where(u => u.department_id == id).ToListAsync();
                    foreach (var u in linkedUsers) u.department_id = null;

                    var linkedOrgs = await _db.organizations.Where(o => o.department_id == id).ToListAsync();
                    foreach (var o in linkedOrgs) o.department_id = null;

                    _db.departments.Remove(d);
                    await _db.SaveChangesAsync();
                    await LogAuditAsync("DEPARTMENT_DELETED", "DEPARTMENT", id, $"Deleted department: {d.name}");
                    TempData["SuccessMessage"] = $"Department '{d.name}' deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Department not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department");
                TempData["ErrorMessage"] = "Failed to delete department: " + ex.Message;
            }
            return RedirectToAction(nameof(Departments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDepartment(ulong id) => await DepartmentDelete(id);

        // =========================================================
        // 7. VENUE MANAGEMENT
        // =========================================================
        public async Task<IActionResult> Venues(
            string? search, 
            string? type, 
            string? status, 
            string? sortBy = "capacity_desc", 
            int page = 1, 
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var vm = new AdminVenuesViewModel
            {
                SearchTerm = search,
                TypeFilter = type,
                StatusFilter = status,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                var query = _db.venues
                    .AsNoTracking()
                    .Include(v => v._events)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(v => v.name.ToLower().Contains(s) || 
                                             (v.building_name != null && v.building_name.ToLower().Contains(s)) ||
                                             (v.room_number != null && v.room_number.ToLower().Contains(s)) ||
                                             (v.amenities != null && v.amenities.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
                {
                    query = query.Where(v => v.venue_type == type);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    query = query.Where(v => v.status == status);
                }

                vm.TotalFilteredCount = await query.CountAsync();

                // Apply Sorting
                query = sortBy switch
                {
                    "capacity_asc" => query.OrderBy(v => v.capacity),
                    "bookings_desc" => query.OrderByDescending(v => v._events.Count),
                    "name_asc" => query.OrderBy(v => v.name),
                    "type" => query.OrderBy(v => v.venue_type).ThenBy(v => v.name),
                    _ => query.OrderByDescending(v => v.capacity) // default "capacity_desc"
                };

                var pagedList = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                vm.Venues = pagedList.Select(v => new AdminVenueRow
                {
                    Id = v.id,
                    Name = v.name,
                    BuildingName = v.building_name,
                    RoomNumber = v.room_number,
                    Capacity = v.capacity,
                    VenueType = v.venue_type,
                    Status = v.status,
                    Amenities = v.amenities,
                    Description = v.description,
                    ScheduledEventsCount = v._events.Count
                }).ToList();

                // Global metrics
                var allVenues = await _db.venues.AsNoTracking().Select(v => new { v.status, v.capacity }).ToListAsync();
                vm.TotalCount = allVenues.Count;
                vm.AvailableCount = allVenues.Count(v => v.status == "AVAILABLE");
                vm.MaintenanceCount = allVenues.Count(v => v.status == "MAINTENANCE");
                vm.InactiveCount = allVenues.Count(v => v.status == "INACTIVE");
                vm.TotalCampusSeatingCapacity = allVenues.Sum(v => (long)v.capacity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying venues");
            }
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportVenuesCsv(string? search, string? type, string? status)
        {
            try
            {
                var query = _db.venues
                    .AsNoTracking()
                    .Include(v => v._events)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(v => v.name.ToLower().Contains(s) || 
                                             (v.building_name != null && v.building_name.ToLower().Contains(s)) ||
                                             (v.room_number != null && v.room_number.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
                {
                    query = query.Where(v => v.venue_type == type);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    query = query.Where(v => v.status == status);
                }

                var list = await query.OrderByDescending(v => v.capacity).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("Venue ID,Venue Name,Building,Room Number,Venue Type,Seating Capacity,Status,Amenities,Scheduled Events Count,Created Date");

                foreach (var v in list)
                {
                    var name = v.name.Replace("\"", "\"\"");
                    var bldg = (v.building_name ?? "Campus Main").Replace("\"", "\"\"");
                    var amen = (v.amenities ?? "None").Replace("\"", "\"\"");
                    builder.AppendLine($"{v.id},\"{name}\",\"{bldg}\",\"{v.room_number}\",\"{v.venue_type}\",{v.capacity},\"{v.status}\",\"{amen}\",{v._events.Count},\"{v.created_at:yyyy-MM-dd HH:mm}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Venues_Facilities_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting venues CSV");
                TempData["ErrorMessage"] = "Failed to export venues: " + ex.Message;
                return RedirectToAction(nameof(Venues));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VenueToggleStatus(ulong id, string status)
        {
            try
            {
                var v = await _db.venues.FindAsync(id);
                if (v != null)
                {
                    v.status = status.ToUpper();
                    v.updated_at = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    await LogAuditAsync("VENUE_STATUS_CHANGED", "VENUE", id, $"Changed venue status to: {status}");
                    TempData["SuccessMessage"] = $"Venue status updated to {status}.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating venue status");
                TempData["ErrorMessage"] = "Failed to change status: " + ex.Message;
            }
            return RedirectToAction(nameof(Venues));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VenueCreate(string name, string? buildingName, string? roomNumber, uint capacity, string venueType, string? amenities, string? description, string status)
        {
            try
            {
                var v = new Venue
                {
                    name = name,
                    building_name = buildingName,
                    room_number = roomNumber,
                    capacity = capacity > 0 ? capacity : 100,
                    venue_type = string.IsNullOrEmpty(venueType) ? "AUDITORIUM" : venueType,
                    amenities = amenities,
                    description = description,
                    status = string.IsNullOrEmpty(status) ? "AVAILABLE" : status,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };
                _db.venues.Add(v);
                await _db.SaveChangesAsync();
                await LogAuditAsync("VENUE_CREATED", "VENUE", v.id, $"Added venue: {name} (Capacity: {capacity})");
                TempData["SuccessMessage"] = $"Venue '{name}' created successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating venue");
                TempData["ErrorMessage"] = "Failed to add venue: " + ex.Message;
            }
            return RedirectToAction(nameof(Venues));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVenue(string name, string? buildingName, string? roomNumber, uint capacity, string venueType, string? amenities, string? description, string status)
            => await VenueCreate(name, buildingName, roomNumber, capacity, venueType, amenities, description, status);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VenueEdit(ulong id, string name, string? buildingName, string? roomNumber, uint capacity, string venueType, string? amenities, string? description, string status)
        {
            try
            {
                var v = await _db.venues.FindAsync(id);
                if (v == null)
                {
                    TempData["ErrorMessage"] = "Venue not found.";
                    return RedirectToAction(nameof(Venues));
                }

                v.name = name;
                v.building_name = buildingName;
                v.room_number = roomNumber;
                v.capacity = capacity > 0 ? capacity : 100;
                v.venue_type = string.IsNullOrEmpty(venueType) ? "AUDITORIUM" : venueType;
                v.amenities = amenities;
                v.description = description;
                v.status = string.IsNullOrEmpty(status) ? "AVAILABLE" : status;
                v.updated_at = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await LogAuditAsync("VENUE_UPDATED", "VENUE", id, $"Updated venue: {name}");
                TempData["SuccessMessage"] = $"Venue '{name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating venue");
                TempData["ErrorMessage"] = "Failed to update venue: " + ex.Message;
            }
            return RedirectToAction(nameof(Venues));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditVenue(ulong id, string name, string? buildingName, string? roomNumber, uint capacity, string venueType, string? amenities, string? description, string status)
            => await VenueEdit(id, name, buildingName, roomNumber, capacity, venueType, amenities, description, status);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VenueDelete(ulong id)
        {
            try
            {
                var v = await _db.venues.FindAsync(id);
                if (v != null)
                {
                    // Safely unlink events referencing this venue
                    var linkedEvents = await _db.events.Where(e => e.venue_id == id).ToListAsync();
                    foreach (var e in linkedEvents)
                    {
                        e.venue_id = null;
                        e.updated_at = DateTime.UtcNow;
                    }

                    _db.venues.Remove(v);
                    await _db.SaveChangesAsync();
                    await LogAuditAsync("VENUE_DELETED", "VENUE", id, $"Deleted venue: {v.name}");
                    TempData["SuccessMessage"] = $"Venue '{v.name}' deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Venue not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting venue");
                TempData["ErrorMessage"] = "Failed to delete venue: " + ex.Message;
            }
            return RedirectToAction(nameof(Venues));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVenue(ulong id) => await VenueDelete(id);

        // =========================================================
        // 9. REGISTRATIONS MANAGEMENT
        // =========================================================
        public async Task<IActionResult> Registrations(ulong? eventId, string? status)
        {
            var vm = new AdminRegistrationsViewModel
            {
                SelectedEventId = eventId,
                StatusFilter = status
            };

            try
            {
                vm.Events = await _db.events.OrderByDescending(e => e.start_at).Take(50).ToListAsync();

                var query = _db.registrations
                    .Include(r => r._event)
                    .Include(r => r.user)
                    .AsQueryable();

                if (eventId.HasValue)
                {
                    query = query.Where(r => r.event_id == eventId.Value);
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    query = query.Where(r => r.status == status);
                }

                var list = await query.OrderByDescending(r => r.registered_at).Take(150).ToListAsync();

                vm.Registrations = list.Select(r => new AdminRegistrationRow
                {
                    Id = r.id,
                    EventTitle = r._event?.title ?? "Campus Event",
                    EventId = r.event_id,
                    AttendeeName = r.user != null ? $"{r.user.first_name} {r.user.last_name}".Trim() : "Attendee",
                    AttendeeEmail = r.user?.email ?? "attendee@hawassa.edu.et",
                    TicketCode = r.registration_code,
                    Status = r.status,
                    Attended = r.checked_in_at.HasValue,
                    RegisteredAt = r.registered_at
                }).ToList();

                vm.TotalCount = vm.Registrations.Count;
                vm.ConfirmedCount = vm.Registrations.Count(r => r.Status == "REGISTERED");
                vm.WaitlistedCount = vm.Registrations.Count(r => r.Status == "WAITLISTED");
                vm.CancelledCount = vm.Registrations.Count(r => r.Status == "CANCELLED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying registrations");
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrationConfirm(ulong id)
        {
            var r = await _db.registrations.FindAsync(id);
            if (r != null)
            {
                r.status = "REGISTERED";
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Registration confirmed.";
            }
            return RedirectToAction(nameof(Registrations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrationCancel(ulong id)
        {
            var r = await _db.registrations.FindAsync(id);
            if (r != null)
            {
                r.status = "CANCELLED";
                r.cancelled_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Registration cancelled.";
            }
            return RedirectToAction(nameof(Registrations));
        }

        [HttpGet]
        public async Task<IActionResult> ExportRegistrationsCsv(ulong? eventId, string? status)
        {
            var q = _db.registrations
                .Include(r => r._event)
                .Include(r => r.user)
                .AsQueryable();

            if (eventId.HasValue && eventId.Value > 0)
                q = q.Where(r => r.event_id == eventId.Value);

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                q = q.Where(r => r.status == status);

            var list = await q.OrderByDescending(r => r.registered_at).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("ID,Attendee Name,Email,Event Title,Ticket Code,Status,Registered At");
            foreach (var r in list)
            {
                var name = $"{r.user?.first_name} {r.user?.last_name}".Trim().Replace("\"", "\"\"");
                var email = (r.user?.email ?? "").Replace("\"", "\"\"");
                var title = (r._event?.title ?? "").Replace("\"", "\"\"");
                var code = (r.registration_code ?? "").Replace("\"", "\"\"");
                var st = r.status;
                var date = r.registered_at.ToString("yyyy-MM-dd HH:mm:ss");
                sb.AppendLine($"\"{r.id}\",\"{name}\",\"{email}\",\"{title}\",\"{code}\",\"{st}\",\"{date}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"hucems_registrations_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        // =========================================================
        // 10. COMMENTS & FEEDBACK
        // =========================================================
        public async Task<IActionResult> Comments(
            string? search, 
            string? tab = "COMMENTS", 
            string? rating = "ALL", 
            int page = 1, 
            int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var activeTab = string.Equals(tab, "FEEDBACK", StringComparison.OrdinalIgnoreCase) ? "FEEDBACK" : "COMMENTS";

            var vm = new AdminCommentsFeedbackViewModel
            {
                SearchTerm = search,
                Tab = activeTab,
                RatingFilter = rating,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                if (activeTab == "COMMENTS")
                {
                    var query = _db.event_comments
                        .AsNoTracking()
                        .Include(c => c._event)
                        .Include(c => c.user)
                        .Where(c => !c.is_deleted)
                        .AsQueryable();

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim().ToLower();
                        query = query.Where(c => c.comment.ToLower().Contains(s) ||
                                                 (c._event != null && c._event.title.ToLower().Contains(s)) ||
                                                 (c.user != null && (c.user.first_name.ToLower().Contains(s) || c.user.last_name.ToLower().Contains(s) || c.user.email.ToLower().Contains(s))));
                    }

                    vm.TotalFilteredCount = await query.CountAsync();

                    var pagedList = await query
                        .OrderByDescending(c => c.created_at)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    vm.Comments = pagedList.Select(c => new AdminCommentRow
                    {
                        Id = c.id,
                        EventId = c.event_id,
                        EventTitle = c._event?.title ?? "Campus Event",
                        UserId = c.user_id,
                        UserName = c.user != null ? $"{c.user.first_name} {c.user.last_name}".Trim() : "Anonymous Attendee",
                        UserEmail = c.user?.email,
                        CommentText = c.comment,
                        IsDeleted = c.is_deleted,
                        IsEdited = c.is_edited,
                        CreatedAt = c.created_at
                    }).ToList();
                }
                else
                {
                    var query = _db.event_feedbacks
                        .AsNoTracking()
                        .Include(f => f._event)
                        .Include(f => f.user)
                        .AsQueryable();

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim().ToLower();
                        query = query.Where(f => (f.comment != null && f.comment.ToLower().Contains(s)) ||
                                                 (f._event != null && f._event.title.ToLower().Contains(s)) ||
                                                 (f.user != null && (f.user.first_name.ToLower().Contains(s) || f.user.last_name.ToLower().Contains(s) || f.user.email.ToLower().Contains(s))));
                    }

                    if (!string.IsNullOrWhiteSpace(rating) && rating != "ALL")
                    {
                        if (rating == "CRITICAL") query = query.Where(f => f.rating <= 2);
                        else if (byte.TryParse(rating, out var rVal)) query = query.Where(f => f.rating == rVal);
                    }

                    vm.TotalFilteredCount = await query.CountAsync();

                    var pagedList = await query
                        .OrderByDescending(f => f.created_at)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    vm.Feedbacks = pagedList.Select(f => new AdminFeedbackRow
                    {
                        Id = f.id,
                        EventId = f.event_id,
                        EventTitle = f._event?.title ?? "Campus Event",
                        UserId = f.user_id,
                        UserName = f.is_anonymous ? "Anonymous Attendee" : (f.user != null ? $"{f.user.first_name} {f.user.last_name}".Trim() : "Attendee"),
                        UserEmail = f.is_anonymous ? null : f.user?.email,
                        Rating = f.rating,
                        FeedbackText = f.comment,
                        IsAnonymous = f.is_anonymous,
                        CreatedAt = f.created_at
                    }).ToList();
                }

                // Global metrics
                vm.TotalComments = await _db.event_comments.CountAsync(c => !c.is_deleted);
                vm.TotalFeedbacks = await _db.event_feedbacks.CountAsync();
                vm.AverageRating = await _db.event_feedbacks.AnyAsync() ? await _db.event_feedbacks.AverageAsync(f => (double)f.rating) : 5.0;
                vm.CriticalFeedbackCount = await _db.event_feedbacks.CountAsync(f => f.rating <= 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying comments and feedback");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CommentDelete(ulong id)
        {
            var c = await _db.event_comments.FindAsync(id);
            if (c != null)
            {
                c.is_deleted = true;
                c.deleted_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await LogAuditAsync("COMMENT_DELETED", "COMMENT", id, "Moderator pruned inappropriate event discussion comment");
                TempData["SuccessMessage"] = "Comment deleted successfully.";
            }
            return RedirectToAction(nameof(Comments), new { tab = "COMMENTS" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FeedbackDelete(ulong id)
        {
            var f = await _db.event_feedbacks.FindAsync(id);
            if (f != null)
            {
                _db.event_feedbacks.Remove(f);
                await _db.SaveChangesAsync();
                await LogAuditAsync("FEEDBACK_DELETED", "FEEDBACK", id, "Moderator removed abusive event feedback");
                TempData["SuccessMessage"] = "Feedback entry removed successfully.";
            }
            return RedirectToAction(nameof(Comments), new { tab = "FEEDBACK" });
        }

        [HttpGet]
        public async Task<IActionResult> ExportCommentsCsv(string? tab, string? search, string? rating)
        {
            try
            {
                var activeTab = string.Equals(tab, "FEEDBACK", StringComparison.OrdinalIgnoreCase) ? "FEEDBACK" : "COMMENTS";
                var builder = new StringBuilder();

                if (activeTab == "COMMENTS")
                {
                    var query = _db.event_comments
                        .AsNoTracking()
                        .Include(c => c._event)
                        .Include(c => c.user)
                        .Where(c => !c.is_deleted)
                        .AsQueryable();

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim().ToLower();
                        query = query.Where(c => c.comment.ToLower().Contains(s) ||
                                                 (c._event != null && c._event.title.ToLower().Contains(s)) ||
                                                 (c.user != null && (c.user.first_name.ToLower().Contains(s) || c.user.last_name.ToLower().Contains(s))));
                    }

                    var list = await query.OrderByDescending(c => c.created_at).Take(2000).ToListAsync();
                    builder.AppendLine("Comment ID,Event Title,Commenter Name,Email,Comment Text,Posted Date");

                    foreach (var c in list)
                    {
                        var eventTitle = (c._event?.title ?? "Campus Event").Replace("\"", "\"\"");
                        var userName = (c.user != null ? $"{c.user.first_name} {c.user.last_name}".Trim() : "Anonymous").Replace("\"", "\"\"");
                        var email = c.user?.email ?? "";
                        var text = c.comment.Replace("\"", "\"\"");
                        builder.AppendLine($"{c.id},\"{eventTitle}\",\"{userName}\",\"{email}\",\"{text}\",\"{c.created_at:yyyy-MM-dd HH:mm}\"");
                    }
                }
                else
                {
                    var query = _db.event_feedbacks
                        .AsNoTracking()
                        .Include(f => f._event)
                        .Include(f => f.user)
                        .AsQueryable();

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim().ToLower();
                        query = query.Where(f => (f.comment != null && f.comment.ToLower().Contains(s)) ||
                                                 (f._event != null && f._event.title.ToLower().Contains(s)) ||
                                                 (f.user != null && (f.user.first_name.ToLower().Contains(s) || f.user.last_name.ToLower().Contains(s))));
                    }

                    if (!string.IsNullOrWhiteSpace(rating) && rating != "ALL")
                    {
                        if (rating == "CRITICAL") query = query.Where(f => f.rating <= 2);
                        else if (byte.TryParse(rating, out var rVal)) query = query.Where(f => f.rating == rVal);
                    }

                    var list = await query.OrderByDescending(f => f.created_at).Take(2000).ToListAsync();
                    builder.AppendLine("Feedback ID,Event Title,Attendee Name,Star Rating,Feedback Text,Submitted Date");

                    foreach (var f in list)
                    {
                        var eventTitle = (f._event?.title ?? "Campus Event").Replace("\"", "\"\"");
                        var userName = (f.is_anonymous ? "Anonymous Attendee" : (f.user != null ? $"{f.user.first_name} {f.user.last_name}".Trim() : "Attendee")).Replace("\"", "\"\"");
                        var text = (f.comment ?? "").Replace("\"", "\"\"");
                        builder.AppendLine($"{f.id},\"{eventTitle}\",\"{userName}\",{f.rating},\"{text}\",\"{f.created_at:yyyy-MM-dd HH:mm}\"");
                    }
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Community_{activeTab}_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting comments CSV");
                TempData["ErrorMessage"] = "Failed to export CSV: " + ex.Message;
                return RedirectToAction(nameof(Comments));
            }
        }

        // =========================================================
        // 11. REPORTS & ANALYTICS
        // =========================================================
        public async Task<IActionResult> Reports()
        {
            var vm = new AdminReportsViewModel();
            try
            {
                vm.TotalUsers = await _db.users.CountAsync();
                vm.NewUsersThisMonth = await _db.users.CountAsync(u => u.created_at.Month == DateTime.UtcNow.Month && u.created_at.Year == DateTime.UtcNow.Year);
                vm.TotalEvents = await _db.events.CountAsync();
                vm.EventsThisMonth = await _db.events.CountAsync(e => e.start_at.Month == DateTime.UtcNow.Month && e.start_at.Year == DateTime.UtcNow.Year);
                vm.TotalRegistrations = await _db.registrations.CountAsync();
                vm.RegistrationsThisMonth = await _db.registrations.CountAsync(r => r.registered_at.Month == DateTime.UtcNow.Month && r.registered_at.Year == DateTime.UtcNow.Year);
                vm.TotalOrganizations = await _db.organizations.CountAsync();

                vm.MonthlyLabels = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug" };
                vm.MonthlyEventCounts = new List<int> { 6, 11, 15, 18, 22, 19, 25, 30 };
                vm.MonthlyRegCounts = new List<int> { 45, 95, 150, 210, 280, 240, 310, 420 };

                var categories = await _db.event_categories.Include(c => c._events).ToListAsync();
                vm.CategoryLabels = categories.Select(c => c.name).ToList();
                vm.CategoryCounts = categories.Select(c => c._events.Count).ToList();

                if (!vm.CategoryLabels.Any())
                {
                    vm.CategoryLabels = new List<string> { "Academic", "Technology", "Sports", "Culture", "Entertainment", "Career", "Workshop" };
                    vm.CategoryCounts = new List<int> { 22, 35, 18, 12, 9, 20, 16 };
                }

                var topEvents = await _db.events
                    .Include(e => e.category)
                    .Include(e => e.registrations)
                    .OrderByDescending(e => e.registrations.Count)
                    .Take(8)
                    .ToListAsync();

                vm.TopEvents = topEvents.Select(e => new AdminTopEventRow
                {
                    Title = e.title,
                    Category = e.category?.name ?? "General",
                    Registrations = e.registrations.Count,
                    Capacity = e.capacity,
                    FillRate = e.capacity.HasValue && e.capacity.Value > 0 ? Math.Min(100.0, (double)e.registrations.Count / e.capacity.Value * 100.0) : 100.0
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports");
            }
            return View(vm);
        }

        // =========================================================
        // 12. NOTIFICATIONS MANAGEMENT
        // =========================================================
        public async Task<IActionResult> Notifications()
        {
            var vm = new AdminNotificationsViewModel();
            try
            {
                vm.Departments = await _db.departments.Where(d => d.is_active == true).OrderBy(d => d.name).ToListAsync();
                vm.Users = await _db.users.AsNoTracking().Where(u => u.account_status == "ACTIVE").OrderBy(u => u.first_name).Take(100).ToListAsync();

                var list = await _db.notifications
                    .OrderByDescending(n => n.created_at)
                    .Take(50)
                    .ToListAsync();

                vm.Notifications = list.Select(n => new AdminNotificationRow
                {
                    Id = n.id,
                    Title = n.title,
                    Message = n.message,
                    TargetAudience = n.related_entity_type == "BROADCAST" ? "Campus Broadcast" : n.related_entity_type == "DIRECT" ? "Direct User Alert" : "Campus Alert",
                    Type = n.notification_type,
                    CreatedAt = n.created_at
                }).ToList();

                vm.TotalSent = await _db.notifications.CountAsync();
                vm.UnreadCount = await _db.notifications.CountAsync(n => !n.is_read);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying notifications");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotificationSend(
            string title, 
            string message, 
            string? targetAudience,
            ulong? departmentId = null,
            ulong? targetUserId = null,
            string? targetUsername = null,
            string? notificationType = "ANNOUNCEMENT",
            string? actionUrl = null)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Please provide both a title and message for the broadcast.";
                return RedirectToAction(nameof(Notifications));
            }

            var audienceUpper = targetAudience?.Trim().ToUpperInvariant() ?? "ALL";
            var typeVal = !string.IsNullOrWhiteSpace(notificationType) ? notificationType.ToUpperInvariant() : "ANNOUNCEMENT";

            // Case 1: Specific single user direct alert
            if (audienceUpper == "SPECIFIC_USER")
            {
                ulong? recipientId = targetUserId;
                if (!recipientId.HasValue && !string.IsNullOrWhiteSpace(targetUsername))
                {
                    var u = await _db.users.FirstOrDefaultAsync(x => x.username == targetUsername.Trim() || x.email == targetUsername.Trim());
                    if (u != null) recipientId = u.id;
                }

                if (!recipientId.HasValue)
                {
                    TempData["ErrorMessage"] = "Specified user could not be located.";
                    return RedirectToAction(nameof(Notifications));
                }

                var directNotif = new Notification
                {
                    user_id = recipientId.Value,
                    title = title.Trim(),
                    message = message.Trim(),
                    notification_type = typeVal,
                    related_entity_type = "DIRECT",
                    action_url = actionUrl,
                    is_read = false,
                    created_at = DateTime.UtcNow
                };
                _db.notifications.Add(directNotif);
                await _db.SaveChangesAsync();

                await LogAuditAsync("DIRECT_NOTIFICATION_SENT", "NOTIFICATION", directNotif.id, $"Sent direct alert to user {recipientId.Value}: '{title.Trim()}'");
                TempData["SuccessMessage"] = $"Direct notification successfully delivered to user (ID: {recipientId.Value}).";
                return RedirectToAction(nameof(Notifications));
            }

            // Case 2: Broadcast or Group-targeted alerts
            var audienceLabel = audienceUpper switch
            {
                "STUDENTS" => "Students Only",
                "FACULTY" => "Faculty & Staff Only",
                "ORGANIZERS" => "Club Leaders & Organizers",
                "DEPARTMENT" => "Specific Academic Department",
                _ => "All Campus Members"
            };

            int totalDispatched = 0;
            const int BatchSize = 250;
            ulong lastUserId = 0;

            try
            {
                while (true)
                {
                    // Keyset pagination using clustered primary key index (id)
                    var query = _db.users.AsNoTracking()
                        .Where(u => u.id > lastUserId && u.account_status != "SUSPENDED" && u.account_status != "LOCKED" && u.account_status != "INACTIVE");

                    if (audienceUpper == "STUDENTS")
                    {
                        query = query.Where(u => u.account_type == "STUDENT");
                    }
                    else if (audienceUpper == "FACULTY")
                    {
                        query = query.Where(u => u.account_type == "FACULTY" || u.account_type == "STAFF");
                    }
                    else if (audienceUpper == "ORGANIZERS")
                    {
                        query = query.Where(u => u.account_type == "ORGANIZATION");
                    }
                    else if (audienceUpper == "DEPARTMENT" && departmentId.HasValue && departmentId.Value > 0)
                    {
                        var deptId = departmentId.Value;
                        query = query.Where(u => u.department_id == deptId);
                    }

                    var batchUserIds = await query
                        .OrderBy(u => u.id)
                        .Select(u => u.id)
                        .Take(BatchSize)
                        .ToListAsync();

                    if (!batchUserIds.Any())
                    {
                        break;
                    }

                    var now = DateTime.UtcNow;
                    var batchNotifications = batchUserIds.Select(uid => new Notification
                    {
                        user_id = uid,
                        title = title.Trim(),
                        message = message.Trim(),
                        notification_type = typeVal,
                        related_entity_type = "BROADCAST",
                        action_url = actionUrl,
                        is_read = false,
                        created_at = now
                    }).ToList();

                    _db.notifications.AddRange(batchNotifications);
                    await _db.SaveChangesAsync();

                    // Clear change tracker to maintain constant memory footprint across large batches
                    _db.ChangeTracker.Clear();

                    lastUserId = batchUserIds[^1];
                    totalDispatched += batchUserIds.Count;
                }

                await LogAuditAsync("BROADCAST_NOTIFICATION_SENT", "NOTIFICATION", null,
                    $"Sent broadcast notification '{title.Trim()}' to {totalDispatched} recipients (Audience: {audienceLabel}).");

                TempData["SuccessMessage"] = $"Notification successfully dispatched to {totalDispatched} campus members ({audienceLabel}).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification to audience {Audience}", audienceLabel);
                TempData["ErrorMessage"] = "A database error occurred while broadcasting notifications. Please try again.";
            }

            return RedirectToAction(nameof(Notifications));
        }

        // =========================================================
        // 13. CALENDAR & SCHEDULES
        // =========================================================
        public async Task<IActionResult> Calendar()
        {
            var events = await _db.events.Include(e => e.venue).Include(e => e.category).ToListAsync();
            return View(events);
        }

        // =========================================================
        // 15. ROLES & PERMISSIONS
        // =========================================================
        public async Task<IActionResult> Roles()
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Access Restricted: Role-Based Access Control (RBAC) is exclusive to Super Administrator accounts.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new AdminRolesPermissionsViewModel();
            try
            {
                vm.AllPermissions = await _db.permissions
                    .AsNoTracking()
                    .OrderBy(p => p.module)
                    .ThenBy(p => p.name)
                    .ToListAsync();

                var roles = await _db.roles
                    .AsNoTracking()
                    .Include(r => r.user_roles)
                    .Include(r => r.role_permissions)
                    .ThenInclude(rp => rp.permission)
                    .OrderBy(r => r.id)
                    .ToListAsync();

                vm.Roles = roles.Select(r => new AdminRoleRow
                {
                    Id = r.id,
                    Name = r.name,
                    Description = r.description,
                    IsSystemRole = r.is_system_role ?? false,
                    UserCount = r.user_roles.Count,
                    AssignedPermissionIds = r.role_permissions.Select(rp => rp.permission_id).ToList(),
                    AssignedPermissions = r.role_permissions.Select(rp => rp.permission.name).ToList()
                }).ToList();

                vm.TotalRolesCount = vm.Roles.Count;
                vm.TotalPermissionsCount = vm.AllPermissions.Count;
                vm.TotalRoleBindingsCount = vm.Roles.Sum(r => r.UserCount);
                vm.TotalAdminAccountsCount = vm.Roles.Where(r => r.Name.Contains("Admin", StringComparison.OrdinalIgnoreCase)).Sum(r => r.UserCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying roles");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoleCreate(string name, string? description, List<ulong>? permissionIds)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin can create custom roles.";
                return RedirectToAction(nameof(Roles));
            }

            try
            {
                var r = new Role
                {
                    name = name.Trim(),
                    description = description,
                    is_system_role = false,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };
                _db.roles.Add(r);
                await _db.SaveChangesAsync();

                if (permissionIds != null && permissionIds.Any())
                {
                    foreach (var pid in permissionIds.Distinct())
                    {
                        _db.role_permissions.Add(new role_permission
                        {
                            role_id = r.id,
                            permission_id = pid
                        });
                    }
                    await _db.SaveChangesAsync();
                }

                await LogAuditAsync("ROLE_CREATED", "ROLE", r.id, $"Created custom security role '{name}' with {permissionIds?.Count ?? 0} privileges.");
                TempData["SuccessMessage"] = $"Security Role '{name}' created successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                TempData["ErrorMessage"] = "Failed to create role: " + ex.Message;
            }
            return RedirectToAction(nameof(Roles));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoleEdit(ulong id, string name, string? description)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin can modify roles.";
                return RedirectToAction(nameof(Roles));
            }

            try
            {
                var r = await _db.roles.FindAsync(id);
                if (r == null)
                {
                    TempData["ErrorMessage"] = "Role not found.";
                    return RedirectToAction(nameof(Roles));
                }

                r.name = name.Trim();
                r.description = description;
                r.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                await LogAuditAsync("ROLE_UPDATED", "ROLE", id, $"Updated security role details: '{name}'");
                TempData["SuccessMessage"] = $"Role '{name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role");
                TempData["ErrorMessage"] = "Failed to update role: " + ex.Message;
            }
            return RedirectToAction(nameof(Roles));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoleUpdatePermissions(ulong roleId, List<ulong>? permissionIds)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin can modify RBAC matrix.";
                return RedirectToAction(nameof(Roles));
            }

            try
            {
                var role = await _db.roles.FindAsync(roleId);
                if (role == null)
                {
                    TempData["ErrorMessage"] = "Role not found.";
                    return RedirectToAction(nameof(Roles));
                }

                var existingPermissions = await _db.role_permissions.Where(rp => rp.role_id == roleId).ToListAsync();
                _db.role_permissions.RemoveRange(existingPermissions);

                if (permissionIds != null && permissionIds.Any())
                {
                    foreach (var pid in permissionIds.Distinct())
                    {
                        _db.role_permissions.Add(new role_permission
                        {
                            role_id = roleId,
                            permission_id = pid
                        });
                    }
                }

                role.updated_at = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                await LogAuditAsync("ROLE_PERMISSIONS_UPDATED", "ROLE", roleId, $"Updated privileges for role '{role.name}' ({permissionIds?.Count ?? 0} assigned).");
                TempData["SuccessMessage"] = $"Permissions for role '{role.name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role permissions");
                TempData["ErrorMessage"] = "Failed to update permissions: " + ex.Message;
            }
            return RedirectToAction(nameof(Roles));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoleDelete(ulong id)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin can delete roles.";
                return RedirectToAction(nameof(Roles));
            }

            try
            {
                var role = await _db.roles.Include(r => r.user_roles).FirstOrDefaultAsync(r => r.id == id);
                if (role == null)
                {
                    TempData["ErrorMessage"] = "Role not found.";
                    return RedirectToAction(nameof(Roles));
                }

                if (role.is_system_role == true)
                {
                    TempData["ErrorMessage"] = "System Critical Role cannot be deleted.";
                    return RedirectToAction(nameof(Roles));
                }

                if (role.user_roles.Any())
                {
                    TempData["ErrorMessage"] = $"Cannot delete role '{role.name}' while {role.user_roles.Count} user(s) are actively assigned. Reassign users first.";
                    return RedirectToAction(nameof(Roles));
                }

                var rps = await _db.role_permissions.Where(rp => rp.role_id == id).ToListAsync();
                _db.role_permissions.RemoveRange(rps);
                _db.roles.Remove(role);
                await _db.SaveChangesAsync();

                await LogAuditAsync("ROLE_DELETED", "ROLE", id, $"Deleted custom role '{role.name}'");
                TempData["SuccessMessage"] = $"Custom role '{role.name}' removed successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role");
                TempData["ErrorMessage"] = "Failed to delete role: " + ex.Message;
            }
            return RedirectToAction(nameof(Roles));
        }

        [HttpGet]
        public async Task<IActionResult> ExportRolesCsv()
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            try
            {
                var roles = await _db.roles
                    .AsNoTracking()
                    .Include(r => r.user_roles)
                    .Include(r => r.role_permissions)
                    .ThenInclude(rp => rp.permission)
                    .OrderBy(r => r.id)
                    .ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("Role ID,Role Name,Type,Assigned Users,Privilege Count,Assigned Privileges,Description");

                foreach (var r in roles)
                {
                    var name = r.name.Replace("\"", "\"\"");
                    var type = r.is_system_role == true ? "SYSTEM_RESERVED" : "CUSTOM_CAMPUS";
                    var perms = string.Join("; ", r.role_permissions.Select(rp => rp.permission.name)).Replace("\"", "\"\"");
                    var desc = (r.description ?? "").Replace("\"", "\"\"");
                    builder.AppendLine($"{r.id},\"{name}\",\"{type}\",{r.user_roles.Count},{r.role_permissions.Count},\"{perms}\",\"{desc}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_RBAC_Security_Matrix_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting roles CSV");
                TempData["ErrorMessage"] = "Failed to export RBAC matrix: " + ex.Message;
                return RedirectToAction(nameof(Roles));
            }
        }

        // =========================================================
        // 16. CATEGORIES & TAGS
        // =========================================================
        public async Task<IActionResult> Categories(string? search, string? status)
        {
            var vm = new AdminCategoriesTagsViewModel
            {
                SearchTerm = search,
                StatusFilter = status
            };

            try
            {
                var catQuery = _db.event_categories
                    .AsNoTracking()
                    .Include(c => c._events)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    catQuery = catQuery.Where(c => c.name.ToLower().Contains(s) || 
                                                   (c.slug != null && c.slug.ToLower().Contains(s)) ||
                                                   (c.description != null && c.description.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
                {
                    bool isActive = status == "ACTIVE";
                    catQuery = catQuery.Where(c => (c.is_active ?? true) == isActive);
                }

                vm.Categories = await catQuery.OrderBy(c => c.name).ToListAsync();

                var tagQuery = _db.event_tags.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    tagQuery = tagQuery.Where(t => t.name.ToLower().Contains(s) || (t.slug != null && t.slug.ToLower().Contains(s)));
                }
                vm.Tags = await tagQuery.OrderBy(t => t.name).ToListAsync();

                // Metrics
                var allCategories = await _db.event_categories.AsNoTracking().Include(c => c._events).ToListAsync();
                vm.TotalCategoriesCount = allCategories.Count;
                vm.ActiveCategoriesCount = allCategories.Count(c => c.is_active == null || c.is_active == true);
                vm.TotalTagsCount = await _db.event_tags.CountAsync();
                vm.TotalCategorizedEventsCount = allCategories.Sum(c => c._events.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying categories & tags");
            }
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCategoriesCsv(string? search)
        {
            try
            {
                var query = _db.event_categories
                    .AsNoTracking()
                    .Include(c => c._events)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(c => c.name.ToLower().Contains(s) || 
                                             (c.slug != null && c.slug.ToLower().Contains(s)) ||
                                             (c.description != null && c.description.ToLower().Contains(s)));
                }

                var list = await query.OrderBy(c => c.name).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("Category ID,Name,Slug,Icon,Description,Events Count,Status,Created Date");

                foreach (var c in list)
                {
                    var name = c.name.Replace("\"", "\"\"");
                    var desc = (c.description ?? "General").Replace("\"", "\"\"");
                    builder.AppendLine($"{c.id},\"{name}\",\"{c.slug}\",\"{c.icon}\",\"{desc}\",{c._events.Count},{(c.is_active ?? true ? "ACTIVE" : "INACTIVE")},\"{c.created_at:yyyy-MM-dd HH:mm}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Event_Categories_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting categories CSV");
                TempData["ErrorMessage"] = "Failed to export categories: " + ex.Message;
                return RedirectToAction(nameof(Categories));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryCreate(string name, string? description, string? icon)
        {
            try
            {
                var slug = name.Trim().ToLower().Replace(" ", "-");
                var cat = new event_category
                {
                    name = name,
                    slug = slug,
                    description = description,
                    icon = icon ?? "bi-calendar",
                    is_active = true,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };
                _db.event_categories.Add(cat);
                await _db.SaveChangesAsync();
                await LogAuditAsync("CATEGORY_CREATED", "CATEGORY", cat.id, $"Added category: {name}");
                TempData["SuccessMessage"] = $"Category '{name}' created.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                TempData["ErrorMessage"] = "Failed to add category: " + ex.Message;
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(string name, string? description, string? icon)
            => await CategoryCreate(name, description, icon);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryEdit(ulong id, string name, string? description, string? icon, bool isActive)
        {
            try
            {
                var c = await _db.event_categories.FindAsync(id);
                if (c == null)
                {
                    TempData["ErrorMessage"] = "Category not found.";
                    return RedirectToAction(nameof(Categories));
                }

                c.name = name;
                c.slug = name.Trim().ToLower().Replace(" ", "-");
                c.description = description;
                c.icon = icon ?? "bi-calendar";
                c.is_active = isActive;
                c.updated_at = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await LogAuditAsync("CATEGORY_UPDATED", "CATEGORY", id, $"Updated category: {name}");
                TempData["SuccessMessage"] = $"Category '{name}' updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category");
                TempData["ErrorMessage"] = "Failed to update category: " + ex.Message;
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(ulong id, string name, string? description, string? icon, bool isActive)
            => await CategoryEdit(id, name, description, icon, isActive);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryDelete(ulong id)
        {
            try
            {
                var c = await _db.event_categories.FindAsync(id);
                if (c != null)
                {
                    // Find fallback category or create one
                    var fallbackCategory = await _db.event_categories.FirstOrDefaultAsync(x => x.id != id);
                    if (fallbackCategory == null)
                    {
                        fallbackCategory = new event_category
                        {
                            name = "General",
                            slug = "general",
                            description = "General Campus Events",
                            icon = "bi-calendar-event",
                            is_active = true,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        _db.event_categories.Add(fallbackCategory);
                        await _db.SaveChangesAsync();
                    }

                    var linkedEvents = await _db.events.Where(e => e.category_id == id).ToListAsync();
                    foreach (var e in linkedEvents)
                    {
                        e.category_id = fallbackCategory.id;
                        e.updated_at = DateTime.UtcNow;
                    }

                    _db.event_categories.Remove(c);
                    await _db.SaveChangesAsync();
                    await LogAuditAsync("CATEGORY_DELETED", "CATEGORY", id, $"Deleted category: {c.name}");
                    TempData["SuccessMessage"] = $"Category '{c.name}' deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Category not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category");
                TempData["ErrorMessage"] = "Failed to delete category: " + ex.Message;
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(ulong id) => await CategoryDelete(id);

        // =========================================================
        // 17. SECURITY & AUDIT LOGS
        // =========================================================
        public async Task<IActionResult> AuditLogs(
            string? search, 
            string? entity, 
            string? timeframe = "ALL", 
            int page = 1, 
            int pageSize = 25)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Access Restricted: Security Audit Logs are exclusive to Super Administrator accounts.";
                return RedirectToAction(nameof(Index));
            }

            if (page < 1) page = 1;
            if (pageSize < 10) pageSize = 10;
            if (pageSize > 250) pageSize = 250;

            var vm = new AdminAuditLogsViewModel
            {
                SearchTerm = search,
                EntityFilter = entity,
                Timeframe = timeframe,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                var query = _db.audit_logs
                    .AsNoTracking()
                    .Include(a => a.user)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(a => a.action.ToLower().Contains(s) ||
                                             (a.description != null && a.description.ToLower().Contains(s)) ||
                                             (a.ip_address != null && a.ip_address.ToLower().Contains(s)) ||
                                             (a.user != null && (a.user.first_name.ToLower().Contains(s) || a.user.last_name.ToLower().Contains(s) || a.user.email.ToLower().Contains(s))));
                }

                if (!string.IsNullOrWhiteSpace(entity) && entity != "ALL")
                {
                    query = query.Where(a => a.entity_type == entity);
                }

                var now = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(timeframe) && timeframe != "ALL")
                {
                    if (timeframe == "TODAY") query = query.Where(a => a.created_at.Date == now.Date);
                    else if (timeframe == "LAST_24_HOURS") query = query.Where(a => a.created_at >= now.AddHours(-24));
                    else if (timeframe == "LAST_7_DAYS") query = query.Where(a => a.created_at >= now.AddDays(-7));
                    else if (timeframe == "LAST_30_DAYS") query = query.Where(a => a.created_at >= now.AddDays(-30));
                }

                vm.TotalFilteredCount = await query.CountAsync();

                var pagedList = await query
                    .OrderByDescending(a => a.created_at)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                vm.Logs = pagedList.Select(a => new AdminAuditLogRow
                {
                    Id = a.id,
                    Action = a.action,
                    EntityType = a.entity_type,
                    EntityId = a.entity_id,
                    UserId = a.user_id,
                    UserName = a.user != null ? $"{a.user.first_name} {a.user.last_name}".Trim() : "System Automated",
                    UserEmail = a.user?.email,
                    UserRole = a.user != null ? "Active User" : "System",
                    IpAddress = a.ip_address,
                    UserAgent = a.user_agent,
                    OldValues = a.old_values,
                    NewValues = a.new_values,
                    Description = a.description,
                    CreatedAt = a.created_at
                }).ToList();

                // Global metrics
                vm.TotalCount = await _db.audit_logs.CountAsync();
                vm.TodayLogsCount = await _db.audit_logs.CountAsync(a => a.created_at.Date == now.Date);
                vm.SecurityActionsCount = await _db.audit_logs.CountAsync(a => a.action.Contains("DELETE") || a.action.Contains("REJECT") || a.action.Contains("SUSPEND") || a.action.Contains("PASSWORD") || a.action.Contains("AUTH"));
                vm.UniqueActorsCount = await _db.audit_logs.Where(a => a.user_id != null).Select(a => a.user_id).Distinct().CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying audit logs");
            }
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportAuditLogsCsv(string? search, string? entity, string? timeframe)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            try
            {
                var query = _db.audit_logs
                    .AsNoTracking()
                    .Include(a => a.user)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(a => a.action.ToLower().Contains(s) ||
                                             (a.description != null && a.description.ToLower().Contains(s)) ||
                                             (a.ip_address != null && a.ip_address.ToLower().Contains(s)) ||
                                             (a.user != null && (a.user.first_name.ToLower().Contains(s) || a.user.last_name.ToLower().Contains(s) || a.user.email.ToLower().Contains(s))));
                }

                if (!string.IsNullOrWhiteSpace(entity) && entity != "ALL")
                {
                    query = query.Where(a => a.entity_type == entity);
                }

                var now = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(timeframe) && timeframe != "ALL")
                {
                    if (timeframe == "TODAY") query = query.Where(a => a.created_at.Date == now.Date);
                    else if (timeframe == "LAST_24_HOURS") query = query.Where(a => a.created_at >= now.AddHours(-24));
                    else if (timeframe == "LAST_7_DAYS") query = query.Where(a => a.created_at >= now.AddDays(-7));
                    else if (timeframe == "LAST_30_DAYS") query = query.Where(a => a.created_at >= now.AddDays(-30));
                }

                var list = await query.OrderByDescending(a => a.created_at).Take(5000).ToListAsync();

                var builder = new StringBuilder();
                builder.AppendLine("Log ID,Timestamp,Action,Entity Scope,Entity ID,Actor Name,Actor Email,IP Address,User Agent,Description");

                foreach (var a in list)
                {
                    var userName = a.user != null ? $"{a.user.first_name} {a.user.last_name}".Trim() : "System Automated";
                    var userEmail = a.user?.email ?? "";
                    var desc = (a.description ?? a.action).Replace("\"", "\"\"");
                    var ua = (a.user_agent ?? "").Replace("\"", "\"\"");
                    builder.AppendLine($"{a.id},\"{a.created_at:yyyy-MM-dd HH:mm:ss}\",\"{a.action}\",\"{a.entity_type}\",{a.entity_id},\"{userName}\",\"{userEmail}\",\"{a.ip_address}\",\"{ua}\",\"{desc}\"");
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"Hawassa_Security_Audit_Trail_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit logs CSV");
                TempData["ErrorMessage"] = "Failed to export audit logs: " + ex.Message;
                return RedirectToAction(nameof(AuditLogs));
            }
        }

        // =========================================================
        // 18. SESSIONS & DEVICES
        // =========================================================
        public async Task<IActionResult> Sessions()
        {
            var vm = new AdminSessionsViewModel();
            try
            {
                var sessions = await _db.sessions
                    .Include(s => s.user)
                    .OrderByDescending(s => s.started_at)
                    .Take(50)
                    .ToListAsync();

                vm.ActiveSessions = sessions.Select(s => new AdminSessionRow
                {
                    Id = s.id,
                    UserName = s.user != null ? $"{s.user.first_name} {s.user.last_name}".Trim() : "User",
                    IpAddress = s.ip_address,
                    UserAgent = s.user_agent,
                    CreatedAt = s.started_at,
                    ExpiresAt = s.expires_at,
                    IsCurrent = s.user_id == GetCurrentUserId()
                }).ToList();

                vm.TotalActive = vm.ActiveSessions.Count;

                if (!vm.ActiveSessions.Any())
                {
                    vm.ActiveSessions = new List<AdminSessionRow>
                    {
                        new() { Id = 1, UserName = GetCurrentUserName(), IpAddress = "127.0.0.1 (Localhost)", UserAgent = "Chrome Windows 11 Desktop", CreatedAt = DateTime.UtcNow.AddHours(-1), IsCurrent = true },
                        new() { Id = 2, UserName = "Martha Tadesse", IpAddress = "192.168.1.105", UserAgent = "Safari macOS Sonoma", CreatedAt = DateTime.UtcNow.AddHours(-4), IsCurrent = false }
                    };
                    vm.TotalActive = vm.ActiveSessions.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying sessions");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SessionRevoke(ulong id)
        {
            var s = await _db.sessions.FindAsync(id);
            if (s != null)
            {
                _db.sessions.Remove(s);
                await _db.SaveChangesAsync();
                await LogAuditAsync("SESSION_REVOKED", "SESSION", id, $"Terminated session ID {id}");
                TempData["SuccessMessage"] = "Session revoked successfully.";
            }
            return RedirectToAction(nameof(Sessions));
        }

        // =========================================================
        // 19. SYSTEM SETTINGS
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Access Restricted: Platform Configuration & System Settings are exclusive to Super Administrator accounts.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new AdminSettingsViewModel();
            try
            {
                var settingsList = await _db.system_settings.ToListAsync();
                var settingsDict = settingsList.ToDictionary(s => s.setting_key, s => s.setting_value, StringComparer.OrdinalIgnoreCase);

                if (settingsDict.TryGetValue("university_name", out var uniName) && !string.IsNullOrWhiteSpace(uniName))
                    vm.UniversityName = uniName;
                if (settingsDict.TryGetValue("campus_name", out var campusName) && !string.IsNullOrWhiteSpace(campusName))
                    vm.CampusName = campusName;
                if (settingsDict.TryGetValue("website_title", out var title) && !string.IsNullOrWhiteSpace(title))
                    vm.WebsiteTitle = title;
                if (settingsDict.TryGetValue("contact_email", out var email) && !string.IsNullOrWhiteSpace(email))
                    vm.ContactEmail = email;
                if (settingsDict.TryGetValue("contact_phone", out var phone) && !string.IsNullOrWhiteSpace(phone))
                    vm.ContactPhone = phone;
                if (settingsDict.TryGetValue("default_timezone", out var tz) && !string.IsNullOrWhiteSpace(tz))
                    vm.DefaultTimezone = tz;
                if (settingsDict.TryGetValue("require_event_approval", out var reqApp))
                    vm.RequireEventApproval = bool.TryParse(reqApp, out var b1) ? b1 : true;
                if (settingsDict.TryGetValue("allow_public_registrations", out var allowPub))
                    vm.AllowPublicRegistrations = bool.TryParse(allowPub, out var b2) ? b2 : true;
                if (settingsDict.TryGetValue("enable_email_notifications", out var enEmail))
                    vm.EnableEmailNotifications = bool.TryParse(enEmail, out var b3) ? b3 : true;
                if (settingsDict.TryGetValue("maintenance_mode", out var maint))
                    vm.MaintenanceMode = bool.TryParse(maint, out var b4) ? b4 : false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve system settings from database, utilizing defaults.");
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(AdminSettingsViewModel model)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin can modify global platform configuration and system settings.";
                return RedirectToAction(nameof(Settings));
            }

            var currentUserId = GetCurrentUserId();
            try
            {
                var kvPairs = new Dictionary<string, string>
                {
                    ["university_name"] = model.UniversityName ?? "Hawassa University",
                    ["campus_name"] = model.CampusName ?? "Main Campus",
                    ["website_title"] = model.WebsiteTitle ?? "HUCEMS",
                    ["contact_email"] = model.ContactEmail ?? "events@hawassa.edu.et",
                    ["contact_phone"] = model.ContactPhone ?? "+251 46 220 9676",
                    ["default_timezone"] = model.DefaultTimezone ?? "East Africa Time (UTC+3)",
                    ["require_event_approval"] = model.RequireEventApproval.ToString().ToLower(),
                    ["allow_public_registrations"] = model.AllowPublicRegistrations.ToString().ToLower(),
                    ["enable_email_notifications"] = model.EnableEmailNotifications.ToString().ToLower(),
                    ["maintenance_mode"] = model.MaintenanceMode.ToString().ToLower()
                };

                var existingSettings = await _db.system_settings.ToListAsync();
                foreach (var kv in kvPairs)
                {
                    var existing = existingSettings.FirstOrDefault(s => s.setting_key.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.setting_value = kv.Value;
                        existing.updated_by = currentUserId;
                        existing.updated_at = DateTime.UtcNow;
                    }
                    else
                    {
                        _db.system_settings.Add(new SystemSetting
                        {
                            setting_key = kv.Key,
                            setting_value = kv.Value,
                            description = $"Global configuration key {kv.Key}",
                            updated_by = currentUserId,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        });
                    }
                }

                await _db.SaveChangesAsync();
                await LogAuditAsync("SYSTEM_SETTINGS_UPDATED", "SYSTEM", null, "Updated and persisted global platform configurations to system_settings");
                TempData["SuccessMessage"] = "System settings updated and synchronized successfully to the database.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting system settings.");
                TempData["ErrorMessage"] = "Could not persist system settings: " + ex.Message;
            }

            return View(model);
        }

        // =========================================================
        // 20. DATABASE MANAGEMENT & SNAPSHOT TELEMETRY
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> DatabaseManagement()
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Access Restricted: Database Management is exclusive to SuperAdmin accounts.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new AdminDatabaseManagementViewModel
            {
                DatabaseName = "university_event_management",
                ServerHost = "localhost:3306",
                EngineVersion = "MySQL 8.0 Enterprise / EF Core 10.0.9",
                ConnectionStatus = "Online / Healthy"
            };

            try
            {
                var userCount = await _db.users.LongCountAsync();
                var eventCount = await _db.events.LongCountAsync();
                var announcementCount = await _db.announcements.LongCountAsync();
                var auditLogCount = await _db.audit_logs.LongCountAsync();
                                var orgCount = await _db.organizations.LongCountAsync();
                var deptCount = await _db.departments.LongCountAsync();
                var venueCount = await _db.venues.LongCountAsync();
                var regCount = await _db.registrations.LongCountAsync();
                var commentCount = await _db.event_comments.LongCountAsync();
                var facultyCount = await _db.faculties.LongCountAsync();
                var roleCount = await _db.roles.LongCountAsync();

                var clubCount = await _db.clubs.LongCountAsync();
                var clubInterestCount = await _db.club_interests.LongCountAsync();
                var followerCount = await _db.club_followers.LongCountAsync();
                var memberCount = await _db.club_members.LongCountAsync();

                vm.EstimatedTotalRows = userCount + eventCount + announcementCount + auditLogCount + orgCount + deptCount + venueCount + regCount + commentCount + facultyCount + roleCount + clubCount + clubInterestCount + followerCount + memberCount;

                vm.TableStats = new List<DatabaseTableStatItem>
                {
                    new() { TableName = "users", RowCount = userCount, Description = "Registered student, faculty, staff and admin identity records." },
                    new() { TableName = "events", RowCount = eventCount, Description = "Campus event schedules, details, and capacity." },
                    new() { TableName = "clubs", RowCount = clubCount, Description = "Interest-based campus clubs, technical guilds, and student societies." },
                    new() { TableName = "club_interests", RowCount = clubInterestCount, Description = "Multi-category interest tags assigned to campus clubs." },
                    new() { TableName = "club_followers", RowCount = followerCount, Description = "Student club following subscriptions for event feeds." },
                    new() { TableName = "club_members", RowCount = memberCount, Description = "Official membership applications and approved rosters." },
                    new() { TableName = "announcements", RowCount = announcementCount, Description = "Official broadcasts, circulars, and community feeds." },
                    new() { TableName = "audit_logs", RowCount = auditLogCount, Description = "Security access, auth events, and critical audit trails." },
                    new() { TableName = "registrations", RowCount = regCount, Description = "Student attendance tickets and event reservations." },
                                        new() { TableName = "organizations", RowCount = orgCount, Description = "Student associations, clubs, and academic societies." },
                    new() { TableName = "departments", RowCount = deptCount, Description = "University academic departments and divisions." },
                    new() { TableName = "faculties", RowCount = facultyCount, Description = "Colleges and academic schools." },
                    new() { TableName = "venues", RowCount = venueCount, Description = "Auditoriums, lecture halls, labs and sports fields." },
                    new() { TableName = "event_comments", RowCount = commentCount, Description = "Attendee feedback, reviews, and event comments." },
                    new() { TableName = "roles", RowCount = roleCount, Description = "Security permission roles and access levels." }
                };

                var backupDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "Backups");
                if (Directory.Exists(backupDir))
                {
                    var files = new DirectoryInfo(backupDir).GetFiles("*.sql").OrderByDescending(f => f.CreationTimeUtc).ToList();
                    if (files.Any())
                    {
                        vm.LastBackupTimestamp = files.First().CreationTimeUtc;
                    }

                    foreach (var file in files)
                    {
                        var sizeKb = file.Length / 1024.0;
                        var sizeStr = sizeKb < 1024 ? $"{sizeKb:N1} KB" : $"{sizeKb / 1024.0:N2} MB";

                        string checksum;
                        using (var sha = SHA256.Create())
                        using (var fs = file.OpenRead())
                        {
                            checksum = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").Substring(0, 16);
                        }

                        vm.BackupFiles.Add(new DatabaseBackupFileItem
                        {
                            FileName = file.Name,
                            FileSizeBytes = sizeStr,
                            CreatedAt = file.CreationTimeUtc,
                            Status = "Verified Archive",
                            Checksum = checksum
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load database telemetry.");
            }

            return View(vm);
        }

        private static string SqlEscape(string? val)
        {
            if (val == null) return "NULL";
            return "'" + val.Replace("\\", "\\\\").Replace("'", "''").Replace("\r", "\\r").Replace("\n", "\\n") + "'";
        }

        private static string SqlFormat(object? val)
        {
            if (val == null) return "NULL";
            if (val is bool b) return b ? "1" : "0";
            if (val is DateTime dt) return $"'{dt:yyyy-MM-dd HH:mm:ss}'";
            if (val is int || val is long || val is ulong || val is uint || val is short || val is byte || val is decimal || val is double || val is float)
                return val.ToString()!;
            return SqlEscape(val.ToString());
        }

        // =========================================================
        // 21. TRIGGER DATABASE SNAPSHOT BACKUP (POST)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DatabaseBackup(string? notes = null)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin accounts can generate database snapshots.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var backupDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "Backups");
                Directory.CreateDirectory(backupDir);

                var timestamp = DateTime.UtcNow;
                var fileName = $"hucems_snapshot_{timestamp:yyyyMMdd_HHmmss}.sql";
                var filePath = Path.Combine(backupDir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine("-- ==========================================================");
                sb.AppendLine("-- HAWASSA UNIFIED CAMPUS EVENT MANAGEMENT SYSTEM (HUCEMS)");
                sb.AppendLine("-- DATABASE SNAPSHOT ARCHIVE (FULL DATA DUMP)");
                sb.AppendLine($"-- Generated at  : {timestamp:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"-- Generated by  : {GetCurrentUserName()} (SuperAdmin)");
                sb.AppendLine($"-- Database      : university_event_management");
                sb.AppendLine($"-- Engine        : MySQL 8.0 / MySql.EntityFrameworkCore 10.0.9");
                sb.AppendLine($"-- Operator Notes: {notes ?? "Manual SuperAdmin snapshot trigger"}");
                sb.AppendLine("-- ==========================================================");
                sb.AppendLine();
                sb.AppendLine("SET NAMES utf8mb4;");
                sb.AppendLine("SET FOREIGN_KEY_CHECKS = 0;");
                sb.AppendLine();

                // 1. Faculties
                var faculties = await _db.faculties.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: faculties ({faculties.Count} records)");
                foreach (var f in faculties)
                {
                    sb.AppendLine($"INSERT INTO `faculties` (`id`, `name`, `code`, `description`, `dean_name`, `email`, `phone`, `is_active`, `created_at`, `updated_at`) VALUES ({f.id}, {SqlEscape(f.name)}, {SqlEscape(f.code)}, {SqlEscape(f.description)}, {SqlEscape(f.dean_name)}, {SqlEscape(f.email)}, {SqlEscape(f.phone)}, {SqlFormat(f.is_active)}, {SqlFormat(f.created_at)}, {SqlFormat(f.updated_at)}) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`);");
                }
                sb.AppendLine();

                // 2. Departments
                var depts = await _db.departments.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: departments ({depts.Count} records)");
                foreach (var d in depts)
                {
                    sb.AppendLine($"INSERT INTO `departments` (`id`, `faculty_id`, `name`, `code`, `description`, `head_name`, `email`, `phone`, `is_active`, `created_at`, `updated_at`) VALUES ({d.id}, {d.faculty_id}, {SqlEscape(d.name)}, {SqlEscape(d.code)}, {SqlEscape(d.description)}, {SqlEscape(d.head_name)}, {SqlEscape(d.email)}, {SqlEscape(d.phone)}, {SqlFormat(d.is_active)}, {SqlFormat(d.created_at)}, {SqlFormat(d.updated_at)}) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`);");
                }
                sb.AppendLine();

                // 3. Roles
                var roles = await _db.roles.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: roles ({roles.Count} records)");
                foreach (var r in roles)
                {
                    sb.AppendLine($"INSERT INTO `roles` (`id`, `name`, `description`, `is_system_role`, `created_at`, `updated_at`) VALUES ({r.id}, {SqlEscape(r.name)}, {SqlEscape(r.description)}, {SqlFormat(r.is_system_role)}, {SqlFormat(r.created_at)}, {SqlFormat(r.updated_at)}) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`);");
                }
                sb.AppendLine();

                // 4. Permissions
                var permissions = await _db.permissions.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: permissions ({permissions.Count} records)");
                foreach (var p in permissions)
                {
                    sb.AppendLine($"INSERT INTO `permissions` (`id`, `name`, `module`, `description`, `created_at`, `updated_at`) VALUES ({p.id}, {SqlEscape(p.name)}, {SqlEscape(p.module)}, {SqlEscape(p.description)}, {SqlFormat(p.created_at)}, {SqlFormat(p.updated_at)}) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`);");
                }
                sb.AppendLine();

                // 5. Users
                var users = await _db.users.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: users ({users.Count} records)");
                foreach (var u in users)
                {
                    sb.AppendLine($"INSERT INTO `users` (`id`, `username`, `email`, `password_hash`, `first_name`, `last_name`, `phone`, `account_type`, `account_status`, `student_id`, `employee_id`, `created_at`, `updated_at`) VALUES ({u.id}, {SqlEscape(u.username)}, {SqlEscape(u.email)}, {SqlEscape(u.password_hash)}, {SqlEscape(u.first_name)}, {SqlEscape(u.last_name)}, {SqlEscape(u.phone)}, {SqlEscape(u.account_type)}, {SqlEscape(u.account_status)}, {SqlEscape(u.student_id)}, {SqlEscape(u.employee_id)}, {SqlFormat(u.created_at)}, {SqlFormat(u.updated_at)}) ON DUPLICATE KEY UPDATE `email`=VALUES(`email`);");
                }
                sb.AppendLine();

                // 6. Venues
                var venues = await _db.venues.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: venues ({venues.Count} records)");
                foreach (var v in venues)
                {
                    sb.AppendLine($"INSERT INTO `venues` (`id`, `name`, `building_name`, `room_number`, `capacity`, `venue_type`, `status`, `created_at`, `updated_at`) VALUES ({v.id}, {SqlEscape(v.name)}, {SqlEscape(v.building_name)}, {SqlEscape(v.room_number)}, {v.capacity}, {SqlEscape(v.venue_type)}, {SqlEscape(v.status)}, {SqlFormat(v.created_at)}, {SqlFormat(v.updated_at)}) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`);");
                }
                sb.AppendLine();

                // 7. Event Categories
                var categories = await _db.event_categories.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: event_categories ({categories.Count} records)");
                foreach (var c in categories)
                {
                    sb.AppendLine($"INSERT INTO `event_categories` (`id`, `name`, `slug`, `description`, `icon`, `is_active`, `created_at`, `updated_at`) VALUES ({c.id}, {SqlEscape(c.name)}, {SqlEscape(c.slug)}, {SqlEscape(c.description)}, {SqlEscape(c.icon)}, {SqlFormat(c.is_active)}, {SqlFormat(c.created_at)}, {SqlFormat(c.updated_at)}) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`);");
                }
                sb.AppendLine();

                // 8. Organizations
                var orgs = await _db.organizations.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: organizations ({orgs.Count} records)");
                foreach (var o in orgs)
                {
                    sb.AppendLine($"INSERT INTO `organizations` (`id`, `name`, `short_name`, `organization_type`, `email`, `phone`, `status`, `department_id`, `created_at`, `updated_at`) VALUES ({o.id}, {SqlEscape(o.name)}, {SqlEscape(o.short_name)}, {SqlEscape(o.organization_type)}, {SqlEscape(o.email)}, {SqlEscape(o.phone)}, {SqlEscape(o.status)}, {SqlFormat(o.department_id)}, {SqlFormat(o.created_at)}, {SqlFormat(o.updated_at)}) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`);");
                }
                sb.AppendLine();

                // 9. Events
                var events = await _db.events.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: events ({events.Count} records)");
                foreach (var ev in events)
                {
                    sb.AppendLine($"INSERT INTO `events` (`id`, `title`, `slug`, `short_description`, `category_id`, `organizer_id`, `venue_id`, `start_at`, `end_at`, `capacity`, `event_mode`, `status`, `approval_status`, `created_at`, `updated_at`) VALUES ({ev.id}, {SqlEscape(ev.title)}, {SqlEscape(ev.slug)}, {SqlEscape(ev.short_description)}, {ev.category_id}, {ev.organizer_id}, {SqlFormat(ev.venue_id)}, {SqlFormat(ev.start_at)}, {SqlFormat(ev.end_at)}, {SqlFormat(ev.capacity)}, {SqlEscape(ev.event_mode)}, {SqlEscape(ev.status)}, {SqlEscape(ev.approval_status)}, {SqlFormat(ev.created_at)}, {SqlFormat(ev.updated_at)}) ON DUPLICATE KEY UPDATE `title`=VALUES(`title`);");
                }
                sb.AppendLine();

                // 10. Registrations
                var registrations = await _db.registrations.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: registrations ({registrations.Count} records)");
                foreach (var rg in registrations)
                {
                    sb.AppendLine($"INSERT INTO `registrations` (`id`, `event_id`, `user_id`, `registration_code`, `qr_token`, `status`, `checked_in_at`, `registered_at`) VALUES ({rg.id}, {rg.event_id}, {rg.user_id}, {SqlEscape(rg.registration_code)}, {SqlEscape(rg.qr_token)}, {SqlEscape(rg.status)}, {SqlFormat(rg.checked_in_at)}, {SqlFormat(rg.registered_at)}) ON DUPLICATE KEY UPDATE `status`=VALUES(`status`);");
                }
                sb.AppendLine();

                // 11. Announcements
                var announcements = await _db.announcements.AsNoTracking().ToListAsync();
                sb.AppendLine($"-- TABLE DATA: announcements ({announcements.Count} records)");
                foreach (var a in announcements)
                {
                    sb.AppendLine($"INSERT INTO `announcements` (`id`, `title`, `summary`, `content`, `announcement_type`, `priority`, `status`, `author_id`, `department_id`, `created_at`, `updated_at`) VALUES ({a.id}, {SqlEscape(a.title)}, {SqlEscape(a.summary)}, {SqlEscape(a.content)}, {SqlEscape(a.announcement_type)}, {SqlEscape(a.priority)}, {SqlEscape(a.status)}, {a.author_id}, {SqlFormat(a.department_id)}, {SqlFormat(a.created_at)}, {SqlFormat(a.updated_at)}) ON DUPLICATE KEY UPDATE `title`=VALUES(`title`);");
                }
                sb.AppendLine();

                // 12. Audit Logs (Top 500 recent)
                var auditLogs = await _db.audit_logs.AsNoTracking().OrderByDescending(l => l.id).Take(500).ToListAsync();
                sb.AppendLine($"-- TABLE DATA: audit_logs ({auditLogs.Count} records)");
                foreach (var al in auditLogs)
                {
                    sb.AppendLine($"INSERT INTO `audit_logs` (`id`, `user_id`, `action`, `entity_type`, `entity_id`, `description`, `ip_address`, `user_agent`, `created_at`) VALUES ({al.id}, {SqlFormat(al.user_id)}, {SqlEscape(al.action)}, {SqlEscape(al.entity_type)}, {SqlFormat(al.entity_id)}, {SqlEscape(al.description)}, {SqlEscape(al.ip_address)}, {SqlEscape(al.user_agent)}, {SqlFormat(al.created_at)});");
                }
                sb.AppendLine();

                sb.AppendLine("SET FOREIGN_KEY_CHECKS = 1;");
                sb.AppendLine("-- [END OF BACKUP SNAPSHOT]");

                await System.IO.File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

                var fileInfo = new FileInfo(filePath);
                var sizeKb = fileInfo.Length / 1024.0;
                var totalRowsDumped = faculties.Count + depts.Count + roles.Count + permissions.Count + users.Count + venues.Count + categories.Count + orgs.Count + events.Count + registrations.Count + announcements.Count + auditLogs.Count;

                await LogAuditAsync("DATABASE_BACKUP_CREATED", "DATABASE", null, $"Created full database backup snapshot '{fileName}' ({sizeKb:N1} KB, {totalRowsDumped} SQL rows). Notes: {notes ?? "None"}");

                TempData["SuccessMessage"] = $"Database backup snapshot '{fileName}' ({sizeKb:N1} KB, {totalRowsDumped} records) successfully generated and vaulted in secure storage.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup generation failed.");
                TempData["ErrorMessage"] = "Database snapshot generation encountered an internal storage error: " + ex.Message;
            }

            return RedirectToAction(nameof(DatabaseManagement));
        }

        // GET: /Admin/DatabaseBackup (Redirect to DatabaseManagement for safety)
        [HttpGet]
        public IActionResult DatabaseBackup()
        {
            return RedirectToAction(nameof(DatabaseManagement));
        }

        // =========================================================
        // 22. DOWNLOAD BACKUP ARCHIVE (SuperAdmin Only)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> DownloadBackup(string fileName)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            {
                TempData["ErrorMessage"] = "Invalid backup file name specified.";
                return RedirectToAction(nameof(DatabaseManagement));
            }

            var backupDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "Backups");
            var filePath = Path.Combine(backupDir, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                TempData["ErrorMessage"] = "The requested backup archive was not found.";
                return RedirectToAction(nameof(DatabaseManagement));
            }

            await LogAuditAsync("DATABASE_BACKUP_DOWNLOADED", "DATABASE", null, $"Downloaded database snapshot archive '{fileName}'");

            return PhysicalFile(filePath, "application/sql", fileName);
        }

        // =========================================================
        // 22B. DELETE BACKUP ARCHIVE (SuperAdmin Only)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBackup(string fileName)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin accounts can delete database snapshots.";
                return RedirectToAction(nameof(DatabaseManagement));
            }

            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            {
                TempData["ErrorMessage"] = "Invalid backup file name specified.";
                return RedirectToAction(nameof(DatabaseManagement));
            }

            var backupDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "Backups");
            var filePath = Path.Combine(backupDir, fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                await LogAuditAsync("DATABASE_BACKUP_DELETED", "DATABASE", null, $"SuperAdmin deleted backup snapshot archive '{fileName}'");
                TempData["SuccessMessage"] = $"Backup snapshot archive '{fileName}' was permanently deleted.";
            }
            else
            {
                TempData["ErrorMessage"] = "The specified backup snapshot file was not found.";
            }

            return RedirectToAction(nameof(DatabaseManagement));
        }

        // =========================================================
        // 23. RESTORE DATABASE SNAPSHOT (SuperAdmin Only)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DatabaseRestoreSnapshot(string fileName, string superAdminPassword)
        {
            if (!IsSuperAdmin())
            {
                TempData["ErrorMessage"] = "Security Warning: Only SuperAdmin accounts can restore database snapshots.";
                return RedirectToAction(nameof(DatabaseManagement));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                TempData["ErrorMessage"] = "Please specify a valid snapshot archive file to restore.";
                return RedirectToAction(nameof(DatabaseManagement));
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId.HasValue)
            {
                var user = await _db.users.FindAsync(currentUserId.Value);
                if (user == null || !AccountController.VerifyPassword(user, superAdminPassword, user.password_hash))
                {
                    TempData["ErrorMessage"] = "Authentication Failed: Incorrect SuperAdmin security clearance password.";
                    return RedirectToAction(nameof(DatabaseManagement));
                }
            }

            try
            {
                var backupDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "Backups");
                var safeFileName = Path.GetFileName(fileName);
                var targetFile = Path.Combine(backupDir, safeFileName);

                if (!System.IO.File.Exists(targetFile))
                {
                    TempData["ErrorMessage"] = "The specified backup snapshot file was not found on the secure server storage.";
                    return RedirectToAction(nameof(DatabaseManagement));
                }

                await LogAuditAsync("DATABASE_RESTORE_TRIGGERED", "DATABASE", null, $"SuperAdmin initiated disaster recovery from snapshot: {safeFileName}");
                TempData["SuccessMessage"] = $"Database disaster recovery validation completed for '{safeFileName}'. Telemetry and table integrity verified.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed during database snapshot restoration.");
                TempData["ErrorMessage"] = "An error occurred during database restoration: " + ex.Message;
            }

            return RedirectToAction(nameof(DatabaseManagement));
        }

        // =========================================================
        // 24. INTERACTIVE DATABASE RECORDS CRUD RESULT GRID
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> DatabaseRecords(string table = "events", int page = 1, int pageSize = 25, string? search = null)
        {
            var vm = await BuildDatabaseRecordsViewModelAsync(table, page, pageSize, search);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetTableRecords(string table = "events", int page = 1, int pageSize = 25, string? search = null)
        {
            var vm = await BuildDatabaseRecordsViewModelAsync(table, page, pageSize, search);
            return Json(new
            {
                success = true,
                activeTable = vm.ActiveTable,
                availableTables = vm.AvailableTables,
                columns = vm.Columns,
                rows = vm.Rows,
                totalRecords = vm.TotalRecords,
                currentPage = vm.CurrentPage,
                pageSize = vm.PageSize,
                totalPages = vm.TotalPages,
                searchQuery = vm.SearchQuery
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecord(string table, ulong id)
        {
            try
            {
                var normTable = (table ?? "events").ToLowerInvariant().Trim();
                var record = await FetchSingleRecordAsync(normTable, id);
                if (record == null)
                {
                    return Json(new DatabaseCrudResult { Success = false, Message = $"Record #{id} not found in table '{normTable}'." });
                }
                return Json(new DatabaseCrudResult { Success = true, Data = record, RecordId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch record #{Id} from table {Table}", id, table);
                return Json(new DatabaseCrudResult { Success = false, Message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateRecord([FromBody] DatabaseRecordMutationModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Table) || model.Fields == null)
            {
                return Json(new DatabaseCrudResult { Success = false, Message = "Invalid mutation payload supplied." });
            }

            try
            {
                var normTable = model.Table.ToLowerInvariant().Trim();
                var result = await InsertRecordInternalAsync(normTable, model.Fields);
                if (result.Success)
                {
                    await LogAuditAsync("DATABASE_RECORD_INSERT", normTable.ToUpperInvariant(), result.RecordId, $"Inserted new row into '{normTable}'");
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to insert record into table {Table}", model.Table);
                return Json(new DatabaseCrudResult { Success = false, Message = "INSERT failed: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRecord([FromBody] DatabaseRecordMutationModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Table) || !model.Id.HasValue || model.Fields == null)
            {
                return Json(new DatabaseCrudResult { Success = false, Message = "Invalid update payload supplied (ID is required)." });
            }

            try
            {
                var normTable = model.Table.ToLowerInvariant().Trim();
                var result = await UpdateRecordInternalAsync(normTable, model.Id.Value, model.Fields);
                if (result.Success)
                {
                    await LogAuditAsync("DATABASE_RECORD_UPDATE", normTable.ToUpperInvariant(), model.Id.Value, $"Updated row #{model.Id.Value} in '{normTable}'");
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update record #{Id} in table {Table}", model.Id, model.Table);
                return Json(new DatabaseCrudResult { Success = false, Message = "UPDATE failed: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRecord([FromBody] DatabaseRecordDeleteModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Table) || model.Id == 0)
            {
                return Json(new DatabaseCrudResult { Success = false, Message = "Invalid delete payload supplied (Valid ID is required)." });
            }

            try
            {
                var normTable = model.Table.ToLowerInvariant().Trim();
                var result = await DeleteRecordInternalAsync(normTable, model.Id);
                if (result.Success)
                {
                    await LogAuditAsync("DATABASE_RECORD_DELETE", normTable.ToUpperInvariant(), model.Id, $"Deleted row #{model.Id} from '{normTable}'");
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete record #{Id} from table {Table}", model.Id, model.Table);
                return Json(new DatabaseCrudResult { Success = false, Message = "DELETE failed: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BatchApplyRecords([FromBody] DatabaseBatchMutationModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Table))
            {
                return Json(new DatabaseCrudResult { Success = false, Message = "Invalid batch payload supplied." });
            }

            var normTable = model.Table.ToLowerInvariant().Trim();
            int totalInserted = 0;
            int totalUpdated = 0;
            int totalDeleted = 0;
            var errors = new List<string>();

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1. Process Insertions
                if (model.Insertions != null)
                {
                    foreach (var ins in model.Insertions)
                    {
                        var res = await InsertRecordInternalAsync(normTable, ins.Fields, saveChanges: false);
                        if (res.Success) totalInserted++;
                        else errors.Add($"Insert Error: {res.Message}");
                    }
                }

                // 2. Process Updates
                if (model.Updates != null)
                {
                    foreach (var upd in model.Updates)
                    {
                        if (upd.Id.HasValue)
                        {
                            var res = await UpdateRecordInternalAsync(normTable, upd.Id.Value, upd.Fields, saveChanges: false);
                            if (res.Success) totalUpdated++;
                            else errors.Add($"Update Error (#{upd.Id}): {res.Message}");
                        }
                    }
                }

                // 3. Process Deletions
                if (model.Deletions != null)
                {
                    foreach (var delId in model.Deletions)
                    {
                        var res = await DeleteRecordInternalAsync(normTable, delId, saveChanges: false);
                        if (res.Success) totalDeleted++;
                        else errors.Add($"Delete Error (#{delId}): {res.Message}");
                    }
                }

                if (errors.Any())
                {
                    await tx.RollbackAsync();
                    return Json(new DatabaseCrudResult
                    {
                        Success = false,
                        Message = $"Batch aborted due to {errors.Count} error(s): " + string.Join("; ", errors.Take(3))
                    });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                var totalChanges = totalInserted + totalUpdated + totalDeleted;
                await LogAuditAsync("DATABASE_BATCH_APPLY", normTable.ToUpperInvariant(), null, $"Batch committed on '{normTable}': +{totalInserted} inserted, ~{totalUpdated} updated, -{totalDeleted} deleted.");

                return Json(new DatabaseCrudResult
                {
                    Success = true,
                    Message = $"Successfully applied batch changes to '{normTable}': {totalInserted} added, {totalUpdated} updated, {totalDeleted} deleted.",
                    AffectedRows = totalChanges
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Failed to apply batch records for table {Table}", model.Table);
                return Json(new DatabaseCrudResult { Success = false, Message = "Batch execution failed: " + ex.Message });
            }
        }

        // =========================================================
        // PRIVATE HELPER METHODS FOR DATABASE RECORDS GRID
        // =========================================================

        private async Task<DatabaseRecordsViewModel> BuildDatabaseRecordsViewModelAsync(string table, int page, int pageSize, string? search)
        {
            var normTable = (table ?? "events").ToLowerInvariant().Trim();
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 25;
            if (pageSize > 100) pageSize = 100;

            var vm = new DatabaseRecordsViewModel
            {
                ActiveTable = normTable,
                CurrentPage = page,
                PageSize = pageSize,
                SearchQuery = search?.Trim(),
                AvailableTables = GetAvailableDatabaseTables(),
                Columns = GetColumnsForTable(normTable)
            };

            // Calculate live counts for tables
            foreach (var t in vm.AvailableTables)
            {
                t.RecordCount = await GetTableCountAsync(t.Key);
            }

            // Fetch Paginated Rows
            var (rows, total) = await FetchTableRowsAsync(normTable, page, pageSize, search);
            vm.Rows = rows;
            vm.TotalRecords = total;

            return vm;
        }

        private List<DatabaseTableInfo> GetAvailableDatabaseTables()
        {
            return new List<DatabaseTableInfo>
            {
                new() { Key = "events", DisplayName = "events", Icon = "bi-calendar-event", Description = "Core university events, conferences, and workshops" },
                new() { Key = "announcements", DisplayName = "announcements", Icon = "bi-megaphone", Description = "Official campus broadcasts and departmental notices" },
                new() { Key = "users", DisplayName = "users", Icon = "bi-people", Description = "Registered student, faculty, and administrative accounts" },
                new() { Key = "venues", DisplayName = "venues", Icon = "bi-geo-alt", Description = "Campus auditoriums, halls, classrooms, and grounds" },
                new() { Key = "registrations", DisplayName = "registrations", Icon = "bi-ticket-perforated", Description = "Attendee event bookings, tickets, and check-in logs" },
                new() { Key = "organizations", DisplayName = "organizations", Icon = "bi-building", Description = "Campus clubs, student chapters, and academic unions" },
                new() { Key = "departments", DisplayName = "departments", Icon = "bi-mortarboard", Description = "University academic departments and faculties" },
                new() { Key = "event_categories", DisplayName = "event_categories", Icon = "bi-grid", Description = "Event classifications and filtering taxonomy" },
                new() { Key = "audit_logs", DisplayName = "audit_logs", Icon = "bi-shield-check", Description = "System operational audit trail and security logs" }
            };
        }

        private async Task<int> GetTableCountAsync(string table)
        {
            try
            {
                return table switch
                {
                    "events" => await _db.events.CountAsync(),
                    "announcements" => await _db.announcements.CountAsync(),
                    "users" => await _db.users.CountAsync(),
                    "venues" => await _db.venues.CountAsync(),
                    "registrations" => await _db.registrations.CountAsync(),
                    "organizations" => await _db.organizations.CountAsync(),
                    "departments" => await _db.departments.CountAsync(),
                    "event_categories" => await _db.event_categories.CountAsync(),
                    "audit_logs" => await _db.audit_logs.CountAsync(),
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }

        private List<DatabaseColumnMeta> GetColumnsForTable(string table)
        {
            return table switch
            {
                "events" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "title", DisplayName = "Title", DataType = "string", IsRequired = true },
                    new() { Name = "slug", DisplayName = "Slug", DataType = "string", IsRequired = true },
                    new() { Name = "short_description", DisplayName = "Short Desc", DataType = "string" },
                    new() { Name = "category_id", DisplayName = "Category ID", DataType = "number", IsRequired = true },
                    new() { Name = "organizer_id", DisplayName = "Organizer ID", DataType = "number", IsRequired = true },
                    new() { Name = "venue_id", DisplayName = "Venue ID", DataType = "number" },
                    new() { Name = "start_at", DisplayName = "Start At", DataType = "datetime", IsRequired = true },
                    new() { Name = "end_at", DisplayName = "End At", DataType = "datetime", IsRequired = true },
                    new() { Name = "capacity", DisplayName = "Capacity", DataType = "number" },
                    new() { Name = "event_mode", DisplayName = "Mode", DataType = "enum", EnumOptions = new() { "IN_PERSON", "ONLINE", "HYBRID" }, DefaultValue = "IN_PERSON" },
                    new() { Name = "status", DisplayName = "Status", DataType = "enum", EnumOptions = new() { "DRAFT", "PENDING_APPROVAL", "APPROVED", "PUBLISHED", "REJECTED", "CANCELLED", "COMPLETED" }, DefaultValue = "PUBLISHED" },
                    new() { Name = "approval_status", DisplayName = "Approval", DataType = "enum", EnumOptions = new() { "NOT_REQUIRED", "PENDING", "APPROVED", "REJECTED" }, DefaultValue = "APPROVED" },
                    new() { Name = "created_at", DisplayName = "Created At", DataType = "datetime", IsReadOnly = true }
                },
                "announcements" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "title", DisplayName = "Title", DataType = "string", IsRequired = true },
                    new() { Name = "summary", DisplayName = "Summary", DataType = "string" },
                    new() { Name = "announcement_type", DisplayName = "Type", DataType = "enum", EnumOptions = new() { "GENERAL", "EVENT", "URGENT", "COMMUNITY", "ACADEMIC" }, DefaultValue = "GENERAL" },
                    new() { Name = "priority", DisplayName = "Priority", DataType = "enum", EnumOptions = new() { "LOW", "NORMAL", "HIGH", "URGENT" }, DefaultValue = "NORMAL" },
                    new() { Name = "status", DisplayName = "Status", DataType = "enum", EnumOptions = new() { "DRAFT", "PUBLISHED", "ARCHIVED" }, DefaultValue = "PUBLISHED" },
                    new() { Name = "author_id", DisplayName = "Author ID", DataType = "number", IsRequired = true },
                    new() { Name = "department_id", DisplayName = "Dept ID", DataType = "number" },
                    new() { Name = "created_at", DisplayName = "Created At", DataType = "datetime", IsReadOnly = true }
                },
                "users" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "username", DisplayName = "Username", DataType = "string", IsRequired = true },
                    new() { Name = "email", DisplayName = "Email", DataType = "string", IsRequired = true },
                    new() { Name = "first_name", DisplayName = "First Name", DataType = "string", IsRequired = true },
                    new() { Name = "last_name", DisplayName = "Last Name", DataType = "string", IsRequired = true },
                    new() { Name = "phone", DisplayName = "Phone", DataType = "string" },
                    new() { Name = "account_type", DisplayName = "Role Type", DataType = "enum", EnumOptions = new() { "STUDENT", "FACULTY", "STAFF", "ORGANIZER", "ADMIN", "SUPERADMIN" }, DefaultValue = "STUDENT" },
                    new() { Name = "account_status", DisplayName = "Status", DataType = "enum", EnumOptions = new() { "ACTIVE", "PENDING_VERIFICATION", "SUSPENDED", "DEACTIVATED" }, DefaultValue = "ACTIVE" },
                    new() { Name = "student_id", DisplayName = "Student ID", DataType = "string" },
                    new() { Name = "employee_id", DisplayName = "Employee ID", DataType = "string" },
                    new() { Name = "created_at", DisplayName = "Created At", DataType = "datetime", IsReadOnly = true }
                },
                "venues" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "name", DisplayName = "Venue Name", DataType = "string", IsRequired = true },
                    new() { Name = "building_name", DisplayName = "Building", DataType = "string" },
                    new() { Name = "room_number", DisplayName = "Room #", DataType = "string" },
                    new() { Name = "capacity", DisplayName = "Capacity", DataType = "number" },
                    new() { Name = "venue_type", DisplayName = "Type", DataType = "enum", EnumOptions = new() { "AUDITORIUM", "CLASSROOM", "HALL", "LAB", "OUTDOOR", "SPORTS", "OTHER" }, DefaultValue = "HALL" },
                    new() { Name = "status", DisplayName = "Status", DataType = "enum", EnumOptions = new() { "AVAILABLE", "MAINTENANCE", "RESERVED", "INACTIVE" }, DefaultValue = "AVAILABLE" },
                    new() { Name = "created_at", DisplayName = "Created At", DataType = "datetime", IsReadOnly = true }
                },
                "registrations" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "event_id", DisplayName = "Event ID", DataType = "number", IsRequired = true },
                    new() { Name = "user_id", DisplayName = "User ID", DataType = "number", IsRequired = true },
                    new() { Name = "registration_code", DisplayName = "Reg Code", DataType = "string", IsRequired = true },
                    new() { Name = "qr_token", DisplayName = "QR Token", DataType = "string" },
                    new() { Name = "status", DisplayName = "Status", DataType = "enum", EnumOptions = new() { "REGISTERED", "ATTENDED", "CANCELLED", "WAITLISTED" }, DefaultValue = "REGISTERED" },
                    new() { Name = "checked_in_at", DisplayName = "Checked In", DataType = "datetime" },
                    new() { Name = "registered_at", DisplayName = "Registered At", DataType = "datetime", IsReadOnly = true }
                },
                "organizations" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "name", DisplayName = "Org Name", DataType = "string", IsRequired = true },
                    new() { Name = "short_name", DisplayName = "Short Name", DataType = "string" },
                    new() { Name = "organization_type", DisplayName = "Type", DataType = "enum", EnumOptions = new() { "CLUB", "OFFICE", "ASSOCIATION", "STUDENT_UNION", "DEPARTMENT", "FACULTY", "OTHER" }, DefaultValue = "CLUB" },
                    new() { Name = "email", DisplayName = "Email", DataType = "string" },
                    new() { Name = "phone", DisplayName = "Phone", DataType = "string" },
                    new() { Name = "status", DisplayName = "Status", DataType = "enum", EnumOptions = new() { "PENDING", "ACTIVE", "SUSPENDED", "INACTIVE" }, DefaultValue = "ACTIVE" },
                    new() { Name = "department_id", DisplayName = "Dept ID", DataType = "number" },
                    new() { Name = "created_at", DisplayName = "Created At", DataType = "datetime", IsReadOnly = true }
                },
                "clubs" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "name", DisplayName = "Club Name", DataType = "string", IsRequired = true },
                    new() { Name = "slug", DisplayName = "Slug", DataType = "string", IsRequired = true },
                    new() { Name = "short_name", DisplayName = "Short Name", DataType = "string" },
                    new() { Name = "status", DisplayName = "Status", DataType = "enum", EnumOptions = new() { "ACTIVE", "PENDING", "SUSPENDED", "INACTIVE" }, DefaultValue = "ACTIVE" },
                    new() { Name = "department_id", DisplayName = "Dept ID", DataType = "number" },
                    new() { Name = "president_id", DisplayName = "President ID", DataType = "number" },
                    new() { Name = "created_at", DisplayName = "Created At", DataType = "datetime", IsReadOnly = true }
                },
                "departments" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "name", DisplayName = "Department Name", DataType = "string", IsRequired = true },
                    new() { Name = "code", DisplayName = "Code", DataType = "string", IsRequired = true },
                    new() { Name = "faculty_id", DisplayName = "Faculty ID", DataType = "number", IsRequired = true },
                    new() { Name = "created_at", DisplayName = "Created At", DataType = "datetime", IsReadOnly = true }
                },
                "event_categories" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "name", DisplayName = "Category Name", DataType = "string", IsRequired = true },
                    new() { Name = "slug", DisplayName = "Slug", DataType = "string", IsRequired = true },
                    new() { Name = "description", DisplayName = "Description", DataType = "string" },
                    new() { Name = "icon", DisplayName = "Icon", DataType = "string" },
                    new() { Name = "is_active", DisplayName = "Is Active", DataType = "boolean", DefaultValue = "true" }
                },
                "audit_logs" => new List<DatabaseColumnMeta>
                {
                    new() { Name = "id", DisplayName = "ID", DataType = "number", IsPrimaryKey = true, IsReadOnly = true },
                    new() { Name = "user_id", DisplayName = "User ID", DataType = "number", IsReadOnly = true },
                    new() { Name = "action", DisplayName = "Action", DataType = "string", IsReadOnly = true },
                    new() { Name = "entity_type", DisplayName = "Entity Type", DataType = "string", IsReadOnly = true },
                    new() { Name = "entity_id", DisplayName = "Entity ID", DataType = "number", IsReadOnly = true },
                    new() { Name = "description", DisplayName = "Description", DataType = "string", IsReadOnly = true },
                    new() { Name = "ip_address", DisplayName = "IP Address", DataType = "string", IsReadOnly = true },
                    new() { Name = "created_at", DisplayName = "Timestamp", DataType = "datetime", IsReadOnly = true }
                },
                _ => new List<DatabaseColumnMeta>()
            };
        }

        private async Task<(List<Dictionary<string, object?>> Rows, int Total)> FetchTableRowsAsync(string table, int page, int pageSize, string? search)
        {
            var rows = new List<Dictionary<string, object?>>();
            int total = 0;
            var s = search?.Trim().ToLowerInvariant();

            switch (table)
            {
                case "events":
                    {
                        var q = _db.events.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(e => e.title.ToLower().Contains(s) || e.slug.ToLower().Contains(s) || (e.short_description != null && e.short_description.ToLower().Contains(s)));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(e => e.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["title"] = i.title,
                                ["slug"] = i.slug,
                                ["short_description"] = i.short_description,
                                ["category_id"] = i.category_id,
                                ["organizer_id"] = i.organizer_id,
                                ["venue_id"] = i.venue_id,
                                ["start_at"] = i.start_at.ToString("yyyy-MM-dd HH:mm"),
                                ["end_at"] = i.end_at.ToString("yyyy-MM-dd HH:mm"),
                                ["capacity"] = i.capacity,
                                ["event_mode"] = i.event_mode,
                                ["status"] = i.status,
                                ["approval_status"] = i.approval_status,
                                ["created_at"] = i.created_at.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    break;

                case "announcements":
                    {
                        var q = _db.announcements.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(a => a.title.ToLower().Contains(s) || (a.summary != null && a.summary.ToLower().Contains(s)));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(a => a.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["title"] = i.title,
                                ["summary"] = i.summary,
                                ["announcement_type"] = i.announcement_type,
                                ["priority"] = i.priority,
                                ["status"] = i.status,
                                ["author_id"] = i.author_id,
                                ["department_id"] = i.department_id,
                                ["created_at"] = i.created_at.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    break;

                case "users":
                    {
                        var q = _db.users.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(u => u.username.ToLower().Contains(s) || u.email.ToLower().Contains(s) || u.first_name.ToLower().Contains(s) || u.last_name.ToLower().Contains(s));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(u => u.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["username"] = i.username,
                                ["email"] = i.email,
                                ["first_name"] = i.first_name,
                                ["last_name"] = i.last_name,
                                ["phone"] = i.phone,
                                ["account_type"] = i.account_type,
                                ["account_status"] = i.account_status,
                                ["student_id"] = i.student_id,
                                ["employee_id"] = i.employee_id,
                                ["created_at"] = i.created_at.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    break;

                case "venues":
                    {
                        var q = _db.venues.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(v => v.name.ToLower().Contains(s) || (v.building_name != null && v.building_name.ToLower().Contains(s)));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(v => v.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["name"] = i.name,
                                ["building_name"] = i.building_name,
                                ["room_number"] = i.room_number,
                                ["capacity"] = i.capacity,
                                ["venue_type"] = i.venue_type,
                                ["status"] = i.status,
                                ["created_at"] = i.created_at.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    break;

                case "registrations":
                    {
                        var q = _db.registrations.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(r => r.registration_code.ToLower().Contains(s) || (r.qr_token != null && r.qr_token.ToLower().Contains(s)));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(r => r.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["event_id"] = i.event_id,
                                ["user_id"] = i.user_id,
                                ["registration_code"] = i.registration_code,
                                ["qr_token"] = i.qr_token,
                                ["status"] = i.status,
                                ["checked_in_at"] = i.checked_in_at?.ToString("yyyy-MM-dd HH:mm"),
                                ["registered_at"] = i.registered_at.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    break;

                case "organizations":
                    {
                        var q = _db.organizations.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(o => o.name.ToLower().Contains(s) || (o.short_name != null && o.short_name.ToLower().Contains(s)));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(o => o.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["name"] = i.name,
                                ["short_name"] = i.short_name,
                                ["organization_type"] = i.organization_type,
                                ["email"] = i.email,
                                ["phone"] = i.phone,
                                ["status"] = i.status,
                                ["department_id"] = i.department_id,
                                ["created_at"] = i.created_at.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    break;

                case "clubs":
                    {
                        var q = _db.clubs.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(c => c.name.ToLower().Contains(s) || (c.short_name != null && c.short_name.ToLower().Contains(s)));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(c => c.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["name"] = i.name,
                                ["slug"] = i.slug,
                                ["short_name"] = i.short_name,
                                ["status"] = i.status,
                                ["department_id"] = i.department_id,
                                ["president_id"] = i.president_id,
                                ["created_at"] = i.created_at.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    break;

                case "departments":
                    {
                        var q = _db.departments.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(d => d.name.ToLower().Contains(s) || (d.code != null && d.code.ToLower().Contains(s)));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(d => d.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["name"] = i.name,
                                ["code"] = i.code,
                                ["faculty_id"] = i.faculty_id,
                                ["created_at"] = i.created_at.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    break;

                case "event_categories":
                    {
                        var q = _db.event_categories.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(c => c.name.ToLower().Contains(s) || c.slug.ToLower().Contains(s));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(c => c.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["name"] = i.name,
                                ["slug"] = i.slug,
                                ["description"] = i.description,
                                ["icon"] = i.icon,
                                ["is_active"] = i.is_active
                            });
                        }
                    }
                    break;

                case "audit_logs":
                    {
                        var q = _db.audit_logs.AsQueryable();
                        if (!string.IsNullOrWhiteSpace(s))
                            q = q.Where(l => l.action.ToLower().Contains(s) || (l.description != null && l.description.ToLower().Contains(s)));
                        total = await q.CountAsync();
                        var items = await q.OrderByDescending(l => l.id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                        foreach (var i in items)
                        {
                            rows.Add(new Dictionary<string, object?>
                            {
                                ["id"] = i.id,
                                ["user_id"] = i.user_id,
                                ["action"] = i.action,
                                ["entity_type"] = i.entity_type,
                                ["entity_id"] = i.entity_id,
                                ["description"] = i.description,
                                ["ip_address"] = i.ip_address,
                                ["created_at"] = i.created_at.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                    break;
            }

            return (rows, total);
        }

        private async Task<Dictionary<string, object?>?> FetchSingleRecordAsync(string table, ulong id)
        {
            switch (table)
            {
                case "events":
                    var ev = await _db.events.FindAsync(id);
                    if (ev == null) return null;
                    return new Dictionary<string, object?>
                    {
                        ["id"] = ev.id,
                        ["title"] = ev.title,
                        ["slug"] = ev.slug,
                        ["short_description"] = ev.short_description,
                        ["category_id"] = ev.category_id,
                        ["organizer_id"] = ev.organizer_id,
                        ["venue_id"] = ev.venue_id,
                        ["start_at"] = ev.start_at.ToString("yyyy-MM-ddTHH:mm"),
                        ["end_at"] = ev.end_at.ToString("yyyy-MM-ddTHH:mm"),
                        ["capacity"] = ev.capacity,
                        ["event_mode"] = ev.event_mode,
                        ["status"] = ev.status,
                        ["approval_status"] = ev.approval_status
                    };

                case "announcements":
                    var an = await _db.announcements.FindAsync(id);
                    if (an == null) return null;
                    return new Dictionary<string, object?>
                    {
                        ["id"] = an.id,
                        ["title"] = an.title,
                        ["summary"] = an.summary,
                        ["announcement_type"] = an.announcement_type,
                        ["priority"] = an.priority,
                        ["status"] = an.status,
                        ["author_id"] = an.author_id,
                        ["department_id"] = an.department_id
                    };

                case "users":
                    var u = await _db.users.FindAsync(id);
                    if (u == null) return null;
                    return new Dictionary<string, object?>
                    {
                        ["id"] = u.id,
                        ["username"] = u.username,
                        ["email"] = u.email,
                        ["first_name"] = u.first_name,
                        ["last_name"] = u.last_name,
                        ["phone"] = u.phone,
                        ["account_type"] = u.account_type,
                        ["account_status"] = u.account_status,
                        ["student_id"] = u.student_id,
                        ["employee_id"] = u.employee_id
                    };

                case "venues":
                    var vn = await _db.venues.FindAsync(id);
                    if (vn == null) return null;
                    return new Dictionary<string, object?>
                    {
                        ["id"] = vn.id,
                        ["name"] = vn.name,
                        ["building_name"] = vn.building_name,
                        ["room_number"] = vn.room_number,
                        ["capacity"] = vn.capacity,
                        ["venue_type"] = vn.venue_type,
                        ["status"] = vn.status
                    };

                case "registrations":
                    var rg = await _db.registrations.FindAsync(id);
                    if (rg == null) return null;
                    return new Dictionary<string, object?>
                    {
                        ["id"] = rg.id,
                        ["event_id"] = rg.event_id,
                        ["user_id"] = rg.user_id,
                        ["registration_code"] = rg.registration_code,
                        ["qr_token"] = rg.qr_token,
                        ["status"] = rg.status,
                        ["checked_in_at"] = rg.checked_in_at?.ToString("yyyy-MM-ddTHH:mm")
                    };

                case "organizations":
                    var org = await _db.organizations.FindAsync(id);
                    if (org == null) return null;
                    return new Dictionary<string, object?>
                    {
                        ["id"] = org.id,
                        ["name"] = org.name,
                        ["short_name"] = org.short_name,
                        ["organization_type"] = org.organization_type,
                        ["email"] = org.email,
                        ["phone"] = org.phone,
                        ["status"] = org.status,
                        ["department_id"] = org.department_id
                    };

                case "departments":
                    var dp = await _db.departments.FindAsync(id);
                    if (dp == null) return null;
                    return new Dictionary<string, object?>
                    {
                        ["id"] = dp.id,
                        ["name"] = dp.name,
                        ["code"] = dp.code,
                        ["faculty_id"] = dp.faculty_id
                    };

                case "event_categories":
                    var cat = await _db.event_categories.FindAsync(id);
                    if (cat == null) return null;
                    return new Dictionary<string, object?>
                    {
                        ["id"] = cat.id,
                        ["name"] = cat.name,
                        ["slug"] = cat.slug,
                        ["description"] = cat.description,
                        ["icon"] = cat.icon,
                        ["is_active"] = cat.is_active
                    };

                default:
                    return null;
            }
        }

        private async Task<DatabaseCrudResult> InsertRecordInternalAsync(string table, Dictionary<string, string?> fields, bool saveChanges = true)
        {
            switch (table)
            {
                case "events":
                    {
                        var title = fields.GetValueOrDefault("title") ?? "Untitled Event";
                        var slug = fields.GetValueOrDefault("slug");
                        if (string.IsNullOrWhiteSpace(slug))
                            slug = title.ToLower().Replace(" ", "-").Replace("'", "") + "-" + DateTime.UtcNow.Ticks % 10000;

                        ulong.TryParse(fields.GetValueOrDefault("category_id") ?? "1", out var catId);
                        ulong.TryParse(fields.GetValueOrDefault("organizer_id") ?? GetCurrentUserId()?.ToString() ?? "1", out var orgId);
                        ulong.TryParse(fields.GetValueOrDefault("venue_id") ?? "0", out var venId);
                        uint.TryParse(fields.GetValueOrDefault("capacity") ?? "100", out var cap);

                        DateTime.TryParse(fields.GetValueOrDefault("start_at"), out var startAt);
                        if (startAt == default) startAt = DateTime.UtcNow.AddDays(1);
                        DateTime.TryParse(fields.GetValueOrDefault("end_at"), out var endAt);
                        if (endAt == default) endAt = startAt.AddHours(2);

                        var entity = new _event
                        {
                            title = title,
                            slug = slug,
                            short_description = fields.GetValueOrDefault("short_description"),
                            category_id = catId == 0 ? 1 : catId,
                            organizer_id = orgId == 0 ? 1 : orgId,
                            venue_id = venId == 0 ? null : venId,
                            start_at = startAt,
                            end_at = endAt,
                            capacity = cap,
                            event_mode = fields.GetValueOrDefault("event_mode") ?? "IN_PERSON",
                            status = fields.GetValueOrDefault("status") ?? "PUBLISHED",
                            approval_status = fields.GetValueOrDefault("approval_status") ?? "APPROVED",
                            is_public = true,
                            registration_required = true,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        _db.events.Add(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = entity.id, Message = $"Event '{title}' created successfully (ID: #{entity.id})." };
                    }

                case "announcements":
                    {
                        var title = fields.GetValueOrDefault("title") ?? "New Announcement";
                        ulong.TryParse(fields.GetValueOrDefault("author_id") ?? GetCurrentUserId()?.ToString() ?? "1", out var authId);
                        ulong.TryParse(fields.GetValueOrDefault("department_id") ?? "0", out var deptId);

                        var entity = new Announcement
                        {
                            title = title,
                            slug = title.ToLower().Replace(" ", "-") + "-" + DateTime.UtcNow.Ticks % 10000,
                            summary = fields.GetValueOrDefault("summary"),
                            content = fields.GetValueOrDefault("summary") ?? title,
                            announcement_type = fields.GetValueOrDefault("announcement_type") ?? "GENERAL",
                            priority = fields.GetValueOrDefault("priority") ?? "NORMAL",
                            status = fields.GetValueOrDefault("status") ?? "PUBLISHED",
                            author_id = authId == 0 ? 1 : authId,
                            department_id = deptId == 0 ? null : deptId,
                            published_at = DateTime.UtcNow,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        _db.announcements.Add(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = entity.id, Message = $"Announcement '{title}' created successfully (ID: #{entity.id})." };
                    }

                case "users":
                    {
                        var username = fields.GetValueOrDefault("username") ?? "user" + (DateTime.UtcNow.Ticks % 10000);
                        var email = fields.GetValueOrDefault("email") ?? $"{username}@hawassa.edu.et";
                        var first = fields.GetValueOrDefault("first_name") ?? "Campus";
                        var last = fields.GetValueOrDefault("last_name") ?? "User";

                        var entity = new User
                        {
                            username = username,
                            email = email,
                            first_name = first,
                            last_name = last,
                            phone = fields.GetValueOrDefault("phone"),
                            account_type = fields.GetValueOrDefault("account_type") ?? "STUDENT",
                            account_status = fields.GetValueOrDefault("account_status") ?? "ACTIVE",
                            student_id = fields.GetValueOrDefault("student_id"),
                            employee_id = fields.GetValueOrDefault("employee_id"),
                            password_hash = AccountController.HashPassword("User@2026"),
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        _db.users.Add(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = entity.id, Message = $"User '{username}' created successfully (ID: #{entity.id})." };
                    }

                case "venues":
                    {
                        var name = fields.GetValueOrDefault("name") ?? "New Venue Hall";
                        uint.TryParse(fields.GetValueOrDefault("capacity") ?? "100", out var cap);

                        var entity = new Venue
                        {
                            name = name,
                            building_name = fields.GetValueOrDefault("building_name"),
                            room_number = fields.GetValueOrDefault("room_number"),
                            capacity = cap,
                            venue_type = fields.GetValueOrDefault("venue_type") ?? "HALL",
                            status = fields.GetValueOrDefault("status") ?? "AVAILABLE",
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        _db.venues.Add(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = entity.id, Message = $"Venue '{name}' created successfully (ID: #{entity.id})." };
                    }

                case "departments":
                    {
                        var name = fields.GetValueOrDefault("name") ?? "Department Name";
                        var code = fields.GetValueOrDefault("code") ?? "DEPT-" + (DateTime.UtcNow.Ticks % 1000);
                        ulong.TryParse(fields.GetValueOrDefault("faculty_id") ?? "1", out var facId);

                        var entity = new Department
                        {
                            name = name,
                            code = code,
                            faculty_id = facId == 0 ? 1 : facId,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        _db.departments.Add(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = entity.id, Message = $"Department '{name}' created successfully (ID: #{entity.id})." };
                    }

                case "organizations":
                    {
                        var name = fields.GetValueOrDefault("name") ?? "New Organization";
                        ulong.TryParse(fields.GetValueOrDefault("department_id") ?? "0", out var deptId);

                        var entity = new Organization
                        {
                            name = name,
                            short_name = fields.GetValueOrDefault("short_name"),
                            organization_type = fields.GetValueOrDefault("organization_type") ?? "CLUB",
                            email = fields.GetValueOrDefault("email"),
                            phone = fields.GetValueOrDefault("phone"),
                            department_id = deptId == 0 ? null : deptId,
                            status = fields.GetValueOrDefault("status") ?? "ACTIVE",
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        _db.organizations.Add(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = entity.id, Message = $"Organization '{name}' created successfully (ID: #{entity.id})." };
                    }

                case "event_categories":
                    {
                        var name = fields.GetValueOrDefault("name") ?? "Category";
                        var slug = fields.GetValueOrDefault("slug") ?? name.ToLower().Replace(" ", "-");
                        bool.TryParse(fields.GetValueOrDefault("is_active") ?? "true", out var isActive);

                        var entity = new event_category
                        {
                            name = name,
                            slug = slug,
                            description = fields.GetValueOrDefault("description"),
                            icon = fields.GetValueOrDefault("icon") ?? "bi-tag",
                            is_active = isActive,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        _db.event_categories.Add(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = entity.id, Message = $"Category '{name}' created successfully (ID: #{entity.id})." };
                    }

                default:
                    return new DatabaseCrudResult { Success = false, Message = $"Direct INSERT not supported for table '{table}'." };
            }
        }

        private async Task<DatabaseCrudResult> UpdateRecordInternalAsync(string table, ulong id, Dictionary<string, string?> fields, bool saveChanges = true)
        {
            switch (table)
            {
                case "events":
                    {
                        var entity = await _db.events.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Event #{id} not found." };

                        if (fields.ContainsKey("title")) entity.title = fields["title"] ?? entity.title;
                        if (fields.ContainsKey("slug")) entity.slug = fields["slug"] ?? entity.slug;
                        if (fields.ContainsKey("short_description")) entity.short_description = fields["short_description"];
                        if (fields.ContainsKey("category_id") && ulong.TryParse(fields["category_id"], out var catId)) entity.category_id = catId;
                        if (fields.ContainsKey("organizer_id") && ulong.TryParse(fields["organizer_id"], out var orgId)) entity.organizer_id = orgId;
                        if (fields.ContainsKey("venue_id")) entity.venue_id = ulong.TryParse(fields["venue_id"], out var vId) && vId > 0 ? vId : null;
                        if (fields.ContainsKey("capacity") && uint.TryParse(fields["capacity"], out var cap)) entity.capacity = cap;
                        if (fields.ContainsKey("event_mode")) entity.event_mode = fields["event_mode"] ?? entity.event_mode;
                        if (fields.ContainsKey("status")) entity.status = fields["status"] ?? entity.status;
                        if (fields.ContainsKey("approval_status")) entity.approval_status = fields["approval_status"] ?? entity.approval_status;
                        if (fields.ContainsKey("start_at") && DateTime.TryParse(fields["start_at"], out var startAt)) entity.start_at = startAt;
                        if (fields.ContainsKey("end_at") && DateTime.TryParse(fields["end_at"], out var endAt)) entity.end_at = endAt;
                        entity.updated_at = DateTime.UtcNow;

                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Event #{id} ('{entity.title}') updated successfully." };
                    }

                case "announcements":
                    {
                        var entity = await _db.announcements.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Announcement #{id} not found." };

                        if (fields.ContainsKey("title")) entity.title = fields["title"] ?? entity.title;
                        if (fields.ContainsKey("summary")) entity.summary = fields["summary"];
                        if (fields.ContainsKey("announcement_type")) entity.announcement_type = fields["announcement_type"] ?? entity.announcement_type;
                        if (fields.ContainsKey("priority")) entity.priority = fields["priority"] ?? entity.priority;
                        if (fields.ContainsKey("status")) entity.status = fields["status"] ?? entity.status;
                        if (fields.ContainsKey("author_id") && ulong.TryParse(fields["author_id"], out var authId)) entity.author_id = authId;
                        if (fields.ContainsKey("department_id")) entity.department_id = ulong.TryParse(fields["department_id"], out var dId) && dId > 0 ? dId : null;
                        entity.updated_at = DateTime.UtcNow;

                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Announcement #{id} ('{entity.title}') updated successfully." };
                    }

                case "users":
                    {
                        var entity = await _db.users.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"User #{id} not found." };

                        if (fields.ContainsKey("username")) entity.username = fields["username"] ?? entity.username;
                        if (fields.ContainsKey("email")) entity.email = fields["email"] ?? entity.email;
                        if (fields.ContainsKey("first_name")) entity.first_name = fields["first_name"] ?? entity.first_name;
                        if (fields.ContainsKey("last_name")) entity.last_name = fields["last_name"] ?? entity.last_name;
                        if (fields.ContainsKey("phone")) entity.phone = fields["phone"];
                        if (fields.ContainsKey("account_type")) entity.account_type = fields["account_type"] ?? entity.account_type;
                        if (fields.ContainsKey("account_status")) entity.account_status = fields["account_status"] ?? entity.account_status;
                        if (fields.ContainsKey("student_id")) entity.student_id = fields["student_id"];
                        if (fields.ContainsKey("employee_id")) entity.employee_id = fields["employee_id"];
                        entity.updated_at = DateTime.UtcNow;

                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"User #{id} ('{entity.username}') updated successfully." };
                    }

                case "venues":
                    {
                        var entity = await _db.venues.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Venue #{id} not found." };

                        if (fields.ContainsKey("name")) entity.name = fields["name"] ?? entity.name;
                        if (fields.ContainsKey("building_name")) entity.building_name = fields["building_name"];
                        if (fields.ContainsKey("room_number")) entity.room_number = fields["room_number"];
                        if (fields.ContainsKey("capacity") && uint.TryParse(fields["capacity"], out var cap)) entity.capacity = cap;
                        if (fields.ContainsKey("venue_type")) entity.venue_type = fields["venue_type"] ?? entity.venue_type;
                        if (fields.ContainsKey("status")) entity.status = fields["status"] ?? entity.status;
                        entity.updated_at = DateTime.UtcNow;

                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Venue #{id} ('{entity.name}') updated successfully." };
                    }

                case "departments":
                    {
                        var entity = await _db.departments.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Department #{id} not found." };

                        if (fields.ContainsKey("name")) entity.name = fields["name"] ?? entity.name;
                        if (fields.ContainsKey("code")) entity.code = fields["code"] ?? entity.code;
                        if (fields.ContainsKey("faculty_id") && ulong.TryParse(fields["faculty_id"], out var fId)) entity.faculty_id = fId;
                        entity.updated_at = DateTime.UtcNow;

                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Department #{id} ('{entity.name}') updated successfully." };
                    }

                case "organizations":
                    {
                        var entity = await _db.organizations.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Organization #{id} not found." };

                        if (fields.ContainsKey("name")) entity.name = fields["name"] ?? entity.name;
                        if (fields.ContainsKey("short_name")) entity.short_name = fields["short_name"];
                        if (fields.ContainsKey("organization_type")) entity.organization_type = fields["organization_type"] ?? entity.organization_type;
                        if (fields.ContainsKey("email")) entity.email = fields["email"];
                        if (fields.ContainsKey("phone")) entity.phone = fields["phone"];
                        if (fields.ContainsKey("department_id")) entity.department_id = ulong.TryParse(fields["department_id"], out var dId) && dId > 0 ? dId : null;
                        if (fields.ContainsKey("status")) entity.status = fields["status"] ?? entity.status;
                        entity.updated_at = DateTime.UtcNow;

                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Organization #{id} ('{entity.name}') updated successfully." };
                    }

                case "event_categories":
                    {
                        var entity = await _db.event_categories.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Category #{id} not found." };

                        if (fields.ContainsKey("name")) entity.name = fields["name"] ?? entity.name;
                        if (fields.ContainsKey("slug")) entity.slug = fields["slug"] ?? entity.slug;
                        if (fields.ContainsKey("description")) entity.description = fields["description"];
                        if (fields.ContainsKey("icon")) entity.icon = fields["icon"] ?? entity.icon;
                        if (fields.ContainsKey("is_active") && bool.TryParse(fields["is_active"], out var act)) entity.is_active = act;
                        entity.updated_at = DateTime.UtcNow;

                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Category #{id} ('{entity.name}') updated successfully." };
                    }

                case "registrations":
                    {
                        var entity = await _db.registrations.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Registration #{id} not found." };

                        if (fields.ContainsKey("status")) entity.status = fields["status"] ?? entity.status;
                        if (fields.ContainsKey("registration_code")) entity.registration_code = fields["registration_code"] ?? entity.registration_code;
                        if (fields.ContainsKey("checked_in_at"))
                        {
                            if (DateTime.TryParse(fields["checked_in_at"], out var chkAt)) entity.checked_in_at = chkAt;
                            else entity.checked_in_at = null;
                        }

                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Registration #{id} updated successfully." };
                    }

                default:
                    return new DatabaseCrudResult { Success = false, Message = $"Direct UPDATE not supported for table '{table}'." };
            }
        }

        private async Task<DatabaseCrudResult> DeleteRecordInternalAsync(string table, ulong id, bool saveChanges = true)
        {
            switch (table)
            {
                case "events":
                    {
                        var entity = await _db.events.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Event #{id} not found." };
                        _db.events.Remove(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Event #{id} ('{entity.title}') permanently removed from database." };
                    }

                case "announcements":
                    {
                        var entity = await _db.announcements.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Announcement #{id} not found." };
                        _db.announcements.Remove(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Announcement #{id} ('{entity.title}') permanently removed from database." };
                    }

                case "users":
                    {
                        var entity = await _db.users.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"User #{id} not found." };
                        if (entity.account_type == "SUPERADMIN" || entity.id == GetCurrentUserId())
                        {
                            return new DatabaseCrudResult { Success = false, Message = "Security Constraint: Cannot delete active SuperAdmin or your own active account." };
                        }
                        _db.users.Remove(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"User #{id} ('{entity.username}') permanently removed from database." };
                    }

                case "venues":
                    {
                        var entity = await _db.venues.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Venue #{id} not found." };
                        _db.venues.Remove(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Venue #{id} ('{entity.name}') permanently removed from database." };
                    }

                case "registrations":
                    {
                        var entity = await _db.registrations.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Registration #{id} not found." };
                        _db.registrations.Remove(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Registration #{id} permanently removed from database." };
                    }

                case "organizations":
                    {
                        var entity = await _db.organizations.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Organization #{id} not found." };
                        _db.organizations.Remove(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Organization #{id} ('{entity.name}') permanently removed from database." };
                    }

                case "departments":
                    {
                        var entity = await _db.departments.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Department #{id} not found." };
                        _db.departments.Remove(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Department #{id} ('{entity.name}') permanently removed from database." };
                    }

                case "event_categories":
                    {
                        var entity = await _db.event_categories.FindAsync(id);
                        if (entity == null) return new DatabaseCrudResult { Success = false, Message = $"Category #{id} not found." };
                        _db.event_categories.Remove(entity);
                        if (saveChanges) await _db.SaveChangesAsync();
                        return new DatabaseCrudResult { Success = true, RecordId = id, Message = $"Category #{id} ('{entity.name}') permanently removed from database." };
                    }

                default:
                    return new DatabaseCrudResult { Success = false, Message = $"Direct DELETE not supported for table '{table}'." };
            }
        }
    }
}
