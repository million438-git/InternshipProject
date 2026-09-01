using System;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    public class CertificateViewModel
    {
        public ulong RegistrationId { get; set; }
        public string CertificateNumber { get; set; } = "";
        public string StudentFullName { get; set; } = "";
        public string StudentIdNumber { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public string FacultyName { get; set; } = "";
        public string EventTitle { get; set; } = "";
        public string EventCategory { get; set; } = "";
        public DateTime EventDate { get; set; }
        public string VenueName { get; set; } = "";
        public string OrganizerName { get; set; } = "";
        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
        public string VerificationUrl { get; set; } = "";
        public string SecurityHash { get; set; } = "";
    }
}
