using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Models;

namespace HawassaUnifiedCampusEventManagementSystem.Controllers
{
    public class AnnouncementsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AnnouncementsController> _logger;

        public AnnouncementsController(ApplicationDbContext db, ILogger<AnnouncementsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =====================================================
        // GET: /Announcements or /Announcements/Index
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Index(string? priority = null, string? type = null, string? search = null)
        {
            ViewData["Title"] = "Campus Announcements & Circulars";

            var vm = new AnnouncementListViewModel
            {
                SelectedPriority = priority,
                SelectedType = type,
                SearchTerm = search
            };

            try
            {
                var now = DateTime.UtcNow;

                var query = _db.announcements
                    .Include(a => a.author)
                    .Include(a => a.department)
                    .Where(a => a.status == "PUBLISHED" && (a.expires_at == null || a.expires_at > now))
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(priority) && priority != "ALL")
                {
                    query = query.Where(a => a.priority == priority.ToUpper());
                }

                if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
                {
                    query = query.Where(a => a.announcement_type == type.ToUpper());
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(a =>
                        a.title.ToLower().Contains(s) ||
                        a.content.ToLower().Contains(s) ||
                        (a.summary != null && a.summary.ToLower().Contains(s)) ||
                        (a.department != null && a.department.name.ToLower().Contains(s)));
                }

                var entities = await query
                    .OrderByDescending(a => a.priority == "URGENT" || a.priority == "HIGH")
                    .ThenByDescending(a => a.published_at ?? a.created_at)
                    .ToListAsync();

                vm.Announcements = entities.Select(a => new AnnouncementItemViewModel
                {
                    Id = a.id,
                    Title = a.title,
                    Slug = a.slug,
                    Summary = a.summary,
                    Content = a.content,
                    Priority = a.priority,
                    AnnouncementType = a.announcement_type,
                    ImageUrl = a.image_url,
                    AuthorName = a.author != null 
                        ? ((a.author.account_type == "SUPERADMIN" || a.author.account_type == "ADMIN" || a.author.username == "superadmin" || a.author.username == "admin") 
                            ? "University Communications" 
                            : $"{a.author.first_name} {a.author.last_name}".Trim()) 
                        : "University Communications",
                    DepartmentName = a.department != null ? a.department.name : "Campus Directorate",
                    PublishedDate = a.published_at ?? a.created_at,
                    ExpiresDate = a.expires_at
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve announcements from database.");
            }

            return View(vm);
        }

        // =====================================================
        // GET: /Announcements/Details/5
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Details(ulong id)
        {
            try
            {
                var now = DateTime.UtcNow;

                var a = await _db.announcements
                    .Include(x => x.author)
                    .Include(x => x.department)
                    .FirstOrDefaultAsync(x => x.id == id && x.status == "PUBLISHED" && (x.expires_at == null || x.expires_at > now));

                if (a == null)
                {
                    TempData["ErrorMessage"] = "The requested announcement was not found or has expired.";
                    return RedirectToAction(nameof(Index));
                }

                var relatedEntities = await _db.announcements
                    .Where(r => r.id != id && r.status == "PUBLISHED" && (r.expires_at == null || r.expires_at > now))
                    .OrderByDescending(r => r.published_at ?? r.created_at)
                    .Take(3)
                    .ToListAsync();

                var vm = new AnnouncementDetailsViewModel
                {
                    Id = a.id,
                    Title = a.title,
                    Slug = a.slug,
                    Summary = a.summary,
                    Content = a.content,
                    Priority = a.priority,
                    AnnouncementType = a.announcement_type,
                    ImageUrl = a.image_url,
                    AuthorName = a.author != null 
                        ? ((a.author.account_type == "SUPERADMIN" || a.author.account_type == "ADMIN" || a.author.username == "superadmin" || a.author.username == "admin") 
                            ? "University Communications" 
                            : $"{a.author.first_name} {a.author.last_name}".Trim()) 
                        : "University Communications",
                    DepartmentName = a.department != null ? a.department.name : "Campus Directorate",
                    PublishedDate = a.published_at ?? a.created_at,
                    ExpiresDate = a.expires_at,
                    RelatedNotices = relatedEntities.Select(r => new AnnouncementItemViewModel
                    {
                        Id = r.id,
                        Title = r.title,
                        Slug = r.slug,
                        Summary = r.summary,
                        Priority = r.priority,
                        AnnouncementType = r.announcement_type,
                        PublishedDate = r.published_at ?? r.created_at
                    }).ToList()
                };

                ViewData["Title"] = vm.Title;
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve announcement details for ID {Id}", id);
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
