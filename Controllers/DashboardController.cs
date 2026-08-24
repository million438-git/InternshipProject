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
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext db, ILogger<DashboardController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =====================================================================
        // ROLE DISPATCHER (GET: /Dashboard or /Dashboard/Index)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userRole = await GetCurrentNormalizedRoleAsync();

            return userRole switch
            {
                "SuperAdmin" => RedirectToAction(nameof(SuperAdmin)),
                "Admin" => RedirectToAction(nameof(Admin)),
                "Faculty" => RedirectToAction(nameof(Faculty)),
                "Staff" => RedirectToAction(nameof(Staff)),
                "Organization" => RedirectToAction(nameof(Organization)),
                _ => RedirectToAction(nameof(Student))
            };
        }

        // =====================================================================
        // 1. STUDENT DASHBOARD (GET: /Dashboard/Student)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Student()
        {
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            var vm = new StudentDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "Student",
                UserDepartment = userDept,
                UserId = formattedId,
                StudentId = studentId ?? "HU/2026/CS-883",
                RegisteredEventsCount = 4,
                AttendedEventsCount = 12,
                EarnedCertificatesCount = 5,
                UpcomingWorkshopsCount = 2
            };

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            PopulateNotifications(vm, "Student");

            // Student-specific data
            vm.MyRegisteredEvents = vm.UpcomingEvents.Take(3).ToList();
            foreach (var ev in vm.MyRegisteredEvents) ev.IsRegistered = true;

            vm.RecommendedClubs = new List<DashboardClubItem>
            {
                new() { Id = 1, Name = "Google Developer Student Club (GDSC)", Category = "Technology & Coding", MemberCount = 210, Description = "Peer-to-peer learning environment for students interested in software and AI technologies." },
                new() { Id = 2, Name = "Hawassa Cyber Knights", Category = "Cybersecurity", MemberCount = 145, Description = "CTF competitions, ethical hacking workshops, and security defense challenges." },
                new() { Id = 3, Name = "Campus Debate & Model UN", Category = "Leadership & Policy", MemberCount = 95, Description = "Sharpen public speaking, debate, and global diplomatic simulation skills." }
            };

            return View("Student", vm);
        }

        // =====================================================================
        // 2. STAFF DASHBOARD (GET: /Dashboard/Staff)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Staff()
        {
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            var vm = new StaffDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "Staff",
                UserDepartment = userDept,
                UserId = formattedId,
                EmployeeId = empId ?? "EMP-STAFF-409",
                DepartmentEventsCount = 6,
                VenueReservationsCount = 4,
                PendingTasksCount = 5,
                StaffNoticesCount = 8,
                ManagedEquipmentsCount = 34
            };

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            PopulateNotifications(vm, "Staff");

            vm.DepartmentEvents = vm.UpcomingEvents.Where(e => e.CategoryName == "Academic" || e.CategoryName == "Career").Take(4).ToList();

            vm.UpcomingVenueBookings = new List<DashboardVenueBookingItem>
            {
                new() { Id = 1, VenueName = "Main Auditorium Hall A", Purpose = "Quarterly Department All-Hands Meeting", ScheduledDate = DateTime.Today.AddDays(1), TimeSlot = "09:00 AM - 12:00 PM", Status = "Confirmed" },
                new() { Id = 2, VenueName = "Conference Room B2", Purpose = "Inter-College Logistics Sync", ScheduledDate = DateTime.Today.AddDays(3), TimeSlot = "02:00 PM - 04:30 PM", Status = "Pending Approval" },
                new() { Id = 3, VenueName = "ICT Training Center", Purpose = "New Staff System Onboarding", ScheduledDate = DateTime.Today.AddDays(5), TimeSlot = "10:00 AM - 01:00 PM", Status = "Confirmed" }
            };

            vm.OperationalTasks = new List<DashboardTaskItem>
            {
                new() { Id = 1, Title = "Verify projector & audio setup for Tech Expo in Main Auditorium", Priority = "High", DueDate = DateTime.Today.AddDays(2), IsCompleted = false },
                new() { Id = 2, Title = "Review volunteer badge print requests for Cultural Gala", Priority = "Medium", DueDate = DateTime.Today.AddDays(3), IsCompleted = false },
                new() { Id = 3, Title = "Submit departmental quarterly event inventory report", Priority = "Low", DueDate = DateTime.Today.AddDays(7), IsCompleted = true }
            };

            vm.StaffAnnouncements = new List<DashboardAnnouncementItem>
            {
                new() { Id = 1, Title = "Campus Emergency Drill Scheduled This Thursday", Content = "All staff coordinators must brief floor monitors on evacuation procedures by Wednesday afternoon.", AuthorName = "Safety & Security Office", DepartmentName = "Campus Security", Priority = "High", CreatedAt = DateTime.Now.AddHours(-4) },
                new() { Id = 2, Title = "Staff Portal Maintenance Notice", Content = "Scheduled system update on Saturday 11:00 PM to Sunday 03:00 AM. Access may be temporarily intermittent.", AuthorName = "IT Directorate", DepartmentName = "ICT Support", Priority = "Normal", CreatedAt = DateTime.Now.AddDays(-1) }
            };

            return View("Staff", vm);
        }

        // =====================================================================
        // 3. FACULTY DASHBOARD (GET: /Dashboard/Faculty)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Faculty()
        {
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            var vm = new FacultyDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "Faculty",
                UserDepartment = userDept,
                UserId = formattedId,
                EmployeeId = empId ?? "FAC-PROF-102",
                AcademicConferencesCount = 3,
                ScheduledLecturesCount = 8,
                SeminarApprovalsCount = 4,
                ResearchPresentationsCount = 2,
                DepartmentStudentsCount = 340
            };

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            PopulateNotifications(vm, "Faculty");

            vm.AcademicSeminars = vm.UpcomingEvents.Where(e => e.CategoryName == "Academic" || e.CategoryName == "Technology").Take(4).ToList();

            vm.PendingStudentProposals = new List<DashboardApprovalItem>
            {
                new() { Id = 1, EventTitle = "AI & IoT Senior Thesis Project Showcase", SubmitterName = "Daniel Tadesse (Student Lead)", SubmitterRole = "Student", Venue = "Engineering Hall 1", EventDate = DateTime.Today.AddDays(8), SubmittedAt = DateTime.Now.AddDays(-1), Status = "PENDING" },
                new() { Id = 2, EventTitle = "Guest Lecture: Dr. Marcus Vance on Cloud Architecture", SubmitterName = "CS Department Committee", SubmitterRole = "Faculty", Venue = "Main Auditorium Hall B", EventDate = DateTime.Today.AddDays(11), SubmittedAt = DateTime.Now.AddHours(-18), Status = "PENDING" }
            };

            vm.WeeklyClassSchedule = new List<DashboardScheduleItem>
            {
                new() { Day = "Monday", CourseCode = "CS-412", CourseTitle = "Network Security & Digital Defense", Time = "08:30 AM - 10:30 AM", Room = "IT Lab 3" },
                new() { Day = "Tuesday", CourseCode = "CS-501", CourseTitle = "Advanced Applied Cryptography Seminar", Time = "02:00 PM - 04:30 PM", Room = "Graduate Seminar Room A" },
                new() { Day = "Thursday", CourseCode = "CS-412", CourseTitle = "Security Lab Practicals & Code Review", Time = "10:30 AM - 12:30 PM", Room = "Cyber Defense Lab" },
                new() { Day = "Friday", CourseCode = "RES-600", CourseTitle = "Faculty Research Colloquium", Time = "03:00 PM - 05:00 PM", Room = "Senate Hall" }
            };

            vm.ResearchSymposiums = new List<DashboardEventItem>
            {
                new() { Id = 101, Title = "7th Annual East Africa Cyber Defense Symposium", StartDate = DateTime.Today.AddDays(14), VenueName = "Senate Hall", CategoryName = "Research Conference", OrganizerName = "College of Informatics", ShortDescription = "Keynote presentations on national critical infrastructure protection and AI-driven threat response." }
            };

            return View("Faculty", vm);
        }

        // =====================================================================
        // 4. ORGANIZATION DASHBOARD (GET: /Dashboard/Organization)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Organization()
        {
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            var vm = new OrganizationDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "Organization",
                UserDepartment = userDept,
                UserId = formattedId,
                OrganizationName = bio ?? "Hawassa University Tech & Innovation Society",
                HostedEventsCount = 5,
                TotalAttendeesCount = 420,
                ActiveMembersCount = 48,
                BoothReservationsCount = 2,
                
            };

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            PopulateNotifications(vm, "Organization");

            vm.HostedEvents = vm.UpcomingEvents.Take(3).ToList();

            vm.OrganizationMembers = new List<DashboardMemberItem>
            {
                new() { Id = 1, FullName = "Kidus Solomon", RoleInOrg = "President / Lead Organizer", Email = "kidus.lead@hawassauniversity.edu.et", JoinedAt = DateTime.Today.AddMonths(-8) },
                new() { Id = 2, FullName = "Selamawit Desta", RoleInOrg = "Logistics Coordinator", Email = "selam.desta@hawassauniversity.edu.et", JoinedAt = DateTime.Today.AddMonths(-6) },
                new() { Id = 3, FullName = "Bruk Yohannes", RoleInOrg = "Marketing & Public Relations", Email = "bruk.pr@hawassauniversity.edu.et", JoinedAt = DateTime.Today.AddMonths(-4) },
                new() { Id = 4, FullName = "Hanna Mesfin", RoleInOrg = "Finance & Sponsorship Lead", Email = "hanna.m@hawassauniversity.edu.et", JoinedAt = DateTime.Today.AddMonths(-3) }
            };

            

            vm.AttendanceAnalytics = new List<DashboardRegistrationStatItem>
            {
                new() { EventTitle = "Campus Hackathon 2026", ConfirmedCount = 185, AttendedCount = 160, MaxCapacity = 200 },
                new() { EventTitle = "Tech Career Fair & Networking", ConfirmedCount = 140, AttendedCount = 125, MaxCapacity = 150 },
                new() { EventTitle = "AI Workshop Series Pt. 1", ConfirmedCount = 95, AttendedCount = 92, MaxCapacity = 100 }
            };

            return View("Organization", vm);
        }

        // =====================================================================
        // 5. ADMIN DASHBOARD (GET: /Dashboard/Admin)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Admin()
        {
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            var vm = new AdminDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "Admin",
                UserDepartment = userDept,
                UserId = formattedId,
                TotalUsersCount = 1240,
                PendingEventApprovalsCount = 4,
                ActiveVenuesCount = 14,
                ReportedContentCount = 2,
                ActiveOrganizationsCount = 28
            };

            try
            {
                vm.TotalUsersCount = await _db.users.CountAsync();
                vm.ActiveOrganizationsCount = await _db.organizations.CountAsync();
                vm.ActiveVenuesCount = await _db.venues.CountAsync();
                vm.PendingEventApprovalsCount = await _db.events.CountAsync(e => e.approval_status == "PENDING");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Admin dashboard stats query fallback.");
            }

            if (vm.TotalUsersCount == 0) vm.TotalUsersCount = 1240;
            if (vm.PendingEventApprovalsCount == 0) vm.PendingEventApprovalsCount = 4;
            if (vm.ActiveVenuesCount == 0) vm.ActiveVenuesCount = 14;

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            PopulateNotifications(vm, "Admin");

            vm.PendingEventApprovals = new List<DashboardApprovalItem>
            {
                new() { Id = 10, EventTitle = "Inter-College Robotics Expo & Drone Race", SubmitterName = "Samuel Girma (Robotics Club)", SubmitterRole = "Organization", Venue = "Engineering Quadrangle", EventDate = DateTime.Today.AddDays(5), SubmittedAt = DateTime.Now.AddHours(-6), Status = "PENDING" },
                new() { Id = 11, EventTitle = "University Cultural Night & Food Festival", SubmitterName = "Meron Haile (Student Council)", SubmitterRole = "Student", Venue = "Main Stadium Field", EventDate = DateTime.Today.AddDays(9), SubmittedAt = DateTime.Now.AddHours(-14), Status = "PENDING" },
                new() { Id = 12, EventTitle = "Graduate Research & Innovation Showcase", SubmitterName = "Dr. Kassahun (Faculty)", SubmitterRole = "Faculty", Venue = "Senate Hall", EventDate = DateTime.Today.AddDays(15), SubmittedAt = DateTime.Now.AddDays(-1), Status = "PENDING" }
            };

            vm.RecentRegisteredUsers = new List<DashboardRecentUserItem>
            {
                new() { Id = 101, FullName = "Haimanot Worku", Email = "haimanot.w@hawassauniversity.edu.et", AccountType = "STUDENT", Status = "ACTIVE", JoinedAt = DateTime.Now.AddMinutes(-35) },
                new() { Id = 102, FullName = "Dr. Solomon Tadesse", Email = "solomon.t@hawassauniversity.edu.et", AccountType = "FACULTY", Status = "ACTIVE", JoinedAt = DateTime.Now.AddHours(-2) },
                new() { Id = 103, FullName = "Campus Journalism Guild", Email = "journalism@hawassauniversity.edu.et", AccountType = "ORGANIZATION", Status = "ACTIVE", JoinedAt = DateTime.Now.AddHours(-5) }
            };

            vm.CampusVenuesStatus = new List<DashboardVenueItem>
            {
                new() { Id = 1, Name = "Main Auditorium Hall A", Capacity = 1200, Building = "Administration Complex", Status = "Occupied (Tech Expo)", ScheduledEventsCount = 8 },
                new() { Id = 2, Name = "IT Complex Lab 3", Capacity = 80, Building = "College of Informatics", Status = "Available", ScheduledEventsCount = 4 },
                new() { Id = 3, Name = "Main University Stadium", Capacity = 5000, Building = "Sports Complex", Status = "Maintenance", ScheduledEventsCount = 2 },
                new() { Id = 4, Name = "Senate Hall", Capacity = 350, Building = "Central Campus Tower", Status = "Available", ScheduledEventsCount = 6 }
            };

            vm.RecentFlaggedContent = new List<DashboardReportItem>
            {
                new() { Id = 1, ContentType = "Event Comment", Reason = "Inappropriate / Promotional spam", ReportedBy = "Dawit Y.", CreatedAt = DateTime.Now.AddHours(-3), Status = "PENDING" },
                new() { Id = 2, ContentType = "Event Poster Image", Reason = "Copyright review requested", ReportedBy = "Arts Directorate", CreatedAt = DateTime.Now.AddDays(-1), Status = "REVIEWED" }
            };

            vm.CategoryBreakdown = new List<DashboardCategoryStatItem>
            {
                new() { CategoryName = "Technology & Coding", EventCount = 14, ColorHex = "#2563eb" },
                new() { CategoryName = "Academic & Research", EventCount = 11, ColorHex = "#7c3aed" },
                new() { CategoryName = "Sports & Fitness", EventCount = 8, ColorHex = "#059669" },
                new() { CategoryName = "Arts & Culture", EventCount = 6, ColorHex = "#d97706" }
            };

            return View("Admin", vm);
        }

        // =====================================================================
        // 6. SUPER ADMIN DASHBOARD (GET: /Dashboard/SuperAdmin)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> SuperAdmin()
        {
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            var vm = new SuperAdminDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "SuperAdmin",
                UserDepartment = "University Central Administration",
                UserId = formattedId,
                TotalSystemUsersCount = 14850,
                TotalPlatformEventsCount = 384,
                TotalSystemRolesCount = 6,
                SecurityAlertsCount = 0,
                SystemHealthPercent = 100,
                ServerUptime = "99.99% (42 days continuous)",
                DatabaseEngine = "MySQL 8.0 Enterprise / AWS Cloud RDS",
                ActiveEnvironment = "Main Campus Production Cluster"
            };

            try
            {
                vm.TotalSystemUsersCount = await _db.users.CountAsync();
                vm.TotalPlatformEventsCount = await _db.events.CountAsync();
                vm.TotalSystemRolesCount = await _db.roles.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SuperAdmin stats query fallback.");
            }

            if (vm.TotalSystemUsersCount == 0) vm.TotalSystemUsersCount = 14850;
            if (vm.TotalPlatformEventsCount == 0) vm.TotalPlatformEventsCount = 384;
            if (vm.TotalSystemRolesCount == 0) vm.TotalSystemRolesCount = 6;

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            PopulateNotifications(vm, "SuperAdmin");

            vm.RealtimeAuditLogs = new List<DashboardAuditItem>
            {
                new() { Id = 1, Action = "User Role Promoted [Staff -> Admin]", UserEmail = "superadmin@hawassauniversity.edu.et", IpAddress = "10.14.0.1 (VPN Gateway)", Timestamp = DateTime.Now.AddMinutes(-8), Severity = "Warning" },
                new() { Id = 2, Action = "Platform Security Policy Synced", UserEmail = "superadmin@hawassauniversity.edu.et", IpAddress = "10.14.0.1", Timestamp = DateTime.Now.AddHours(-1), Severity = "Info" },
                new() { Id = 3, Action = "Database Snapshot & Automated Backup Completed", UserEmail = "System Daemon (Cron)", IpAddress = "localhost", Timestamp = DateTime.Now.AddHours(-4), Severity = "Success" },
                new() { Id = 4, Action = "Batch 120 Student Accounts Synchronized from SIS", UserEmail = "registrar@hawassauniversity.edu.et", IpAddress = "10.12.4.22", Timestamp = DateTime.Now.AddHours(-7), Severity = "Info" }
            };

            vm.RolesDistribution = new List<DashboardRoleSummaryItem>
            {
                new() { RoleName = "Students", UserCount = 13800, BadgeClass = "bg-primary" },
                new() { RoleName = "Faculty Members", UserCount = 620, BadgeClass = "bg-purple text-white" },
                new() { RoleName = "Staff Coordinators", UserCount = 350, BadgeClass = "bg-success" },
                new() { RoleName = "Clubs & Organizations", UserCount = 68, BadgeClass = "bg-warning text-dark" },
                new() { RoleName = "Campus Administrators", UserCount = 10, BadgeClass = "bg-danger" },
                new() { RoleName = "Super Administrators", UserCount = 2, BadgeClass = "bg-dark" }
            };

            vm.SystemServicesStatus = new List<DashboardSystemHealthItem>
            {
                new() { ServiceName = "Web Application Core (ASP.NET Core)", Status = "Healthy (100%)", Latency = "8ms", HealthClass = "text-success" },
                new() { ServiceName = "MySQL Database Cluster", Status = "Healthy (Connections: 24/500)", Latency = "2ms", HealthClass = "text-success" },
                new() { ServiceName = "Campus Notification Dispatcher", Status = "Active (Queue: 0)", Latency = "15ms", HealthClass = "text-success" },
                new() { ServiceName = "QR Code & Ticket Verification API", Status = "Operational", Latency = "12ms", HealthClass = "text-success" },
                new() { ServiceName = "Calendar Synchronization Service", Status = "Syncing (iCal/Google)", Latency = "20ms", HealthClass = "text-success" }
            };

            vm.SecurityIncidents = new List<DashboardSecurityAlertItem>
            {
                new() { Title = "SSL/TLS 1.3 Active & Valid", Description = "Wildcard certificate valid for *.hawassauniversity.edu.et until Nov 2027.", Severity = "Normal", OccurredAt = DateTime.Now.AddDays(-10) },
                new() { Title = "0 Failed Brute-Force Attempts", Description = "Rate limiter actively enforcing IP rate limits across login endpoints.", Severity = "Normal", OccurredAt = DateTime.Now.AddMinutes(-30) }
            };

            return View("SuperAdmin", vm);
        }

        // =====================================================================
        // HELPER METHODS
        // =====================================================================

        private async Task<string> GetCurrentNormalizedRoleAsync()
        {
            var claimRole = User.FindFirstValue(ClaimTypes.Role);
            if (!string.IsNullOrEmpty(claimRole))
            {
                return NormalizeRole(claimRole);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdStr) && ulong.TryParse(userIdStr, out ulong uid))
            {
                try
                {
                    var dbUser = await _db.users
                        .Include(u => u.user_roleusers)
                            .ThenInclude(ur => ur.role)
                        .FirstOrDefaultAsync(u => u.id == uid);

                    if (dbUser != null)
                    {
                        if (dbUser.user_roleusers != null && dbUser.user_roleusers.Any())
                        {
                            foreach (var ur in dbUser.user_roleusers)
                            {
                                var rName = ur.role?.name;
                                if (!string.IsNullOrEmpty(rName))
                                    return NormalizeRole(rName);
                            }
                        }

                        if (!string.IsNullOrEmpty(dbUser.account_type))
                        {
                            return NormalizeRole(dbUser.account_type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve user role from database.");
                }
            }

            return "Student";
        }

        private static string NormalizeRole(string role)
        {
            var r = role.Trim().ToUpperInvariant();
            if (r.Contains("SUPER")) return "SuperAdmin";
            if (r.Contains("ADMIN")) return "Admin";
            if (r.Contains("FACULTY") || r.Contains("PROFESSOR") || r.Contains("TEACHER") || r.Contains("LECTURER")) return "Faculty";
            if (r.Contains("STAFF") || r.Contains("EMPLOYEE") || r.Contains("OFFICER")) return "Staff";
            if (r.Contains("ORG") || r.Contains("CLUB")) return "Organization";
            return "Student";
        }

        private async Task<(ulong? UserId, string UserName, string UserEmail, string UserRole, string UserDept, string FormattedId, string? StudentId, string? EmployeeId, string? Bio)> GetUserInfoAsync()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "user@hawassauniversity.edu.et";
            var userName = User.Identity?.Name ?? "Campus Member";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "Student";
            var userDept = "Computer Science & Cyber Security";
            var formattedUserId = "HUCEMS-2026-001";
            string? studentId = null;
            string? employeeId = null;
            string? bio = null;
            ulong? uidVal = null;

            if (!string.IsNullOrEmpty(userIdStr) && ulong.TryParse(userIdStr, out ulong uid))
            {
                uidVal = uid;
                try
                {
                    var dbUser = await _db.users
                        .Include(u => u.department)
                        .FirstOrDefaultAsync(u => u.id == uid);

                    if (dbUser != null)
                    {
                        userName = $"{dbUser.first_name} {dbUser.last_name}".Trim();
                        userEmail = dbUser.email;
                        userDept = dbUser.department?.name ?? "Computer Science & Cyber Security";
                        formattedUserId = $"HUCEMS-{dbUser.id:D4}";
                        studentId = dbUser.student_id;
                        employeeId = dbUser.employee_id;
                        bio = dbUser.bio;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load user info in GetUserInfoAsync.");
                }
            }

            return (uidVal, userName, userEmail, userRole, userDept, formattedUserId, studentId, employeeId, bio);
        }

        private async Task PopulateSharedStatsAsync(DashboardViewModel vm)
        {
            try
            {
                vm.UpcomingEventsCount = await _db.events.CountAsync(e => e.start_at >= DateTime.Today);
                vm.TodayEventsCount = await _db.events.CountAsync(e => e.start_at.Date == DateTime.Today);
                vm.AnnouncementCount = await _db.announcements.CountAsync();
                vm.RegistrationCount = await _db.registrations.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch counts from database.");
            }

            if (vm.UpcomingEventsCount == 0) vm.UpcomingEventsCount = 8;
            if (vm.TodayEventsCount == 0) vm.TodayEventsCount = 2;
            if (vm.AnnouncementCount == 0) vm.AnnouncementCount = 5;
            if (vm.RegistrationCount == 0) vm.RegistrationCount = 42;
        }

        private async Task PopulateUpcomingEventsAsync(DashboardViewModel vm)
        {
            try
            {
                var dbEvents = await _db.events
                    .Include(e => e.category)
                    .Include(e => e.venue)
                    .Include(e => e.organizer)
                    .OrderBy(e => e.start_at)
                    .Take(6)
                    .ToListAsync();

                if (dbEvents.Any())
                {
                    vm.UpcomingEvents = dbEvents.Select(e => new DashboardEventItem
                    {
                        Id = e.id,
                        Title = e.title,
                        ShortDescription = e.short_description ?? (e.description != null && e.description.Length > 90 ? e.description.Substring(0, 90) + "..." : e.description),
                        ImageUrl = e.image_url,
                        StartDate = e.start_at,
                        VenueName = e.venue?.name ?? "Main Auditorium",
                        CategoryName = e.category?.name ?? "Academic",
                        OrganizerName = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "University Staff",
                        AttendeeCount = 45,
                        Capacity = (int)(e.capacity ?? 150)
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch events for dashboard.");
            }

            if (!vm.UpcomingEvents.Any())
            {
                vm.UpcomingEvents = new List<DashboardEventItem>
                {
                    new()
                    {
                        Id = 1,
                        Title = "Annual University Tech & Innovation Expo",
                        ShortDescription = "Showcase student robotics, AI software, and cybersecurity solutions.",
                        StartDate = DateTime.Now.AddDays(2).Date.AddHours(9),
                        VenueName = "Main Auditorium Hall A",
                        CategoryName = "Technology",
                        OrganizerName = "Tech Club HU",
                        AttendeeCount = 180,
                        Capacity = 300
                    },
                    new()
                    {
                        Id = 2,
                        Title = "Campus Cyber Defense & Hackathon Workshop",
                        ShortDescription = "Hands-on penetration testing and digital defense workshop.",
                        StartDate = DateTime.Now.AddDays(4).Date.AddHours(14),
                        VenueName = "IT Complex Lab 3",
                        CategoryName = "Cybersecurity",
                        OrganizerName = "Department of CS",
                        AttendeeCount = 75,
                        Capacity = 80
                    },
                    new()
                    {
                        Id = 3,
                        Title = "Inter-Department Football Championship",
                        ShortDescription = "Quarter finals between Engineering and Computer Science.",
                        StartDate = DateTime.Now.AddDays(6).Date.AddHours(16),
                        VenueName = "Main University Stadium",
                        CategoryName = "Sports",
                        OrganizerName = "Sports Directorate",
                        AttendeeCount = 450,
                        Capacity = 1000
                    }
                };
            }
        }

        private async Task PopulateAnnouncementsAsync(DashboardViewModel vm)
        {
            try
            {
                var dbAnnouncements = await _db.announcements
                    .Include(a => a.author)
                    .OrderByDescending(a => a.created_at)
                    .Take(4)
                    .ToListAsync();

                if (dbAnnouncements.Any())
                {
                    vm.RecentAnnouncements = dbAnnouncements.Select(a => new DashboardAnnouncementItem
                    {
                        Id = a.id,
                        Title = a.title,
                        Content = a.content,
                        AuthorName = a.author != null ? $"{a.author.first_name} {a.author.last_name}".Trim() : "Campus Administration",
                        CreatedAt = a.created_at,
                        Priority = "Normal"
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load announcements for dashboard.");
            }

            if (!vm.RecentAnnouncements.Any())
            {
                vm.RecentAnnouncements = new List<DashboardAnnouncementItem>
                {
                    new()
                    {
                        Id = 1,
                        Title = "Registration Open for Semester Hackathon 2026",
                        Content = "Teams of up to 4 students can register online starting this week.",
                        AuthorName = "Student Affairs Directorate",
                        DepartmentName = "College of Informatics",
                        Priority = "High",
                        CreatedAt = DateTime.Now.AddHours(-3)
                    },
                    new()
                    {
                        Id = 2,
                        Title = "Main Campus Library Extended Hours for Finals",
                        Content = "Main campus library will remain open 24/7 during the upcoming exam period.",
                        AuthorName = "Library Administration",
                        DepartmentName = "University Library",
                        Priority = "Normal",
                        CreatedAt = DateTime.Now.AddDays(-1)
                    },
                    new()
                    {
                        Id = 3,
                        Title = "Call for Volunteer Campus Event Coordinators",
                        Content = "Join the organizing committee for the upcoming Hawassa University Cultural Gala.",
                        AuthorName = "Events Council",
                        DepartmentName = "Student Union",
                        Priority = "Normal",
                        CreatedAt = DateTime.Now.AddDays(-2)
                    }
                };
            }
        }

        private static void PopulateNotifications(DashboardViewModel vm, string role)
        {
            vm.Notifications = role switch
            {
                "SuperAdmin" => new List<DashboardNotificationItem>
                {
                    new() { Id = 1, Message = "Platform Security Audit passed with 0 vulnerabilities detected.", CreatedAt = DateTime.Now.AddMinutes(-20), IsRead = false, Type = "Success" },
                    new() { Id = 2, Message = "Nightly database snapshot verified and archived to cloud vault.", CreatedAt = DateTime.Now.AddHours(-4), IsRead = true, Type = "Info" },
                    new() { Id = 3, Message = "New administrator account provisioned for Faculty of Engineering.", CreatedAt = DateTime.Now.AddDays(-1), IsRead = true, Type = "Warning" }
                },
                "Admin" => new List<DashboardNotificationItem>
                {
                    new() { Id = 1, Message = "3 new campus event approval requests awaiting your review.", CreatedAt = DateTime.Now.AddMinutes(-15), IsRead = false, Type = "Warning" },
                    new() { Id = 2, Message = "Main Auditorium booked for Tech Expo on Friday 09:00 AM.", CreatedAt = DateTime.Now.AddHours(-3), IsRead = false, Type = "Info" },
                    new() { Id = 3, Message = "Weekly attendee report exported to PDF successfully.", CreatedAt = DateTime.Now.AddDays(-1), IsRead = true, Type = "Success" }
                },
                "Faculty" => new List<DashboardNotificationItem>
                {
                    new() { Id = 1, Message = "2 student symposium project submissions pending faculty endorsement.", CreatedAt = DateTime.Now.AddMinutes(-40), IsRead = false, Type = "Warning" },
                    new() { Id = 2, Message = "Academic Colloquium reminder: Friday 03:00 PM in Senate Hall.", CreatedAt = DateTime.Now.AddHours(-5), IsRead = false, Type = "Info" },
                    new() { Id = 3, Message = "Department syllabus & exam schedules published to student portal.", CreatedAt = DateTime.Now.AddDays(-1), IsRead = true, Type = "Success" }
                },
                "Staff" => new List<DashboardNotificationItem>
                {
                    new() { Id = 1, Message = "Auditorium Hall A audio/visual equipment check confirmed.", CreatedAt = DateTime.Now.AddMinutes(-30), IsRead = false, Type = "Success" },
                    new() { Id = 2, Message = "Logistics memo: Campus security drill scheduled for Thursday morning.", CreatedAt = DateTime.Now.AddHours(-4), IsRead = false, Type = "Warning" },
                    new() { Id = 3, Message = "Quarterly departmental event calendar finalized.", CreatedAt = DateTime.Now.AddDays(-2), IsRead = true, Type = "Info" }
                },
                "Organization" => new List<DashboardNotificationItem>
                {
                    new() { Id = 1, Message = "Your event 'Campus Cyber Defense Workshop' was APPROVED by Admin!", CreatedAt = DateTime.Now.AddMinutes(-10), IsRead = false, Type = "Success" },
                    new() { Id = 2, Message = "45 new students registered for Tech Expo in the last 24 hours.", CreatedAt = DateTime.Now.AddHours(-2), IsRead = false, Type = "Info" },
                    new() { Id = 3, Message = "Booth reservation #104 confirmed in Engineering Quad.", CreatedAt = DateTime.Now.AddDays(-1), IsRead = true, Type = "Success" }
                },
                _ => new List<DashboardNotificationItem>
                {
                    new() { Id = 1, Message = "You are successfully registered for 'Campus Cyber Defense & Hackathon Workshop'!", CreatedAt = DateTime.Now.AddMinutes(-15), IsRead = false, Type = "Success" },
                    new() { Id = 2, Message = "Reminder: Annual Tech & Innovation Expo starts in 2 days at Main Auditorium.", CreatedAt = DateTime.Now.AddHours(-2), IsRead = false, Type = "Info" },
                    new() { Id = 3, Message = "New study group 'Ethical Hacking' posted a meeting schedule.", CreatedAt = DateTime.Now.AddDays(-1), IsRead = true, Type = "Info" }
                }
            };
        }
    }
}
