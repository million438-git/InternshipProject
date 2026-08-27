using System;
using System.Collections.Generic;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    // =========================================================
    // 1. DASHBOARD OVERVIEW VIEW MODEL
    // =========================================================
    public class AdminOverviewViewModel
    {
        public string AdminName { get; set; } = "Administrator";
        public string AdminRole { get; set; } = "Super Admin";
        public string AdminEmail { get; set; } = "admin@hawassauniversity.edu.et";

        // Statistics
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalEvents { get; set; }
        public int UpcomingEvents { get; set; }
        public int TodayEvents { get; set; }
        public int PendingApprovals { get; set; }
        public int TotalOrganizations { get; set; }
        public int TotalRegistrations { get; set; }
        public int TotalAnnouncements { get; set; }
        public int TotalVenues { get; set; }

        // Recent items
        public List<AdminRecentUserItem> RecentUsers { get; set; } = new();
        public List<AdminRecentActivityItem> RecentActivities { get; set; } = new();
        public List<AdminPendingEventItem> PendingEventsList { get; set; } = new();

        // Chart Data (Events per Category, Monthly Registrations)
        public List<string> ChartCategories { get; set; } = new();
        public List<int> ChartCategoryCounts { get; set; } = new();
        public List<string> ChartMonths { get; set; } = new();
        public List<int> ChartMonthlyRegistrations { get; set; } = new();
    }

    public class AdminRecentUserItem
    {
        public ulong Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AccountType { get; set; } = "STUDENT";
        public string Status { get; set; } = "ACTIVE";
        public DateTime CreatedAt { get; set; }
    }

    public class AdminRecentActivityItem
    {
        public ulong Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AdminPendingEventItem
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Organizer { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public string Venue { get; set; } = string.Empty;
    }

    // =========================================================
    // SUPERADMIN MASTER GOVERNANCE VIEW MODEL
    // =========================================================
    public class SuperAdminMasterDashboardViewModel
    {
        public string SuperAdminName { get; set; } = "Super Administrator";
        public string SuperAdminEmail { get; set; } = "superadmin@hawassauniversity.edu.et";
        
        // System Wide Metrics
        public int TotalPlatformUsers { get; set; }
        public int ActiveUsersCount { get; set; }
        public int PendingUserApprovalsCount { get; set; }
        public int TotalAdministratorsCount { get; set; }
        public int TotalCampusEvents { get; set; }
        public int PendingEventApprovalsCount { get; set; }
        public int TotalClubsCount { get; set; }
        public int TotalOrganizationsCount { get; set; }
        public int TotalFacultiesCount { get; set; }
        public int TotalDepartmentsCount { get; set; }
        public int TotalAuditLogsCount { get; set; }
        public long TotalDatabaseRowsEstimated { get; set; }
        public string DatabaseStatus { get; set; } = "Online / Healthy";
        public string ServerStatus { get; set; } = "Production Active";
        public string SystemUptime { get; set; } = "99.99%";

        // Pending user registrations requiring SuperAdmin activation
        public List<AdminUserApprovalItem> PendingApprovalsList { get; set; } = new();

        // Admin Activity Stream (Actions taken by Admins and Staff)
        public List<AdminActivityLogItem> AdminActivityFeed { get; set; } = new();

        // Platform events pending approval
        public List<AdminPendingEventItem> PendingEventsList { get; set; } = new();

        // System Settings summary
        public bool RequireEventApproval { get; set; } = true;
        public bool MaintenanceMode { get; set; } = false;
        public bool EmailNotificationsEnabled { get; set; } = true;
    }

    public class AdminActivityLogItem
    {
        public ulong Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminRole { get; set; } = "Admin";
        public string Description { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public ulong? EntityId { get; set; }
        public string? IpAddress { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // =========================================================
    // 2. USER MANAGEMENT VIEW MODELS
    // =========================================================
    public class AdminUsersViewModel
    {
        public List<AdminUserRow> Users { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? RoleFilter { get; set; }
        public string? StatusFilter { get; set; }
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int SuspendedCount { get; set; }
        public int PendingCount { get; set; }
        public int PendingApprovalCount { get; set; }
        public bool IsSuperAdmin { get; set; }
    }

    public class AdminUserRow
    {
        public ulong Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? StudentId { get; set; }
        public string? EmployeeId { get; set; }
        public string AccountType { get; set; } = "STUDENT";
        public string Status { get; set; } = "ACTIVE";
        public string? DepartmentName { get; set; }
        public string? RegisteredByAdminName { get; set; }
        public ulong? RegisteredByAdminId { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int EventCount { get; set; }
        public int RegistrationCount { get; set; }
    }

    public class AdminUserApprovalViewModel
    {
        public List<AdminUserApprovalItem> PendingUsers { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? RoleFilter { get; set; }
        public int TotalPendingCount { get; set; }
        public int StudentPendingCount { get; set; }
        public int FacultyPendingCount { get; set; }
        public int StaffPendingCount { get; set; }
        public int OrganizationPendingCount { get; set; }
        public bool IsSuperAdmin { get; set; }
    }

    public class AdminUserApprovalItem
    {
        public ulong Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string AccountType { get; set; } = "STUDENT";
        public string Status { get; set; } = "PENDING";
        public string? DepartmentName { get; set; }
        public string? FacultyName { get; set; }
        public string? StudentId { get; set; }
        public string? EmployeeId { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? RegisteredByAdminName { get; set; }
        public ulong? RegisteredByAdminId { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    public class AdminCreateUserInputModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Password { get; set; } = string.Empty;
        public string AccountType { get; set; } = "STUDENT";
        public ulong? FacultyId { get; set; }
        public ulong? DepartmentId { get; set; }
        public string? StudentId { get; set; }
        public string? AcademicProgram { get; set; }
        public string? YearOfStudy { get; set; }
        public string? EmployeeId { get; set; }
        public string? AcademicTitle { get; set; }
        public string? StaffUnit { get; set; }
        public string? JobTitle { get; set; }
        public string? OfficeLocation { get; set; }
        public string? OrganizationName { get; set; }
        public string? OrganizationType { get; set; }
        public string? OrganizationAcronym { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? InitialStatus { get; set; }
        public bool SendWelcomeEmail { get; set; } = true;
    }

    public class AdminRegisterUserPageViewModel
    {
        public List<Department> Departments { get; set; } = new();
        public List<Faculty> Faculties { get; set; } = new();
        public bool IsSuperAdmin { get; set; }
        public string SelectedRole { get; set; } = "STUDENT";
        public AdminCreateUserInputModel Form { get; set; } = new();
        public int TotalRegisteredUsersCount { get; set; }
        public int PendingApprovalsCount { get; set; }
    }

    // =========================================================
    // 3. EVENT MANAGEMENT VIEW MODELS
    // =========================================================
    public class AdminEventsViewModel
    {
        public List<AdminEventRow> Events { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public string? CategoryFilter { get; set; }
        public int TotalEvents { get; set; }
        public int PendingApprovalCount { get; set; }
        public int PublishedCount { get; set; }
        public int CancelledCount { get; set; }
    }

    public class AdminEventRow
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? VenueName { get; set; }
        public string OrganizerName { get; set; } = string.Empty;
        public string? OrganizationName { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public uint? Capacity { get; set; }
        public int RegistrationCount { get; set; }
        public string Status { get; set; } = "PUBLISHED";
        public string ApprovalStatus { get; set; } = "APPROVED";
        public bool IsPublic { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // =========================================================
    // 4. ANNOUNCEMENT MANAGEMENT VIEW MODEL
    // =========================================================
    public class AdminAnnouncementsViewModel
    {
        public List<AdminAnnouncementRow> Announcements { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int TotalCount { get; set; }
        public int PinnedCount { get; set; }
        public int PublishedCount { get; set; }
    }

    public class AdminAnnouncementRow
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public string Priority { get; set; } = "NORMAL";
        public string Status { get; set; } = "PUBLISHED";
        public bool IsPinned { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // =========================================================
    // 5. ORGANIZATION MANAGEMENT VIEW MODEL
    // =========================================================
    public class AdminOrganizationsViewModel
    {
        public List<AdminOrganizationRow> Organizations { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int PendingCount { get; set; }
    }

    public class AdminOrganizationRow
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string OrganizationType { get; set; } = "CLUB";
        public string? DepartmentName { get; set; }
        public string? Email { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public int MemberCount { get; set; }
        public int EventCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // =========================================================
    // 6. FACULTIES & DEPARTMENTS VIEW MODELS
    // =========================================================
    public class AdminFacultiesViewModel
    {
        public List<AdminFacultyRow> Faculties { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class AdminFacultyRow
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? DeanName { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public int DepartmentCount { get; set; }
    }

    public class AdminDepartmentsViewModel
    {
        public List<AdminDepartmentRow> Departments { get; set; } = new();
        public List<Faculty> Faculties { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class AdminDepartmentRow
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string FacultyName { get; set; } = string.Empty;
        public ulong FacultyId { get; set; }
        public string? HeadName { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public int StudentCount { get; set; }
    }

    // =========================================================
    // 7. VENUE MANAGEMENT VIEW MODEL
    // =========================================================
    public class AdminVenuesViewModel
    {
        public List<AdminVenueRow> Venues { get; set; } = new();
        public int TotalCount { get; set; }
        public int AvailableCount { get; set; }
        public int MaintenanceCount { get; set; }
    }

    public class AdminVenueRow
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BuildingName { get; set; }
        public string? RoomNumber { get; set; }
        public uint Capacity { get; set; }
        public string VenueType { get; set; } = "AUDITORIUM";
        public string Status { get; set; } = "AVAILABLE";
        public string? Amenities { get; set; }
        public string? Description { get; set; }
        public int ScheduledEventsCount { get; set; }
    }

    // =========================================================
    // 8. REGISTRATIONS VIEW MODEL
    // =========================================================
    public class AdminRegistrationsViewModel
    {
        public List<AdminRegistrationRow> Registrations { get; set; } = new();
        public List<_event> Events { get; set; } = new();
        public ulong? SelectedEventId { get; set; }
        public string? StatusFilter { get; set; }
        public int TotalCount { get; set; }
        public int ConfirmedCount { get; set; }
        public int WaitlistedCount { get; set; }
        public int CancelledCount { get; set; }
    }

    public class AdminRegistrationRow
    {
        public ulong Id { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public ulong EventId { get; set; }
        public string AttendeeName { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public string? TicketCode { get; set; }
        public string Status { get; set; } = "CONFIRMED";
        public bool Attended { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    // =========================================================
    // 9. COMMENTS & FEEDBACK VIEW MODEL
    // =========================================================
    public class AdminCommentsFeedbackViewModel
    {
        public List<AdminCommentRow> Comments { get; set; } = new();
        public List<AdminFeedbackRow> Feedbacks { get; set; } = new();
        public int TotalComments { get; set; }
        public int TotalFeedbacks { get; set; }
        public double AverageRating { get; set; }
    }

    public class AdminCommentRow
    {
        public ulong Id { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public ulong EventId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string CommentText { get; set; } = string.Empty;
        public bool IsFlagged { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminFeedbackRow
    {
        public ulong Id { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? FeedbackText { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // =========================================================
    // 10. REPORTS & ANALYTICS VIEW MODEL
    // =========================================================
    public class AdminReportsViewModel
    {
        public int TotalUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int TotalEvents { get; set; }
        public int EventsThisMonth { get; set; }
        public int TotalRegistrations { get; set; }
        public int RegistrationsThisMonth { get; set; }
        public int TotalOrganizations { get; set; }

        public List<string> MonthlyLabels { get; set; } = new();
        public List<int> MonthlyEventCounts { get; set; } = new();
        public List<int> MonthlyRegCounts { get; set; } = new();

        public List<string> CategoryLabels { get; set; } = new();
        public List<int> CategoryCounts { get; set; } = new();

        public List<AdminTopEventRow> TopEvents { get; set; } = new();
    }

    public class AdminTopEventRow
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Registrations { get; set; }
        public uint? Capacity { get; set; }
        public double FillRate { get; set; }
    }

    // =========================================================
    // 11. NOTIFICATIONS VIEW MODEL
    // =========================================================
    public class AdminNotificationsViewModel
    {
        public List<AdminNotificationRow> Notifications { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<User> Users { get; set; } = new();
        public int TotalSent { get; set; }
        public int UnreadCount { get; set; }
    }

    public class AdminNotificationRow
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = "ALL";
        public string Type { get; set; } = "ANNOUNCEMENT";
        public DateTime CreatedAt { get; set; }
    }

    // =========================================================
    // 12. ROLES & PERMISSIONS VIEW MODEL
    // =========================================================
    public class AdminRolesPermissionsViewModel
    {
        public List<AdminRoleRow> Roles { get; set; } = new();
        public List<Permission> AllPermissions { get; set; } = new();
    }

    public class AdminRoleRow
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int UserCount { get; set; }
        public List<string> AssignedPermissions { get; set; } = new();
    }

    // =========================================================
    // 13. CATEGORIES & TAGS VIEW MODEL
    // =========================================================
    public class AdminCategoriesTagsViewModel
    {
        public List<event_category> Categories { get; set; } = new();
        public List<event_tag> Tags { get; set; } = new();
    }

    // =========================================================
    // 14. AUDIT LOGS VIEW MODEL
    // =========================================================
    public class AdminAuditLogsViewModel
    {
        public List<AdminAuditLogRow> Logs { get; set; } = new();
        public string? SearchTerm { get; set; }
        public int TotalCount { get; set; }
    }

    public class AdminAuditLogRow
    {
        public ulong Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public ulong? EntityId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // =========================================================
    // 15. SESSIONS & DEVICES VIEW MODEL
    // =========================================================
    public class AdminSessionsViewModel
    {
        public List<AdminSessionRow> ActiveSessions { get; set; } = new();
        public int TotalActive { get; set; }
    }

    public class AdminSessionRow
    {
        public ulong Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsCurrent { get; set; }
    }

    // =========================================================
    // 16. SYSTEM SETTINGS VIEW MODEL
    // =========================================================
    public class AdminSettingsViewModel
    {
        public string UniversityName { get; set; } = "Hawassa University";
        public string CampusName { get; set; } = "Main Campus (Unified Hub)";
        public string WebsiteTitle { get; set; } = "Hawassa Unified Campus Event Management System (HUCEMS)";
        public string ContactEmail { get; set; } = "events@hawassauniversity.edu.et";
        public string ContactPhone { get; set; } = "+251 46 220 9676";
        public string DefaultTimezone { get; set; } = "East Africa Time (UTC+3)";
        public bool RequireEventApproval { get; set; } = true;
        public bool AllowPublicRegistrations { get; set; } = true;
        public bool EnableEmailNotifications { get; set; } = true;
        public bool EnableAuditLogging { get; set; } = true;
        public bool MaintenanceMode { get; set; } = false;
    }

    // =========================================================
    // 17. DATABASE MANAGEMENT & SNAPSHOT VIEW MODEL
    // =========================================================
    public class AdminDatabaseManagementViewModel
    {
        public string DatabaseName { get; set; } = "university_event_management";
        public string ServerHost { get; set; } = "localhost:3306";
        public string EngineVersion { get; set; } = "MySQL 8.0 Enterprise / RDS";
        public string ConnectionStatus { get; set; } = "Online / Operational";
        public int TotalTables { get; set; } = 30;
        public long EstimatedTotalRows { get; set; }
        public string TotalDatabaseSize { get; set; } = "Healthy (Pool: 24/500)";
        public DateTime? LastBackupTimestamp { get; set; }
        public List<DatabaseTableStatItem> TableStats { get; set; } = new();
        public List<DatabaseBackupFileItem> BackupFiles { get; set; } = new();
    }

    public class DatabaseTableStatItem
    {
        public string TableName { get; set; } = string.Empty;
        public long RowCount { get; set; }
        public string Engine { get; set; } = "InnoDB";
        public string Description { get; set; } = string.Empty;
    }

    public class DatabaseBackupFileItem
    {
        public string FileName { get; set; } = string.Empty;
        public string FileSizeBytes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "Verified Snapshot";
        public string Checksum { get; set; } = string.Empty;
    }
}
