using System;
using System.Collections.Generic;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    // =========================================================================
    // 1. USER NOTIFICATION INBOX VIEW MODEL
    // =========================================================================
    public class NotificationCenterViewModel
    {
        public ulong UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = "Student";
        public string? DepartmentName { get; set; }

        public List<NotificationItemDto> Notifications { get; set; } = new();

        // Metrics & KPI Counts
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int AnnouncementAlertsCount { get; set; }
        public int EventAlertsCount { get; set; }
        public int RegistrationAlertsCount { get; set; }
        public int SystemAlertsCount { get; set; }
        public int ClubAlertsCount { get; set; }

        // Filter & Search Controls
        public string ActiveFilter { get; set; } = "ALL"; // ALL, UNREAD, ANNOUNCEMENT, EVENT, REGISTRATION, SYSTEM, CLUB
        public string? SearchTerm { get; set; }
    }

    // =========================================================================
    // 2. NOTIFICATION ITEM DTO
    // =========================================================================
    public class NotificationItemDto
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = "SYSTEM"; // EVENT, REGISTRATION, REMINDER, ANNOUNCEMENT, SYSTEM, FEEDBACK, CLUB
        public string? RelatedEntityType { get; set; }
        public ulong? RelatedEntityId { get; set; }
        public string? ActionUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TimeAgoFormatted { get; set; } = "Just now";

        // UI Helpers
        public string BadgeClass => NotificationType switch
        {
            "ANNOUNCEMENT" => "bg-warning-subtle text-warning-emphasis border border-warning-subtle",
            "EVENT" => "bg-primary-subtle text-primary border border-primary-subtle",
            "REGISTRATION" => "bg-success-subtle text-success border border-success-subtle",
            "REMINDER" => "bg-info-subtle text-info-emphasis border border-info-subtle",
            "CLUB" => "bg-purple-subtle text-purple border",
            _ => "bg-secondary-subtle text-secondary border"
        };

        public string IconClass => NotificationType switch
        {
            "ANNOUNCEMENT" => "bi-megaphone-fill text-warning",
            "EVENT" => "bi-calendar-event-fill text-primary",
            "REGISTRATION" => "bi-ticket-perforated-fill text-success",
            "REMINDER" => "bi-alarm-fill text-info",
            "CLUB" => "bi-people-fill text-purple",
            _ => "bi-bell-fill text-secondary"
        };
    }

    // =========================================================================
    // 3. SEND DIRECT / BROADCAST REQUEST DTOs
    // =========================================================================
    public class SendDirectNotificationRequest
    {
        public ulong? TargetUserId { get; set; }
        public string? TargetUsername { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = "SYSTEM";
        public string? ActionUrl { get; set; }
    }

    public class BroadcastNotificationRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = "ALL"; // ALL, STUDENTS, FACULTY, STAFF, ORGANIZERS, DEPARTMENT, SPECIFIC_USER
        public ulong? DepartmentId { get; set; }
        public ulong? TargetUserId { get; set; }
        public string? TargetUsername { get; set; }
        public string NotificationType { get; set; } = "ANNOUNCEMENT";
        public string? ActionUrl { get; set; }
    }
}
