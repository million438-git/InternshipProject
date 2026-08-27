using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    public class ClubListViewModel
    {
        public string? SearchQuery { get; set; }
        public ulong? SelectedCategoryId { get; set; }
        public ulong? SelectedDepartmentId { get; set; }
        public string? FilterType { get; set; } // 'all', 'recommended', 'following', 'my'
        public string? StatusFilter { get; set; } // 'ALL', 'ACTIVE', 'PENDING', 'SUSPENDED'
        public bool IsUserAdmin { get; set; }
        public int TotalActiveCount { get; set; }
        public int TotalPendingCount { get; set; }
        public int TotalSuspendedCount { get; set; }

        public bool HasSelectedInterests { get; set; }
        public List<string> UserInterestNames { get; set; } = new();

        public List<ClubCardViewModel> RecommendedClubs { get; set; } = new();
        public List<ClubCardViewModel> AllClubs { get; set; } = new();

        public List<SelectListItem> AvailableCategories { get; set; } = new();
        public List<SelectListItem> AvailableDepartments { get; set; } = new();
    }

    public class ClubCardViewModel
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? DepartmentName { get; set; }
        public string? FacultyName { get; set; }
        public string? OrganizationName { get; set; }
        public string? PresidentName { get; set; }
        public string Status { get; set; } = "ACTIVE";

        public List<ClubInterestBadge> Interests { get; set; } = new();

        public int FollowerCount { get; set; }
        public int MemberCount { get; set; }
        public int UpcomingEventCount { get; set; }

        public bool IsFollowing { get; set; }
        public string MembershipStatus { get; set; } = "NONE"; // NONE, PENDING, APPROVED, REJECTED
        public string? MembershipRole { get; set; }

        public int MatchScore { get; set; }
        public string? RecommendationReason { get; set; }
        public bool IsPresidentOrAdmin { get; set; }
    }

    public class ClubInterestBadge
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
    }

    public class ClubDetailsViewModel
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? FacultyName { get; set; }
        public string? DepartmentName { get; set; }
        public string? OrganizationName { get; set; }
        public string? PresidentName { get; set; }
        public string? PresidentEmail { get; set; }
        public ulong? PresidentId { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public DateTime CreatedAt { get; set; }

        public List<ClubInterestBadge> Interests { get; set; } = new();
        public int FollowerCount { get; set; }
        public int MemberCount { get; set; }
        public int PendingRequestsCount { get; set; }

        public bool IsFollowing { get; set; }
        public string MembershipStatus { get; set; } = "NONE"; // NONE, PENDING, APPROVED, REJECTED
        public string? MembershipRole { get; set; }
        public bool CanManage { get; set; }
        public bool IsUserAdmin { get; set; }

        public List<ClubEventItem> UpcomingEvents { get; set; } = new();
        public List<ClubAnnouncementItem> Announcements { get; set; } = new();
        public List<ClubMemberItem> Officers { get; set; } = new();
        public List<ClubMemberItem> Members { get; set; } = new();
    }

    public class ClubEventItem
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime StartAt { get; set; }
        public string? VenueName { get; set; }
        public string? CategoryName { get; set; }
    }

    public class ClubAnnouncementItem
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Priority { get; set; } = "NORMAL";
    }

    public class ClubMemberItem
    {
        public ulong MemberRecordId { get; set; }
        public ulong UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public string Role { get; set; } = "MEMBER";
        public string Status { get; set; } = "APPROVED";
        public DateTime AppliedAt { get; set; }
        public string? RequestNotes { get; set; }
    }

    public class ClubCreateEditViewModel
    {
        public ulong? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ShortName { get; set; }
        public string? Slug { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }

        public ulong? FacultyId { get; set; }
        public ulong? DepartmentId { get; set; }
        public ulong? OrganizationId { get; set; }
        public ulong? PresidentId { get; set; }

        public string Status { get; set; } = "ACTIVE";

        public List<ulong> SelectedCategoryIds { get; set; } = new();

        public List<SelectListItem> AvailableFaculties { get; set; } = new();
        public List<SelectListItem> AvailableDepartments { get; set; } = new();
        public List<SelectListItem> AvailableOrganizations { get; set; } = new();
        public List<SelectListItem> AvailableUsers { get; set; } = new();
        public List<InterestCategoryOption> AvailableCategories { get; set; } = new();
    }

    public class InterestCategoryOption
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public bool IsSelected { get; set; }
    }

    public class ClubManageMembersViewModel
    {
        public ulong ClubId { get; set; }
        public string ClubName { get; set; } = string.Empty;
        public string ClubSlug { get; set; } = string.Empty;
        public bool IsPresidentOrAdmin { get; set; }
        public int TotalMembersCount { get; set; }
        public int PendingRequestsCount { get; set; }
        public int FollowersCount { get; set; }

        public List<ClubMemberItem> PendingRequests { get; set; } = new();
        public List<ClubMemberItem> ActiveMembers { get; set; } = new();
        public List<ClubMemberItem> Followers { get; set; } = new();
    }

    public class UserInterestsViewModel
    {
        public List<InterestSelectionItem> Categories { get; set; } = new();
        public List<ulong> SelectedCategoryIds { get; set; } = new();
        public int TotalSelectedCount => SelectedCategoryIds?.Count ?? 0;
        public string? ReturnUrl { get; set; }
    }

    public class InterestSelectionItem
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string InterestLevel { get; set; } = "MEDIUM"; // LOW, MEDIUM, HIGH
        public bool IsSelected { get; set; }
    }

    public class MyClubsViewModel
    {
        public List<ClubCardViewModel> FollowedClubs { get; set; } = new();
        public List<ClubCardViewModel> MembershipClubs { get; set; } = new();
        public List<ClubCardViewModel> ManagedClubs { get; set; } = new();
        public List<ClubCardViewModel> RecommendedClubs { get; set; } = new();
    }
}
