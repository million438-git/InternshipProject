using System;
using System.Collections.Generic;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    public class PersonalizationPreferencesViewModel
    {
        public ulong UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserRole { get; set; } = "Student";
        public string? PrimaryDepartmentName { get; set; }

        // Category Interests (Topic Personalization)
        public List<CategoryInterestItemViewModel> Categories { get; set; } = new();
        public int SelectedCategoriesCount => Categories.Count(c => c.IsSelected);

        // Department Subscriptions (Academic Following & Alerts)
        public List<DepartmentSubscriptionItemViewModel> DepartmentSubscriptions { get; set; } = new();
        public int SubscribedDepartmentsCount => DepartmentSubscriptions.Count(d => d.IsSubscribed);
        public int AlertsEnabledCount => DepartmentSubscriptions.Count(d => d.IsSubscribed && d.NotifyOnNewEvent);

        // General Notification Settings
        public bool EmailNotificationsEnabled { get; set; } = true;
        public bool PushAlertsEnabled { get; set; } = true;
        public bool EventRemindersEnabled { get; set; } = true;
    }

    public class CategoryInterestItemViewModel
    {
        public ulong CategoryId { get; set; }
        public ulong? InterestId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? ColorHex { get; set; }
        public bool IsSelected { get; set; }
        public string InterestLevel { get; set; } = "MEDIUM"; // LOW, MEDIUM, HIGH
        public DateTime? CreatedAt { get; set; }
        public int AssociatedEventsCount { get; set; }
    }

    public class DepartmentSubscriptionItemViewModel
    {
        public ulong? SubId { get; set; }
        public ulong DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string? DepartmentCode { get; set; }
        public string? FacultyName { get; set; }
        public string? Building { get; set; }
        public bool IsSubscribed { get; set; }
        public bool NotifyOnNewEvent { get; set; } = true;
        public DateTime? SubscribedAt { get; set; }
        public int ActiveEventsCount { get; set; }
    }

    public class DeptSubscriptionToggleRequest
    {
        public ulong DepartmentId { get; set; }
        public bool? NotifyOnNewEvent { get; set; }
    }

    public class SaveInterestsRequest
    {
        public List<ulong> CategoryIds { get; set; } = new();
        public string InterestLevel { get; set; } = "HIGH";
    }
}
