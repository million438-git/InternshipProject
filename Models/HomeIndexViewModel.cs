using System;
using System.Collections.Generic;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    public class HomeIndexViewModel
    {
        public int TotalActiveEvents { get; set; }
        public int TotalDepartments { get; set; }
        public int TotalClubs { get; set; }
        public int TotalVenues { get; set; }

        public List<HomeEventItemViewModel> UpcomingEvents { get; set; } = new();
        public List<HomeAnnouncementItemViewModel> LatestAnnouncements { get; set; } = new();
    }

    public class HomeEventItemViewModel
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? ImageUrl { get; set; }
        public string? CategoryName { get; set; }
        public string? VenueName { get; set; }
        public DateTime StartDate { get; set; }
        public string? FormattedTime { get; set; }
        public string? Slug { get; set; }
    }

    public class HomeAnnouncementItemViewModel
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string Priority { get; set; } = "NORMAL";
        public string? DepartmentName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
