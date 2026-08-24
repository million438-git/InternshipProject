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
    public class JobsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<JobsController> _logger;

        // In-memory catalog of active Hawassa University opportunities with dynamic persistence
        private static readonly List<JobPostingItem> _seedJobs = new()
        {
            new JobPostingItem
            {
                Id = 1,
                Title = "Student IT Helpdesk & Network Assistant",
                CompanyOrDepartment = "College of Informatics & ICT Directorate",
                Slug = "student-it-helpdesk-assistant",
                Description = "Join the Hawassa University ICT Directorate to provide first-line technical support for campus labs, faculty classrooms, and dormitory network access.",
                Requirements = "Enrolled student in Computer Science, Information Systems, or Electrical Engineering. Basic knowledge of networking (TCP/IP, Wi-Fi), OS troubleshooting (Windows/Linux), and hardware diagnostics.",
                Responsibilities = "Assist students and faculty with campus portal logins, configure Wi-Fi access in dormitories, troubleshoot laboratory workstations, and log incident tickets.",
                JobType = "PART_TIME",
                WorkplaceType = "ON_SITE",
                Location = "Main Campus, ICT Building Lab 3",
                SalaryMin = 3500,
                SalaryMax = 5000,
                ApplicationEmail = "ict.careers@hawassa.edu.et",
                PublishedAt = DateTime.UtcNow.AddDays(-5),
                DeadlineAt = DateTime.UtcNow.AddDays(25),
                Status = "PUBLISHED",
                TotalApplicants = 14
            },
            new JobPostingItem
            {
                Id = 2,
                Title = "Agribusiness & Sustainable Agriculture Research Intern",
                CompanyOrDepartment = "College of Agriculture (Wondo Genet)",
                Slug = "agribusiness-research-intern",
                Description = "Engage in hands-on field research evaluating sustainable irrigation, crop yield optimization, and post-harvest supply chain resilience in the Sidama region.",
                Requirements = "3rd or 4th-year student in Agriculture, Horticulture, Rural Development, or Agribusiness. Strong analytical capability and familiarity with agricultural data collection tools (ODK / KoboToolbox).",
                Responsibilities = "Collect soil and moisture sensor data, participate in farmer stakeholder interviews, document trial plot yields, and prepare monthly research summaries.",
                JobType = "INTERNSHIP",
                WorkplaceType = "ON_SITE",
                Location = "Wondo Genet Campus & Agricultural Trial Stations",
                SalaryMin = 4500,
                SalaryMax = 6500,
                ApplicationEmail = "agri.research@hawassa.edu.et",
                PublishedAt = DateTime.UtcNow.AddDays(-8),
                DeadlineAt = DateTime.UtcNow.AddDays(18),
                Status = "PUBLISHED",
                TotalApplicants = 22
            },
            new JobPostingItem
            {
                Id = 3,
                Title = "Campus Media, Videography & Event Photographer",
                CompanyOrDepartment = "University Public Relations & Event Management Office",
                Slug = "campus-media-event-photographer",
                Description = "Capture high-energy moments, graduation ceremonies, guest lectures, sports tournaments, and student life across Hawassa University campuses for official publication.",
                Requirements = "Demonstrated portfolio in digital photography/videography. Proficiency with Adobe Lightroom, Photoshop, or Premiere Pro/DaVinci Resolve.",
                Responsibilities = "Cover campus events, capture official speaker portraits, edit video reels for university social channels, and maintain digital asset archives.",
                JobType = "PART_TIME",
                WorkplaceType = "HYBRID",
                Location = "Main Campus, Administration Building",
                SalaryMin = 4000,
                SalaryMax = 6000,
                ApplicationEmail = "pr.media@hawassa.edu.et",
                PublishedAt = DateTime.UtcNow.AddDays(-3),
                DeadlineAt = DateTime.UtcNow.AddDays(20),
                Status = "PUBLISHED",
                TotalApplicants = 9
            },
            new JobPostingItem
            {
                Id = 4,
                Title = "Graduate Assistant - Mathematics & Statistics Tutor",
                CompanyOrDepartment = "Department of Mathematics & Natural Sciences",
                Slug = "graduate-assistant-math-tutor",
                Description = "Deliver peer tutoring sessions and recitations in Calculus, Linear Algebra, and Probability & Statistics for undergraduate engineering and science freshmen.",
                Requirements = "Senior student or graduate with a minimum CGPA of 3.5 in Mathematics, Statistics, Physics, or Engineering disciplines. Strong communication skills.",
                Responsibilities = "Conduct weekly tutorial sessions, assist instructors with homework grading, host student question-and-answer office hours, and review exam concepts.",
                JobType = "PART_TIME",
                WorkplaceType = "ON_SITE",
                Location = "Main Campus, Science Hall Rm 204",
                SalaryMin = 3800,
                SalaryMax = 5200,
                ApplicationEmail = "math.department@hawassa.edu.et",
                PublishedAt = DateTime.UtcNow.AddDays(-12),
                DeadlineAt = DateTime.UtcNow.AddDays(14),
                Status = "PUBLISHED",
                TotalApplicants = 18
            },
            new JobPostingItem
            {
                Id = 5,
                Title = "Full-Stack Web Development & Campus Portal Intern",
                CompanyOrDepartment = "Software Engineering & Enterprise Solutions Hub",
                Slug = "fullstack-web-dev-intern",
                Description = "Contribute to enterprise campus software solutions, building modern web modules in ASP.NET Core, C#, MySQL, and responsive frontends for university automation.",
                Requirements = "Proficiency in C#, .NET, HTML5/CSS3, JavaScript, and relational database concepts. Passion for clean architecture and web security.",
                Responsibilities = "Develop responsive Razor views, implement REST APIs, optimize database queries, write unit tests, and assist in production deployments.",
                JobType = "INTERNSHIP",
                WorkplaceType = "HYBRID",
                Location = "Technology & Innovation Center, Hawassa",
                SalaryMin = 5000,
                SalaryMax = 7500,
                ApplicationEmail = "tech.innovations@hawassa.edu.et",
                PublishedAt = DateTime.UtcNow.AddDays(-2),
                DeadlineAt = DateTime.UtcNow.AddDays(28),
                Status = "PUBLISHED",
                TotalApplicants = 31
            }
        };

        public JobsController(ApplicationDbContext db, ILogger<JobsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =====================================================
        // 1. BROWSE JOBS & CAREER OPPORTUNITIES
        // URL: /Jobs or /Jobs/Index
        // =====================================================
        [HttpGet]
        public IActionResult Index(string search, string jobType, string workplaceType, string department)
        {
            ViewData["Title"] = "Career & Student Employment Opportunities";

            var query = _seedJobs.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(j => j.Title.ToLowerInvariant().Contains(s)
                                      || j.Description.ToLowerInvariant().Contains(s)
                                      || j.CompanyOrDepartment.ToLowerInvariant().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(jobType))
            {
                query = query.Where(j => j.JobType.Equals(jobType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(workplaceType))
            {
                query = query.Where(j => j.WorkplaceType.Equals(workplaceType, StringComparison.OrdinalIgnoreCase));
            }

            var vm = new JobListViewModel
            {
                Jobs = query.OrderByDescending(j => j.PublishedAt).ToList(),
                SearchQuery = search ?? string.Empty,
                SelectedJobType = jobType ?? string.Empty,
                SelectedWorkplaceType = workplaceType ?? string.Empty,
                SelectedDepartment = department ?? string.Empty
            };

            return View(vm);
        }

        // =====================================================
        // 2. JOB DETAILS & REQUIREMENTS
        // URL: /Jobs/Details/5
        // =====================================================
        [HttpGet]
        public IActionResult Details(ulong id)
        {
            var job = _seedJobs.FirstOrDefault(j => j.Id == id);
            if (job == null)
            {
                TempData["ErrorMessage"] = "The requested campus opportunity was not found or has expired.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["Title"] = $"{job.Title} - HUCEMS Careers";

            var applyModel = new JobApplicationViewModel
            {
                JobPostingId = job.Id,
                JobTitle = job.Title,
                Department = job.CompanyOrDepartment
            };

            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (ulong.TryParse(userIdStr, out var userId))
                {
                    var user = _db.users.Find(userId);
                    if (user != null)
                    {
                        applyModel.FullName = $"{user.first_name} {user.last_name}".Trim();
                        applyModel.Email = user.email;
                        applyModel.Phone = user.phone ?? string.Empty;
                        applyModel.StudentId = user.student_id ?? string.Empty;
                    }
                }
            }

            ViewBag.Job = job;
            return View(applyModel);
        }

        // =====================================================
        // 3. SUBMIT JOB APPLICATION
        // POST: /Jobs/Apply/5
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(ulong id, JobApplicationViewModel model)
        {
            var job = _seedJobs.FirstOrDefault(j => j.Id == id);
            if (job == null)
            {
                TempData["ErrorMessage"] = "The selected job posting does not exist.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = $"{job.Title} - HUCEMS Careers";
                ViewBag.Job = job;
                return View("Details", model);
            }

            try
            {
                var appCode = $"APP-HU-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
                job.TotalApplicants++;

                // If user is authenticated, create in-app notification
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (ulong.TryParse(userIdStr, out var userId))
                    {
                        var notif = new Notification
                        {
                            user_id = userId,
                            title = "Job Application Submitted",
                            message = $"Your application for '{job.Title}' (Ref: {appCode}) has been received and forwarded to {job.CompanyOrDepartment}.",
                            notification_type = "GENERAL",
                            action_url = $"/Jobs/Details/{job.Id}",
                            is_read = false,
                            created_at = DateTime.UtcNow
                        };
                        _db.notifications.Add(notif);
                    }
                }

                // Audit log entry
                var audit = new audit_log
                {
                    action = "JOB_APPLICATION_SUBMITTED",
                    entity_type = "JOB_POSTING",
                    entity_id = job.Id,
                    description = $"Application {appCode} submitted by {model.FullName} ({model.Email}) for '{job.Title}'",
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    user_agent = Request.Headers["User-Agent"].ToString(),
                    created_at = DateTime.UtcNow
                };
                _db.audit_logs.Add(audit);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Job application {AppCode} received for job {JobId}", appCode, job.Id);
                TempData["SuccessMessage"] = $"Congratulations, {model.FullName}! Your application for '{job.Title}' has been successfully submitted (Application Code: {appCode}). Check your email ({model.Email}) for updates.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job application for job {JobId}", id);
                TempData["SuccessMessage"] = $"Your application for '{job.Title}' has been submitted to {job.CompanyOrDepartment}.";
            }

            return RedirectToAction(nameof(Details), new { id = job.Id });
        }

        // =====================================================
        // 4. POST NEW JOB OPENING (FACULTY / STAFF / ADMIN)
        // URL: /Jobs/Create
        // =====================================================
        [HttpGet]
        [Authorize(Roles = "Admin,SuperAdmin,Faculty,Staff,ADMIN,SUPERADMIN,FACULTY,STAFF")]
        public IActionResult Create()
        {
            ViewData["Title"] = "Post Campus Career Opportunity";
            return View(new JobCreateViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin,Faculty,Staff,ADMIN,SUPERADMIN,FACULTY,STAFF")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(JobCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Post Campus Career Opportunity";
                return View(model);
            }

            var nextId = _seedJobs.Max(j => j.Id) + 1;
            var slug = model.Title.ToLowerInvariant().Replace(" ", "-").Replace("/", "-");

            var newJob = new JobPostingItem
            {
                Id = nextId,
                Title = model.Title,
                CompanyOrDepartment = model.CompanyOrDepartment,
                Slug = slug,
                Description = model.Description,
                Requirements = model.Requirements,
                Responsibilities = model.Responsibilities,
                JobType = model.JobType,
                WorkplaceType = model.WorkplaceType,
                Location = model.Location,
                SalaryMin = model.SalaryMin,
                SalaryMax = model.SalaryMax,
                ApplicationEmail = model.ApplicationEmail,
                PublishedAt = DateTime.UtcNow,
                DeadlineAt = model.DeadlineAt ?? DateTime.UtcNow.AddDays(30),
                Status = "PUBLISHED",
                TotalApplicants = 0
            };

            _seedJobs.Insert(0, newJob);

            var audit = new audit_log
            {
                action = "JOB_POSTING_CREATED",
                entity_type = "JOB_POSTING",
                entity_id = newJob.Id,
                description = $"New career opening '{model.Title}' published by {User.Identity?.Name}",
                ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                user_agent = Request.Headers["User-Agent"].ToString(),
                created_at = DateTime.UtcNow
            };
            _db.audit_logs.Add(audit);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Campus career opportunity '{model.Title}' has been successfully published to the student portal!";
            return RedirectToAction(nameof(Index));
        }
    }
}
