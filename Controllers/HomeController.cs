using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Models;

namespace HawassaUnifiedCampusEventManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext db, ILogger<HomeController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =====================================================
        // HOME PAGE
        // URL: / or /Home or /Home/Index
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = new HomeIndexViewModel();

            try
            {
                vm.TotalActiveEvents = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_db.events, e => e.status == "PUBLISHED" || e.approval_status == "APPROVED");
                vm.TotalDepartments = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_db.departments);
                vm.TotalClubs = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_db.clubs, c => c.status == "ACTIVE");
                vm.TotalVenues = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_db.venues);

                var events = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                    _db.events
                        .Include(e => e.category)
                        .Include(e => e.venue)
                        .Where(e => (e.status == "PUBLISHED" || e.approval_status == "APPROVED") && e.start_at >= DateTime.UtcNow)
                        .OrderBy(e => e.start_at)
                        .Take(3)
                );

                vm.UpcomingEvents = events.Select(e => new HomeEventItemViewModel
                {
                    Id = e.id,
                    Title = e.title,
                    ShortDescription = e.short_description ?? (e.description != null && e.description.Length > 110 ? e.description.Substring(0, 110) + "..." : e.description),
                    ImageUrl = e.image_url,
                    CategoryName = e.category?.name ?? "Academic",
                    VenueName = e.venue?.name ?? "Main Campus",
                    StartDate = e.start_at,
                    FormattedTime = e.start_at.ToString("hh:mm tt"),
                    Slug = e.slug
                }).ToList();

                var announcements = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                    _db.announcements
                        .Include(a => a.department)
                        .Where(a => a.status == "PUBLISHED")
                        .OrderByDescending(a => a.created_at)
                        .Take(2)
                );

                vm.LatestAnnouncements = announcements.Select(a => new HomeAnnouncementItemViewModel
                {
                    Id = a.id,
                    Title = a.title,
                    Content = a.content != null && a.content.Length > 160 ? a.content.Substring(0, 160) + "..." : a.content,
                    Priority = a.priority ?? "NORMAL",
                    DepartmentName = a.department?.name ?? "Office of the Registrar",
                    CreatedAt = a.created_at
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load dynamic data for home page.");
            }

            return View(vm);
        }

        // =====================================================
        // ABOUT PAGE
        // URL: /Home/About
        // =====================================================
        [HttpGet]
        public IActionResult About()
        {
            ViewData["Title"] = "About HUCEMS";
            return View();
        }

        // =====================================================
        // PRIVACY PAGE
        // URL: /Home/Privacy
        // =====================================================
        [HttpGet]
        public IActionResult Privacy()
        {
            ViewData["Title"] = "Privacy Policy & Terms";
            return View();
        }

        // =====================================================
        // TERMS OF SERVICE PAGE
        // URL: /Home/Terms
        // =====================================================
        [HttpGet]
        public IActionResult Terms()
        {
            ViewData["Title"] = "Terms of Service & Platform Policy";
            return View("Privacy");
        }



        // =====================================================
        // CONTACT PAGE
        // URL: /Home/Contact or /Contact
        // =====================================================
        [HttpGet]
        public IActionResult Contact()
        {
            ViewData["Title"] = "Contact Campus Administration";
            var vm = new ContactViewModel();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Contact Campus Administration";
                return View(model);
            }

            try
            {
                var inquiryAudit = new audit_log
                {
                    action = "CONTACT_INQUIRY",
                    entity_type = "COMMUNICATION",
                    description = $"{model.FullName}|{model.Email}|{model.Phone ?? "N/A"}|{model.Subject}|{model.Department}|{model.Message}",
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    user_agent = Request.Headers["User-Agent"].ToString(),
                    created_at = DateTime.UtcNow
                };

                _db.audit_logs.Add(inquiryAudit);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Contact inquiry received from {Email} regarding {Subject}", model.Email, model.Subject);
                TempData["SuccessMessage"] = $"Thank you, {model.FullName}. Your inquiry regarding '{model.Subject}' has been submitted to the HUCEMS Campus Administration Desk.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist contact inquiry to database.");
                TempData["SuccessMessage"] = $"Thank you, {model.FullName}. Your message has been received.";
            }

            return RedirectToAction(nameof(Contact));
        }

        // =====================================================
        // ERROR PAGE
        // =====================================================
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true
        )]
        public IActionResult Error(int? statusCode = null)
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            ViewBag.StatusCode = statusCode;

            return View(new ErrorViewModel
            {
                RequestId = requestId
            });
        }
    }
}