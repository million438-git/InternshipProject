using System;
using System.Collections.Generic;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    // =========================================================================
    // BASE DASHBOARD VIEW MODEL
    // =========================================================================
    public class DashboardViewModel
    {
        public string UserName { get; set; } = "Campus Member";
        public string UserEmail { get; set; } = string.Empty;
        public string UserRole { get; set; } = "Student";
        public string UserDepartment { get; set; } = "Computer Cyber Security";
        public string UserId { get; set; } = "HUCEMS-2026-001";
        public string? StudentId { get; set; }
        public string? EmployeeId { get; set; }
        public string? OrganizationName { get; set; }

        // General Counts
        public int UpcomingEventsCount { get; set; }
        public int TodayEventsCount { get; set; }
        public int AnnouncementCount { get; set; }
        public int RegistrationCount { get; set; }

        // General Collections
        public List<DashboardEventItem> UpcomingEvents { get; set; } = new();
        public List<DashboardAnnouncementItem> RecentAnnouncements { get; set; } = new();
        public List<DashboardNotificationItem> Notifications { get; set; } = new();
    }

    // =========================================================================
    // 1. STUDENT DASHBOARD VIEW MODEL
    // =========================================================================
    public class StudentDashboardViewModel : DashboardViewModel
    {
        public int RegisteredEventsCount { get; set; }
        public int AttendedEventsCount { get; set; }
        public int EarnedCertificatesCount { get; set; }
        public int UpcomingWorkshopsCount { get; set; }
        public bool HasSelectedInterests { get; set; }
        public int SelectedInterestsCount { get; set; }
        public int SubscribedDepartmentsCount { get; set; }
        public int FollowedClubsCount { get; set; }

        public List<DashboardEventItem> MyRegisteredEvents { get; set; } = new();
        public List<DashboardEventItem> RecommendedEventsForYou { get; set; } = new();
        public List<DashboardEventItem> SubscribedDepartmentEvents { get; set; } = new();
        public List<DashboardClubItem> RecommendedClubs { get; set; } = new();
        public List<DashboardClubItem> MyClubs { get; set; } = new();
    }

    // =========================================================================
    // 2. STAFF DASHBOARD VIEW MODEL
    // =========================================================================
    public class StaffDashboardViewModel : DashboardViewModel
    {
        public int DepartmentEventsCount { get; set; }
        public int VenueReservationsCount { get; set; }
        public int PendingTasksCount { get; set; }
        public int StaffNoticesCount { get; set; }
        public int ManagedEquipmentsCount { get; set; }

        public List<DashboardEventItem> DepartmentEvents { get; set; } = new();
        public List<DashboardVenueBookingItem> UpcomingVenueBookings { get; set; } = new();
        public List<DashboardTaskItem> OperationalTasks { get; set; } = new();
        public List<DashboardAnnouncementItem> StaffAnnouncements { get; set; } = new();
    }

    // =========================================================================
    // 3. FACULTY DASHBOARD VIEW MODEL
    // =========================================================================
    public class FacultyDashboardViewModel : DashboardViewModel
    {
        public int AcademicConferencesCount { get; set; }
        public int ScheduledLecturesCount { get; set; }
        public int SeminarApprovalsCount { get; set; }
        public int ResearchPresentationsCount { get; set; }
        public int DepartmentStudentsCount { get; set; }

        public List<DashboardEventItem> AcademicSeminars { get; set; } = new();
        public List<DashboardApprovalItem> PendingStudentProposals { get; set; } = new();
        public List<DashboardScheduleItem> WeeklyClassSchedule { get; set; } = new();
        public List<DashboardEventItem> ResearchSymposiums { get; set; } = new();
    }

    // =========================================================================
    // 4. ORGANIZATION DASHBOARD VIEW MODEL
    // =========================================================================
    public class OrganizationDashboardViewModel : DashboardViewModel
    {
        public int HostedEventsCount { get; set; }
        public int TotalAttendeesCount { get; set; }
        public int ActiveMembersCount { get; set; }
        public int BoothReservationsCount { get; set; }

        public List<DashboardEventItem> HostedEvents { get; set; } = new();
        public List<DashboardMemberItem> OrganizationMembers { get; set; } = new();
        public List<DashboardRegistrationStatItem> AttendanceAnalytics { get; set; } = new();
    }

    // =========================================================================
    // 5. ADMIN DASHBOARD VIEW MODEL
    // =========================================================================
    public class AdminDashboardViewModel : DashboardViewModel
    {
        public int TotalUsersCount { get; set; }
        public int PendingEventApprovalsCount { get; set; }
        public int ActiveVenuesCount { get; set; }
        public int ReportedContentCount { get; set; }
        public int ActiveOrganizationsCount { get; set; }

        public List<DashboardApprovalItem> PendingEventApprovals { get; set; } = new();
        public List<DashboardRecentUserItem> RecentRegisteredUsers { get; set; } = new();
        public List<DashboardVenueItem> CampusVenuesStatus { get; set; } = new();
        public List<DashboardReportItem> RecentFlaggedContent { get; set; } = new();
        public List<DashboardCategoryStatItem> CategoryBreakdown { get; set; } = new();
    }

    // =========================================================================
    // 6. SUPER ADMIN DASHBOARD VIEW MODEL
    // =========================================================================
    public class SuperAdminDashboardViewModel : DashboardViewModel
    {
        public int TotalSystemUsersCount { get; set; }
        public int TotalPlatformEventsCount { get; set; }
        public int TotalSystemRolesCount { get; set; }
        public int SecurityAlertsCount { get; set; }
        public int PendingUserApprovalsCount { get; set; }
        public int SystemHealthPercent { get; set; } = 99;
        public string ServerUptime { get; set; } = "99.98%";
        public string DatabaseEngine { get; set; } = "MySQL 8.0 Enterprise";
        public string ActiveEnvironment { get; set; } = "Production / Campus Network";

        public List<DashboardAuditItem> RealtimeAuditLogs { get; set; } = new();
        public List<DashboardPendingUserApprovalItem> PendingUsersList { get; set; } = new();
        public List<DashboardRoleSummaryItem> RolesDistribution { get; set; } = new();
        public List<DashboardSystemHealthItem> SystemServicesStatus { get; set; } = new();
        public List<DashboardSecurityAlertItem> SecurityIncidents { get; set; } = new();
    }

    public class DashboardPendingUserApprovalItem
    {
        public ulong Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string AccountType { get; set; } = "STUDENT";
        public string? DepartmentName { get; set; }
        public string? StudentOrEmployeeId { get; set; }
        public string? RegisteredByAdminName { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    // =========================================================================
    // SUPPORTING SUB-MODELS
    // =========================================================================
    public class DashboardEventItem
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? VenueName { get; set; }
        public string? CategoryName { get; set; }
        public string? OrganizerName { get; set; }
        public string Status { get; set; } = "Published";
        public int AttendeeCount { get; set; }
        public int Capacity { get; set; } = 100;
        public bool IsRegistered { get; set; }
    }

    public class DashboardAnnouncementItem
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? AuthorName { get; set; }
        public string? DepartmentName { get; set; }
        public string Priority { get; set; } = "Normal";
        public DateTime CreatedAt { get; set; }
    }

    public class DashboardNotificationItem
    {
        public ulong Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Type { get; set; } = "Info";
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    public class DashboardClubItem
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Category { get; set; } = "Technology";
        public int MemberCount { get; set; }
        public int FollowerCount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? RecommendationReason { get; set; }
        public bool IsFollowing { get; set; }
        public string MembershipStatus { get; set; } = "NONE";
    }

    public class DashboardVenueBookingItem
    {
        public ulong Id { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public string TimeSlot { get; set; } = "09:00 AM - 12:00 PM";
        public string Status { get; set; } = "Confirmed";
    }

    public class DashboardTaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium";
        public DateTime DueDate { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class DashboardApprovalItem
    {
        public ulong Id { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public string SubmitterName { get; set; } = string.Empty;
        public string SubmitterRole { get; set; } = "Student";
        public string Venue { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string Status { get; set; } = "PENDING";
    }

    public class DashboardScheduleItem
    {
        public string Day { get; set; } = "Monday";
        public string CourseCode { get; set; } = "CSE-412";
        public string CourseTitle { get; set; } = "Network Security & Cryptography";
        public string Time { get; set; } = "09:00 AM - 11:30 AM";
        public string Room { get; set; } = "Main Tech Hall 3";
    }

    public class DashboardMemberItem
    {
        public ulong Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string RoleInOrg { get; set; } = "Coordinator";
        public string Email { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }

    public class DashboardRegistrationStatItem
    {
        public string EventTitle { get; set; } = string.Empty;
        public int ConfirmedCount { get; set; }
        public int AttendedCount { get; set; }
        public int MaxCapacity { get; set; }
    }

    public class DashboardRecentUserItem
    {
        public ulong Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AccountType { get; set; } = "STUDENT";
        public string Status { get; set; } = "ACTIVE";
        public DateTime JoinedAt { get; set; }
    }

    public class DashboardVenueItem
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public string Building { get; set; } = string.Empty;
        public string Status { get; set; } = "Available";
        public int ScheduledEventsCount { get; set; }
    }

    public class DashboardReportItem
    {
        public ulong Id { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string ContentType { get; set; } = "Event Comment";
        public string ReportedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "PENDING";
    }

    public class DashboardCategoryStatItem
    {
        public string CategoryName { get; set; } = string.Empty;
        public int EventCount { get; set; }
        public string ColorHex { get; set; } = "#2563eb";
    }

    public class DashboardAuditItem
    {
        public ulong Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string IpAddress { get; set; } = "127.0.0.1";
        public DateTime Timestamp { get; set; }
        public string Severity { get; set; } = "Info";
    }

    public class DashboardRoleSummaryItem
    {
        public string RoleName { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public string BadgeClass { get; set; } = "bg-primary";
    }

    public class DashboardSystemHealthItem
    {
        public string ServiceName { get; set; } = string.Empty;
        public string Status { get; set; } = "Operational";
        public string Latency { get; set; } = "12ms";
        public string HealthClass { get; set; } = "text-success";
    }

    public class DashboardSecurityAlertItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Low";
        public DateTime OccurredAt { get; set; }
    }
}