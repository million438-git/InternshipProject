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
            if (await RestrictDashboardAsync("Student") is { } denied) return denied;
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            int realRegisteredCount = 0;
            List<DashboardEventItem> studentRegisteredEvents = new();

            if (userId.HasValue)
            {
                try
                {
                    realRegisteredCount = await _db.registrations
                        .CountAsync(r => r.user_id == userId.Value && r.status == "REGISTERED");

                    var registeredDbEvents = await _db.registrations
                        .Where(r => r.user_id == userId.Value && r.status == "REGISTERED")
                        .Include(r => r._event)
                            .ThenInclude(e => e.category)
                        .Include(r => r._event)
                            .ThenInclude(e => e.venue)
                        .Include(r => r._event)
                            .ThenInclude(e => e.organizer)
                        .Select(r => r._event)
                        .Where(e => e != null)
                        .OrderBy(e => e.start_at)
                        .ToListAsync();

                    if (registeredDbEvents.Any())
                    {
                        studentRegisteredEvents = registeredDbEvents.Select(e => new DashboardEventItem
                        {
                            Id = e.id,
                            Title = e.title,
                            ShortDescription = e.short_description ?? (e.description != null && e.description.Length > 90 ? e.description.Substring(0, 90) + "..." : e.description),
                            ImageUrl = e.image_url,
                            StartDate = e.start_at,
                            VenueName = e.venue?.name ?? "Main Campus Hall",
                            CategoryName = e.category?.name ?? "Academic",
                            OrganizerName = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "Hawassa University",
                            IsRegistered = true,
                            Capacity = (int)(e.capacity ?? 100)
                        }).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load real student registered events.");
                }
            }

            var vm = new StudentDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "Student",
                UserDepartment = userDept,
                UserId = formattedId,
                StudentId = !string.IsNullOrWhiteSpace(studentId) ? studentId : (string.IsNullOrWhiteSpace(formattedId) ? "Pending Assignment" : formattedId),
                RegisteredEventsCount = realRegisteredCount,
                AttendedEventsCount = userId.HasValue ? await _db.registrations.CountAsync(r => r.user_id == userId.Value && r.status == "ATTENDED") : 0,
                EarnedCertificatesCount = 0,
                UpcomingWorkshopsCount = studentRegisteredEvents.Count(e => e.StartDate >= DateTime.Now)
            };

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            await PopulateNotificationsAsync(vm, userId);

            // Student-specific data
            vm.MyRegisteredEvents = studentRegisteredEvents;

            // Interest-Based Club Recommendations, Department Subscriptions & Personalized Events
            try
            {
                List<ulong> userInterestIds = new();
                List<ulong> userSubscribedDeptIds = new();

                if (userId.HasValue)
                {
                    userInterestIds = await _db.user_category_interests
                        .Where(ui => ui.user_id == userId.Value)
                        .Select(ui => ui.category_id)
                        .ToListAsync();

                    userSubscribedDeptIds = await _db.user_dept_subscriptions
                        .Where(s => s.user_id == userId.Value)
                        .Select(s => s.department_id)
                        .ToListAsync();
                }

                vm.HasSelectedInterests = userInterestIds.Any();
                vm.SelectedInterestsCount = userInterestIds.Count;
                vm.SubscribedDepartmentsCount = userSubscribedDeptIds.Count;

                var dbClubs = await _db.clubs
                    .Include(c => c.club_interests).ThenInclude(ci => ci.category)
                    .Include(c => c.club_followers)
                    .Include(c => c.club_members)
                    .Where(c => c.status == "ACTIVE")
                    .ToListAsync();

                if (dbClubs.Any())
                {
                    var clubItems = dbClubs.Select(c =>
                    {
                        var isFollowing = userId.HasValue && c.club_followers.Any(f => f.user_id == userId.Value);
                        var memberRecord = userId.HasValue ? c.club_members.FirstOrDefault(m => m.user_id == userId.Value) : null;

                        int matchScore = 0;
                        string? reason = null;

                        if (userInterestIds.Any())
                        {
                            var matchingCats = c.club_interests
                                .Where(ci => userInterestIds.Contains(ci.category_id))
                                .Select(ci => ci.category?.name)
                                .Where(n => !string.IsNullOrEmpty(n))
                                .ToList();

                            matchScore = matchingCats.Count;
                            if (matchScore > 0)
                            {
                                reason = $"Because you are interested in {matchingCats.First()}";
                            }
                        }

                        return new
                        {
                            Item = new DashboardClubItem
                            {
                                Id = c.id,
                                Name = c.name,
                                Slug = c.slug,
                                Category = c.club_interests.FirstOrDefault()?.category?.name ?? "General Club",
                                MemberCount = c.club_members.Count(m => m.status == "APPROVED"),
                                FollowerCount = c.club_followers.Count,
                                Description = c.description ?? "Official Hawassa University student club.",
                                LogoUrl = c.logo_url,
                                RecommendationReason = reason,
                                IsFollowing = isFollowing,
                                MembershipStatus = memberRecord != null ? memberRecord.status : "NONE"
                            },
                            Score = matchScore,
                            Followers = c.club_followers.Count,
                            IsFollowed = isFollowing,
                            HasMembership = memberRecord != null
                        };
                    }).ToList();

                    if (vm.HasSelectedInterests)
                    {
                        vm.RecommendedClubs = clubItems
                            .Where(x => x.Score > 0)
                            .OrderByDescending(x => x.Score)
                            .ThenByDescending(x => x.Followers)
                            .Take(4)
                            .Select(x => x.Item)
                            .ToList();
                    }

                    if (!vm.RecommendedClubs.Any())
                    {
                        vm.RecommendedClubs = clubItems
                            .OrderByDescending(x => x.Followers)
                            .Take(4)
                            .Select(x => x.Item)
                            .ToList();
                    }

                    vm.MyClubs = clubItems
                        .Where(x => x.IsFollowed || x.HasMembership)
                        .Select(x => x.Item)
                        .ToList();

                    vm.FollowedClubsCount = clubItems.Count(x => x.IsFollowed);
                }

                // Personalized Events Feed
                var activeEvents = await _db.events
                    .Include(e => e.category)
                    .Include(e => e.venue)
                    .Include(e => e.organizer)
                        .ThenInclude(o => o.department)
                    .Include(e => e.registrations)
                    .Where(e => (e.status == "PUBLISHED" || e.approval_status == "APPROVED") && e.start_at >= DateTime.UtcNow)
                    .OrderBy(e => e.start_at)
                    .Take(20)
                    .ToListAsync();

                if (userInterestIds.Any() || userSubscribedDeptIds.Any())
                {
                    vm.RecommendedEventsForYou = activeEvents
                        .Where(e => userInterestIds.Contains(e.category_id) ||
                                    (e.organizer.department_id.HasValue && userSubscribedDeptIds.Contains(e.organizer.department_id.Value)))
                        .Take(6)
                        .Select(e => new DashboardEventItem
                        {
                            Id = e.id,
                            Title = e.title,
                            CategoryName = e.category?.name ?? "General",
                            VenueName = e.venue?.name ?? (e.organizer.department != null ? $"{e.organizer.department.name} Wing" : "Main Campus"),
                            StartDate = e.start_at,
                            AttendeeCount = e.registrations.Count,
                            Capacity = (int)(e.capacity ?? 100),
                            Status = (e.organizer.department_id.HasValue && userSubscribedDeptIds.Contains(e.organizer.department_id.Value))
                                ? $"From Subscribed: {e.organizer.department?.name ?? "Dept"}"
                                : $"Matches Interest: {e.category?.name ?? "Topic"}"
                        })
                        .ToList();
                }

                if (userSubscribedDeptIds.Any())
                {
                    vm.SubscribedDepartmentEvents = activeEvents
                        .Where(e => e.organizer.department_id.HasValue && userSubscribedDeptIds.Contains(e.organizer.department_id.Value))
                        .Take(6)
                        .Select(e => new DashboardEventItem
                        {
                            Id = e.id,
                            Title = e.title,
                            CategoryName = e.category?.name ?? "Department Event",
                            VenueName = e.venue?.name ?? (e.organizer.department?.name ?? "Department Wing"),
                            StartDate = e.start_at,
                            AttendeeCount = e.registrations.Count,
                            Capacity = (int)(e.capacity ?? 100),
                            Status = $"🏛️ {e.organizer.department?.name ?? "Department"}"
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load personalization data for Student dashboard.");
            }

            return View("Student", vm);
        }

        // =====================================================================
        // 2. STAFF DASHBOARD (GET: /Dashboard/Staff)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Staff()
        {
            if (await RestrictDashboardAsync("Staff") is { } denied) return denied;
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            int deptEvents = 0;
            int venueCount = 0;
            int noticesCount = 0;

            try
            {
                deptEvents = await _db.events.CountAsync(e => e.organizer_id == userId || (e.organizer.department != null && e.organizer.department.name == userDept));
                venueCount = await _db.venues.CountAsync();
                noticesCount = await _db.announcements.CountAsync(a => a.status == "PUBLISHED");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load staff counts.");
            }

            var vm = new StaffDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "Staff",
                UserDepartment = userDept,
                UserId = formattedId,
                EmployeeId = !string.IsNullOrWhiteSpace(empId) ? empId : (string.IsNullOrWhiteSpace(formattedId) ? "Pending Assignment" : formattedId),
                DepartmentEventsCount = deptEvents,
                VenueReservationsCount = venueCount,
                PendingTasksCount = 0,
                StaffNoticesCount = noticesCount,
                ManagedEquipmentsCount = 0
            };

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            await PopulateNotificationsAsync(vm, userId);

            vm.DepartmentEvents = vm.UpcomingEvents.Where(e => e.CategoryName == "Academic" || e.CategoryName == "Career").Take(4).ToList();

            try
            {
                var upcomingDbBookings = await _db.events
                    .Include(e => e.venue)
                    .Where(e => e.start_at >= DateTime.UtcNow && e.venue_id != null)
                    .OrderBy(e => e.start_at)
                    .Take(5)
                    .ToListAsync();

                vm.UpcomingVenueBookings = upcomingDbBookings.Select(e => new DashboardVenueBookingItem
                {
                    Id = e.id,
                    VenueName = e.venue?.name ?? "Main Campus Hall",
                    Purpose = e.title,
                    ScheduledDate = e.start_at,
                    TimeSlot = e.start_at.ToString("hh:mm tt"),
                    Status = e.approval_status ?? "Confirmed"
                }).ToList();

                var staffNotices = await _db.announcements
                    .Include(a => a.department)
                    .Where(a => a.status == "PUBLISHED")
                    .OrderByDescending(a => a.created_at)
                    .Take(4)
                    .ToListAsync();

                vm.StaffAnnouncements = staffNotices.Select(a => new DashboardAnnouncementItem
                {
                    Id = a.id,
                    Title = a.title,
                    Content = a.content,
                    AuthorName = a.department?.name ?? "Campus Administration",
                    DepartmentName = a.department?.name ?? "General",
                    Priority = a.priority ?? "Normal",
                    CreatedAt = a.created_at
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load staff events and bookings.");
            }

            return View("Staff", vm);
        }

        // =====================================================================
        // 3. FACULTY DASHBOARD (GET: /Dashboard/Faculty)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Faculty()
        {
            if (await RestrictDashboardAsync("Faculty") is { } denied) return denied;
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            int confCount = 0;
            int seminarCount = 0;

            try
            {
                confCount = await _db.events.CountAsync(e => e.category.name == "Academic");
                seminarCount = await _db.events.CountAsync(e => e.approval_status == "PENDING");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load faculty counts.");
            }

            var vm = new FacultyDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "Faculty",
                UserDepartment = userDept,
                UserId = formattedId,
                EmployeeId = !string.IsNullOrWhiteSpace(empId) ? empId : (string.IsNullOrWhiteSpace(formattedId) ? "Pending Assignment" : formattedId),
                AcademicConferencesCount = confCount,
                ScheduledLecturesCount = 0,
                SeminarApprovalsCount = seminarCount,
                ResearchPresentationsCount = confCount,
                DepartmentStudentsCount = 0
            };

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            await PopulateNotificationsAsync(vm, userId);

            vm.AcademicSeminars = vm.UpcomingEvents.Where(e => e.CategoryName == "Academic" || e.CategoryName == "Technology").Take(4).ToList();

            try
            {
                var pendingProposals = await _db.events
                    .Include(e => e.venue)
                    .Include(e => e.organizer)
                    .Where(e => e.approval_status == "PENDING")
                    .OrderByDescending(e => e.created_at)
                    .Take(4)
                    .ToListAsync();

                vm.PendingStudentProposals = pendingProposals.Select(e => new DashboardApprovalItem
                {
                    Id = e.id,
                    EventTitle = e.title,
                    SubmitterName = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "Campus Submitter",
                    SubmitterRole = e.organizer?.account_type ?? "Student",
                    Venue = e.venue?.name ?? "Main Campus",
                    EventDate = e.start_at,
                    SubmittedAt = e.created_at,
                    Status = e.approval_status ?? "PENDING"
                }).ToList();

                var symposiums = await _db.events
                    .Include(e => e.venue)
                    .Include(e => e.category)
                    .Include(e => e.organizer)
                    .Where(e => e.category.name == "Academic" && e.start_at >= DateTime.UtcNow)
                    .OrderBy(e => e.start_at)
                    .Take(3)
                    .ToListAsync();

                vm.ResearchSymposiums = symposiums.Select(e => new DashboardEventItem
                {
                    Id = e.id,
                    Title = e.title,
                    StartDate = e.start_at,
                    VenueName = e.venue?.name ?? "Campus Hall",
                    CategoryName = e.category?.name ?? "Academic",
                    OrganizerName = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "Faculty Board",
                    ShortDescription = e.short_description ?? e.description
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load faculty proposals.");
            }

            return View("Faculty", vm);
        }

        // =====================================================================
        // 4. ORGANIZATION DASHBOARD (GET: /Dashboard/Organization)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Organization()
        {
            if (await RestrictDashboardAsync("Organization") is { } denied) return denied;
            var (userId, userName, userEmail, userRole, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            int hostedCount = 0;
            int totalRsvps = 0;
            int membersCount = 0;

            try
            {
                if (userId.HasValue)
                {
                    hostedCount = await _db.events.CountAsync(e => e.organizer_id == userId.Value);
                    totalRsvps = await _db.registrations.CountAsync(r => r._event.organizer_id == userId.Value);
                    membersCount = await _db.club_members.CountAsync(cm => cm.club.president_id == userId.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load organization counts.");
            }

            var vm = new OrganizationDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "Organization",
                UserDepartment = userDept,
                UserId = formattedId,
                OrganizationName = bio ?? userName,
                HostedEventsCount = hostedCount,
                TotalAttendeesCount = totalRsvps,
                ActiveMembersCount = membersCount,
                BoothReservationsCount = 0
            };

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            await PopulateNotificationsAsync(vm, userId);

            try
            {
                if (userId.HasValue)
                {
                    var myEvents = await _db.events
                        .Include(e => e.category)
                        .Include(e => e.venue)
                        .Include(e => e.registrations)
                        .Where(e => e.organizer_id == userId.Value)
                        .OrderByDescending(e => e.start_at)
                        .Take(5)
                        .ToListAsync();

                    vm.HostedEvents = myEvents.Select(e => new DashboardEventItem
                    {
                        Id = e.id,
                        Title = e.title,
                        StartDate = e.start_at,
                        VenueName = e.venue?.name ?? "Main Campus",
                        CategoryName = e.category?.name ?? "General",
                        AttendeeCount = e.registrations.Count,
                        Capacity = (int)(e.capacity ?? 100)
                    }).ToList();

                    vm.AttendanceAnalytics = myEvents.Select(e => new DashboardRegistrationStatItem
                    {
                        EventTitle = e.title,
                        ConfirmedCount = e.registrations.Count,
                        AttendedCount = e.registrations.Count(r => r.status == "ATTENDED"),
                        MaxCapacity = (int)(e.capacity ?? 100)
                    }).ToList();

                    var members = await _db.club_members
                        .Include(m => m.user)
                        .Where(m => m.club.president_id == userId.Value)
                        .Take(6)
                        .ToListAsync();

                    vm.OrganizationMembers = members.Select(m => new DashboardMemberItem
                    {
                        Id = m.id,
                        FullName = m.user != null ? $"{m.user.first_name} {m.user.last_name}".Trim() : "Club Member",
                        RoleInOrg = m.membership_role ?? "Member",
                        Email = m.user?.email ?? "N/A",
                        JoinedAt = m.applied_at
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load organization events.");
            }

            return View("Organization", vm);
        }

        // =====================================================================
        // 5. ADMIN DASHBOARD (GET: /Dashboard/Admin)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Admin()
        {
            var userRole = await GetCurrentNormalizedRoleAsync();
            if (userRole != "Admin" && userRole != "SuperAdmin")
            {
                TempData["ErrorMessage"] = "Access Restricted: Campus Administrator or SuperAdmin role required.";
                return RedirectToAction(nameof(Index));
            }

            var (userId, userName, userEmail, _, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            var vm = new AdminDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = userRole,
                UserDepartment = userDept,
                UserId = formattedId
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
                _logger.LogWarning(ex, "Admin dashboard stats query error.");
            }

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            await PopulateNotificationsAsync(vm, userId);

            try
            {
                var pendingEvents = await _db.events
                    .Include(e => e.venue)
                    .Include(e => e.organizer)
                    .Where(e => e.approval_status == "PENDING")
                    .OrderBy(e => e.start_at)
                    .Take(5)
                    .ToListAsync();

                vm.PendingEventApprovals = pendingEvents.Select(e => new DashboardApprovalItem
                {
                    Id = e.id,
                    EventTitle = e.title,
                    SubmitterName = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "Campus Submitter",
                    SubmitterRole = e.organizer?.account_type ?? "Coordinator",
                    Venue = e.venue?.name ?? "Main Campus",
                    EventDate = e.start_at,
                    SubmittedAt = e.created_at,
                    Status = e.approval_status ?? "PENDING"
                }).ToList();

                var recentUsers = await _db.users
                    .OrderByDescending(u => u.created_at)
                    .Take(5)
                    .ToListAsync();

                vm.RecentRegisteredUsers = recentUsers.Select(u => new DashboardRecentUserItem
                {
                    Id = u.id,
                    FullName = $"{u.first_name} {u.last_name}".Trim(),
                    Email = u.email,
                    AccountType = u.account_type ?? "STUDENT",
                    Status = u.account_status ?? "ACTIVE",
                    JoinedAt = u.created_at
                }).ToList();

                var venues = await _db.venues
                    .Include(v => v._events)
                    .Take(6)
                    .ToListAsync();

                vm.CampusVenuesStatus = venues.Select(v => new DashboardVenueItem
                {
                    Id = v.id,
                    Name = v.name,
                    Capacity = (int)v.capacity,
                    Building = v.building_name ?? "Main Campus",
                    Status = v.status == "AVAILABLE" ? "Available" : "Maintenance",
                    ScheduledEventsCount = v._events.Count(e => e.start_at >= DateTime.UtcNow)
                }).ToList();

                var categories = await _db.event_categories
                    .Include(c => c._events)
                    .Take(5)
                    .ToListAsync();

                vm.CategoryBreakdown = categories.Select(c => new DashboardCategoryStatItem
                {
                    CategoryName = c.name,
                    EventCount = c._events.Count,
                    ColorHex = "#2563eb"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load admin approval lists.");
            }

            return View("Admin", vm);
        }

        // =====================================================================
        // 6. SUPER ADMIN DASHBOARD (GET: /Dashboard/SuperAdmin)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> SuperAdmin()
        {
            var userRole = await GetCurrentNormalizedRoleAsync();
            if (userRole != "SuperAdmin")
            {
                TempData["ErrorMessage"] = "Access Restricted: The SuperAdmin Master Dashboard is strictly exclusive to Central Super Administrator accounts.";
                return RedirectToAction(nameof(Admin));
            }

            var (userId, userName, userEmail, _, userDept, formattedId, studentId, empId, bio) = await GetUserInfoAsync();

            var vm = new SuperAdminDashboardViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                UserRole = "SuperAdmin",
                UserDepartment = "University Central Administration",
                UserId = formattedId,
                SecurityAlertsCount = 0,
                SystemHealthPercent = 100,
                ServerUptime = "Operational",
                DatabaseEngine = "MySQL 8.0 Enterprise / EF Core 10.0",
                ActiveEnvironment = "Production Campus Network"
            };

            try
            {
                vm.TotalSystemUsersCount = await _db.users.CountAsync();
                vm.TotalPlatformEventsCount = await _db.events.CountAsync();
                vm.TotalSystemRolesCount = await _db.roles.CountAsync();

                var pendingUsers = await _db.users
                    .Include(u => u.department)
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.assigned_byNavigation)
                    .Where(u => u.account_status == "PENDING" || u.account_status == "PENDING_APPROVAL")
                    .OrderByDescending(u => u.created_at)
                    .ToListAsync();

                vm.PendingUserApprovalsCount = pendingUsers.Count;
                vm.PendingUsersList = pendingUsers.Select(u =>
                {
                    var assignedBy = u.user_roleusers.FirstOrDefault(ur => ur.assigned_byNavigation != null)?.assigned_byNavigation;
                    var regName = assignedBy != null ? $"{assignedBy.first_name} {assignedBy.last_name}".Trim() : "Campus Administrator";
                    return new DashboardPendingUserApprovalItem
                    {
                        Id = u.id,
                        FullName = $"{u.first_name} {u.last_name}".Trim(),
                        Username = u.username,
                        Email = u.email,
                        Phone = u.phone,
                        AccountType = u.account_type,
                        DepartmentName = u.department?.name ?? "General Campus",
                        StudentOrEmployeeId = u.student_id ?? u.employee_id,
                        RegisteredByAdminName = regName,
                        RegisteredAt = u.created_at
                    };
                }).ToList();

                var auditLogs = await _db.audit_logs
                    .OrderByDescending(a => a.created_at)
                    .Take(6)
                    .ToListAsync();

                vm.RealtimeAuditLogs = auditLogs.Select(a => new DashboardAuditItem
                {
                    Id = a.id,
                    Action = $"{a.action} [{a.entity_type}]",
                    UserEmail = a.user_id.HasValue ? $"User #{a.user_id.Value}" : "System / Visitor",
                    IpAddress = a.ip_address ?? "127.0.0.1",
                    Timestamp = a.created_at,
                    Severity = a.action.Contains("DELETE", StringComparison.OrdinalIgnoreCase) ? "Warning" : "Info"
                }).ToList();

                var roles = await _db.roles
                    .Include(r => r.user_roles)
                    .ToListAsync();

                vm.RolesDistribution = roles.Select(r => new DashboardRoleSummaryItem
                {
                    RoleName = r.name,
                    UserCount = r.user_roles.Count,
                    BadgeClass = "bg-primary"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SuperAdmin stats query error.");
            }

            await PopulateSharedStatsAsync(vm);
            await PopulateUpcomingEventsAsync(vm);
            await PopulateAnnouncementsAsync(vm);
            await PopulateNotificationsAsync(vm, userId);

            return View("SuperAdmin", vm);
        }

        // =====================================================================
        // HELPER METHODS
        // =====================================================================

        private async Task<IActionResult?> RestrictDashboardAsync(params string[] allowedRoles)
        {
            var userRole = await GetCurrentNormalizedRoleAsync();
            if (string.Equals(userRole, "SuperAdmin", StringComparison.Ordinal))
            {
                return null;
            }

            if (allowedRoles.Any(r => string.Equals(r, userRole, StringComparison.Ordinal)))
            {
                return null;
            }

            TempData["ErrorMessage"] = "Access Restricted: You are not authorized to view this dashboard.";
            return RedirectToAction(nameof(Index));
        }

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
            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var userName = User.Identity?.Name ?? "Campus Member";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "Student";
            var userDept = "Not assigned";
            var formattedUserId = string.IsNullOrEmpty(userIdStr) ? string.Empty : $"HUCEMS-{userIdStr}";
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
                        userDept = dbUser.department?.name ?? "Not assigned";
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
                        AttendeeCount = 0,
                        Capacity = (int)(e.capacity ?? 0)
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch events for dashboard.");
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
        }

        private async Task PopulateNotificationsAsync(DashboardViewModel vm, ulong? userId)
        {
            if (!userId.HasValue)
            {
                return;
            }

            try
            {
                var dbNotifs = await _db.notifications
                    .AsNoTracking()
                    .Where(n => n.user_id == userId.Value)
                    .OrderByDescending(n => n.created_at)
                    .Take(6)
                    .ToListAsync();

                vm.Notifications = dbNotifs.Select(n => new DashboardNotificationItem
                {
                    Id = n.id,
                    Message = $"[{n.title}] {n.message}",
                    Type = n.notification_type == "ANNOUNCEMENT" ? "Warning" : n.notification_type == "EVENT" ? "Info" : n.notification_type == "REGISTRATION" ? "Success" : "Primary",
                    CreatedAt = n.created_at,
                    IsRead = n.is_read
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query notifications for user {UserId}", userId.Value);
            }
        }
    }
}
