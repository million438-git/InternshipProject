using System;
using System.Collections.Generic;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    public class CheckInViewModel
    {
        public ulong EventId { get; set; }
        public string EventTitle { get; set; } = "";
        public DateTime EventDate { get; set; }
        public string VenueName { get; set; } = "";
        public int Capacity { get; set; }
        public int TotalRegisteredCount { get; set; }
        public int AttendedCount { get; set; }
        public int PendingCount => Math.Max(0, TotalRegisteredCount - AttendedCount);
        public double AttendancePercentage => TotalRegisteredCount > 0 ? Math.Round((double)AttendedCount / TotalRegisteredCount * 100, 1) : 0;
        public List<AttendeeCheckInItem> Attendees { get; set; } = new();
    }

    public class AttendeeCheckInItem
    {
        public ulong RegistrationId { get; set; }
        public ulong UserId { get; set; }
        public string FullName { get; set; } = "";
        public string StudentId { get; set; } = "";
        public string Email { get; set; } = "";
        public string Department { get; set; } = "";
        public string RegistrationCode { get; set; } = "";
        public string Status { get; set; } = "REGISTERED";
        public DateTime RegisteredAt { get; set; }
        public DateTime? AttendedAt { get; set; }
    }

    public class VerifyTicketRequest
    {
        public ulong EventId { get; set; }
        public string TicketCode { get; set; } = "";
    }

    public class VerifyTicketResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Status { get; set; } = ""; // "VERIFIED", "ALREADY_ATTENDED", "INVALID"
        public string? FullName { get; set; }
        public string? StudentId { get; set; }
        public string? Department { get; set; }
        public string? TicketCode { get; set; }
        public DateTime? AttendedAt { get; set; }
        public int TotalAttended { get; set; }
        public int TotalRegistered { get; set; }
    }
}
