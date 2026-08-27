using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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
        public IActionResult Index()
        {
            return View();
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
            ViewData["Title"] = "Privacy Policy";
            return View();
        }

        // =====================================================
        // SITEMAP / ALL PAGES DIRECTORY
        // URL: /Home/Sitemap or /Sitemap
        // =====================================================
        [HttpGet]
        public IActionResult Sitemap()
        {
            ViewData["Title"] = "All Pages Directory & Sitemap";
            return View();
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