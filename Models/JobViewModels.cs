using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    // =========================================================
    // 1. CANONICAL JOB / OPPORTUNITY ITEM VIEW MODEL
    // =========================================================
    public class JobPostingItem
    {
        public ulong Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CompanyOrDepartment { get; set; } = "Hawassa University";
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string Responsibilities { get; set; } = string.Empty;
        public string JobType { get; set; } = "INTERNSHIP"; // FULL_TIME, PART_TIME, INTERNSHIP, VOLUNTEER, ASSISTANTSHIP
        public string WorkplaceType { get; set; } = "ON_SITE"; // ON_SITE, REMOTE, HYBRID
        public string Location { get; set; } = "Main Campus, Hawassa";
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }
        public string SalaryCurrency { get; set; } = "ETB";
        public string ApplicationEmail { get; set; } = "careers@hawassa.edu.et";
        public string ApplicationUrl { get; set; } = string.Empty;
        public DateTime? PublishedAt { get; set; }
        public DateTime? DeadlineAt { get; set; }
        public string Status { get; set; } = "PUBLISHED";
        public int TotalApplicants { get; set; }
        public bool IsActive => Status == "PUBLISHED" && (!DeadlineAt.HasValue || DeadlineAt.Value > DateTime.UtcNow);
    }

    // =========================================================
    // 2. JOB LIST & SEARCH VIEW MODEL
    // =========================================================
    public class JobListViewModel
    {
        public List<JobPostingItem> Jobs { get; set; } = new();
        public string SearchQuery { get; set; } = string.Empty;
        public string SelectedJobType { get; set; } = string.Empty;
        public string SelectedWorkplaceType { get; set; } = string.Empty;
        public string SelectedDepartment { get; set; } = string.Empty;
        public int TotalActiveOpportunities => Jobs.Count;
    }

    // =========================================================
    // 3. JOB APPLICATION SUBMISSION VIEW MODEL
    // =========================================================
    public class JobApplicationViewModel
    {
        public ulong JobPostingId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Valid Email Address is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact phone number is required.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        public string Phone { get; set; } = string.Empty;

        public string StudentId { get; set; } = string.Empty;
        public string YearOfStudy { get; set; } = "3rd Year";
        public string Gpa { get; set; } = string.Empty;
        public string PortfolioUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please include a brief statement or cover letter.")]
        public string CoverLetter { get; set; } = string.Empty;

        public string ResumeUrl { get; set; } = string.Empty;
    }

    // =========================================================
    // 4. CREATE NEW JOB / OPPORTUNITY VIEW MODEL
    // =========================================================
    public class JobCreateViewModel
    {
        [Required(ErrorMessage = "Job Title is required.")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Hosting Department or Organization is required.")]
        public string CompanyOrDepartment { get; set; } = "Hawassa University";

        [Required(ErrorMessage = "Job Opportunity Description is required.")]
        public string Description { get; set; } = string.Empty;

        public string Requirements { get; set; } = string.Empty;
        public string Responsibilities { get; set; } = string.Empty;

        [Required]
        public string JobType { get; set; } = "INTERNSHIP";

        [Required]
        public string WorkplaceType { get; set; } = "ON_SITE";

        public string Location { get; set; } = "Main Campus, Hawassa";
        public decimal? SalaryMin { get; set; }
        public decimal? SalaryMax { get; set; }

        [Required(ErrorMessage = "Contact Email is required.")]
        [EmailAddress]
        public string ApplicationEmail { get; set; } = "careers@hawassa.edu.et";

        public DateTime? DeadlineAt { get; set; } = DateTime.UtcNow.AddDays(30);
    }
}
