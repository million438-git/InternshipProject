using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Models;
using HawassaUnifiedCampusEventManagementSystem.Services;

namespace HawassaUnifiedCampusEventManagementSystem.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<EventsController> _logger;
        private readonly IWebHostEnvironment _env;

        public EventsController(ApplicationDbContext db, ILogger<EventsController> logger, IWebHostEnvironment env)
        {
            _db = db;
            _logger = logger;
            _env = env;
        }

        private async Task<string?> ProcessEventImageUploadAsync(IFormFile? imageFile, string? existingImageUrl = null, bool removeImage = false)
        {
            if (removeImage && !string.IsNullOrEmpty(existingImageUrl))
            {
                DeleteLocalImageFile(existingImageUrl);
                existingImageUrl = null;
            }

            if (imageFile == null || imageFile.Length == 0)
            {
                return removeImage ? null : existingImageUrl;
            }

            // 1. Validate file size (max 5MB)
            const long maxFileSize = 5 * 1024 * 1024;
            if (imageFile.Length > maxFileSize)
            {
                throw new InvalidOperationException("Event image file size cannot exceed 5 MB.");
            }

            // 2. Validate file extension
            var ext = Path.GetExtension(imageFile.FileName)?.ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
            {
                throw new InvalidOperationException("Invalid image format. Allowed formats: JPG, PNG, and WebP.");
            }

            // 3. Validate MIME type
            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/pjpeg" };
            if (!allowedMimeTypes.Contains(imageFile.ContentType.ToLowerInvariant()))
            {
                throw new InvalidOperationException("Invalid file content type. Please upload a valid image.");
            }

            // 4. Clean up previous physical image if replacing
            if (!string.IsNullOrEmpty(existingImageUrl))
            {
                DeleteLocalImageFile(existingImageUrl);
            }

            // 5. Generate secure unique filename and ensure directory exists
            var uniqueFileName = $"event-{Guid.NewGuid():N}{ext}";
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsFolder = Path.Combine(webRoot, "uploads", "events");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/uploads/events/{uniqueFileName}";
        }

        private void DeleteLocalImageFile(string? relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeUrl) || !relativeUrl.StartsWith("/uploads/events/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var fileName = Path.GetFileName(relativeUrl);
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var physicalPath = Path.Combine(webRoot, "uploads", "events", fileName);
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old event image file: {Url}", relativeUrl);
            }
        }

        private ulong? GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdStr) && ulong.TryParse(userIdStr, out ulong parsedId))
            {
                return parsedId;
            }
            return null;
        }

        private bool IsAdminOrSuperAdmin()
        {
            return RoleClaims.IsAdmin(User);
        }

        // Map EF entity to view model
        private Event ToViewModel(_event e, ulong? currentUserId = null)
        {
            if (e == null) return new Event();

            var isRegistered = false;
            if (currentUserId.HasValue && e.registrations != null && e.registrations.Any())
            {
                isRegistered = e.registrations.Any(r => r.user_id == currentUserId.Value && (r.status == "REGISTERED" || r.status == "CONFIRMED"));
            }

            var vm = new Event
            {
                Id = e.id,
                Title = e.title,
                Category = e.category?.name,
                Capacity = e.capacity.HasValue ? (int?)e.capacity.Value : null,
                Description = e.description ?? string.Empty,
                EventDate = e.start_at,
                Venue = e.venue?.name,
                StartTime = e.start_at.TimeOfDay,
                EndTime = e.end_at != default && e.end_at != e.start_at ? e.end_at.TimeOfDay : (TimeSpan?)null,
                OrganizerId = e.organizer_id,
                Organizer = e.organizer != null ? ($"{e.organizer.first_name} {e.organizer.last_name}".Trim()) : null,
                OrganizerEmail = e.organizer?.email,
                ContactPhone = e.organizer?.phone,
                IsPublished = e.is_public ?? false,
                ApprovalStatus = e.approval_status ?? "APPROVED",
                Status = e.status ?? "PUBLISHED",
                IsUserRegistered = isRegistered,
                RegisteredCount = e.registrations != null ? e.registrations.Count(r => r.status == "REGISTERED" || r.status == "CONFIRMED") : 0,
                ShortDescription = e.short_description,
                ImageUrl = e.image_url,
                Slug = e.slug,
                CreatedAt = e.created_at
            };

            // Map Discussions
            if (e.event_comments != null)
            {
                vm.Comments = e.event_comments
                    .Where(c => !c.is_deleted)
                    .OrderByDescending(c => c.created_at)
                    .Select(c => new EventCommentItemViewModel
                    {
                        Id = c.id,
                        UserId = c.user_id,
                        UserName = c.user != null ? $"{c.user.first_name} {c.user.last_name}".Trim() : "Campus Attendee",
                        CommentText = c.comment,
                        CreatedAt = c.created_at,
                        CanDelete = currentUserId.HasValue && (currentUserId.Value == c.user_id || currentUserId.Value == e.organizer_id || IsAdminOrSuperAdmin())
                    }).ToList();
            }

            // Map Feedback & Ratings
            if (e.event_feedbacks != null && e.event_feedbacks.Any())
            {
                vm.Feedbacks = e.event_feedbacks
                    .OrderByDescending(f => f.created_at)
                    .Select(f => new EventFeedbackItemViewModel
                    {
                        Id = f.id,
                        UserName = f.is_anonymous ? "Anonymous Attendee" : (f.user != null ? $"{f.user.first_name} {f.user.last_name}".Trim() : "Campus Attendee"),
                        Rating = f.rating,
                        Comment = f.comment,
                        IsAnonymous = f.is_anonymous,
                        CreatedAt = f.created_at
                    }).ToList();

                vm.TotalRatings = e.event_feedbacks.Count;
                vm.AverageRating = Math.Round(e.event_feedbacks.Average(f => (double)f.rating), 1);

                if (currentUserId.HasValue)
                {
                    var myRating = e.event_feedbacks.FirstOrDefault(f => f.user_id == currentUserId.Value);
                    if (myRating != null)
                    {
                        vm.HasUserRated = true;
                        vm.UserRating = myRating.rating;
                    }
                }
            }

            return vm;
        }

        // =========================================================
        // GET: /Events
        // =========================================================
        public async Task<IActionResult> Index()
        {
            var currentUserId = GetCurrentUserId();
            var isAdmin = IsAdminOrSuperAdmin();

            var query = _db.events
                .Include(x => x.category)
                .Include(x => x.venue)
                .Include(x => x.organizer)
                .Include(x => x.registrations)
                .Include(x => x.event_feedbacks)
                .AsQueryable();

            // Normal users and guests see published & approved events. Admins see all events.
            if (!isAdmin)
            {
                if (currentUserId.HasValue)
                {
                    query = query.Where(x => (x.is_public == true && x.approval_status == "APPROVED") || x.organizer_id == currentUserId.Value);
                }
                else
                {
                    query = query.Where(x => x.is_public == true && x.approval_status == "APPROVED");
                }
            }

            var items = await query.OrderByDescending(x => x.start_at).ToListAsync();
            var vm = items.Select(e => ToViewModel(e, currentUserId));
            return View(vm);
        }

        // =========================================================
        // GET: /Events/Details/5
        // =========================================================
        public async Task<IActionResult> Details(ulong? id)
        {
            if (id == null || id == 0) return NotFound();

            var currentUserId = GetCurrentUserId();

            var e = await _db.events
                .Include(x => x.category)
                .Include(x => x.venue)
                .Include(x => x.organizer)
                .Include(x => x.registrations)
                .Include(x => x.event_comments).ThenInclude(c => c.user)
                .Include(x => x.event_feedbacks).ThenInclude(f => f.user)
                .FirstOrDefaultAsync(x => x.id == id.Value);

            if (e == null) return NotFound();

            return View(ToViewModel(e, currentUserId));
        }

        // =========================================================
        // GET: /Events/Create
        // Allowed Roles: Faculty, Staff, Organization, Club, Admin, SuperAdmin
        // =========================================================
        [Authorize(Roles = "Faculty,Staff,Organization,Club,Admin,SuperAdmin,FACULTY,STAFF,ORGANIZATION,ADMIN,SUPERADMIN")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _db.event_categories.Where(c => c.is_active == true).ToListAsync();
            ViewBag.Venues = await _db.venues.Where(v => v.status == "AVAILABLE").ToListAsync();
            return View(new Event { EventDate = DateTime.Today.AddDays(3), StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(11) });
        }

        // =========================================================
        // POST: /Events/Create
        // =========================================================
        [Authorize(Roles = "Faculty,Staff,Organization,Club,Admin,SuperAdmin,FACULTY,STAFF,ORGANIZATION,ADMIN,SUPERADMIN")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _db.event_categories.Where(c => c.is_active == true).ToListAsync();
                ViewBag.Venues = await _db.venues.Where(v => v.status == "AVAILABLE").ToListAsync();
                return View(model);
            }

            var organizerId = GetCurrentUserId();
            if (!organizerId.HasValue)
            {
                return Unauthorized();
            }

            var isAdmin = IsAdminOrSuperAdmin();

            // Calculate start and end times satisfying MySQL chk_events_dates (end_at > start_at)
            var startAt = model.EventDate.Date + model.StartTime;
            DateTime endAt;
            if (model.EndTime.HasValue && model.EndTime.Value > model.StartTime)
            {
                endAt = model.EventDate.Date + model.EndTime.Value;
            }
            else
            {
                endAt = startAt.AddHours(2);
            }

            // Resolve Category ID dynamically
            ulong categoryId = 1;
            if (!string.IsNullOrWhiteSpace(model.Category))
            {
                var matchingCat = await _db.event_categories.FirstOrDefaultAsync(c => c.name.ToLower() == model.Category.ToLower() || c.slug.ToLower() == model.Category.ToLower());
                if (matchingCat != null)
                {
                    categoryId = matchingCat.id;
                }
                else
                {
                    var firstCat = await _db.event_categories.FirstOrDefaultAsync();
                    if (firstCat != null) categoryId = firstCat.id;
                }
            }
            else
            {
                var firstCat = await _db.event_categories.FirstOrDefaultAsync();
                if (firstCat != null) categoryId = firstCat.id;
            }

            // Resolve Venue ID if provided
            ulong? venueId = null;
            if (!string.IsNullOrWhiteSpace(model.Venue))
            {
                var mv = model.Venue.Trim().ToLower();
                var matchingVenue = await _db.venues.FirstOrDefaultAsync(v => v.name.ToLower() == mv || v.name.ToLower().Contains(mv) || mv.Contains(v.name.ToLower()));
                if (matchingVenue != null)
                {
                    venueId = matchingVenue.id;
                }
                else
                {
                    var firstVenue = await _db.venues.FirstOrDefaultAsync();
                    if (firstVenue != null) venueId = firstVenue.id;
                }
            }
            else
            {
                var firstVenue = await _db.venues.FirstOrDefaultAsync();
                if (firstVenue != null) venueId = firstVenue.id;
            }

            // Venue Overlap & Double-Booking Conflict Prevention
            if (venueId.HasValue)
            {
                var conflictingEvent = await _db.events
                    .Include(e => e.venue)
                    .FirstOrDefaultAsync(e => e.venue_id == venueId.Value &&
                                              (e.status != "CANCELLED" && e.approval_status != "REJECTED") &&
                                              (startAt < e.end_at && endAt > e.start_at));

                if (conflictingEvent != null)
                {
                    ModelState.AddModelError("Venue", $"The venue '{conflictingEvent.venue?.name ?? "Selected Venue"}' is already reserved for '{conflictingEvent.title}' on {conflictingEvent.start_at:MMM dd} from {conflictingEvent.start_at:hh:mm tt} to {conflictingEvent.end_at:hh:mm tt}. Please select a different time or venue.");
                    ViewBag.Categories = await _db.event_categories.Where(c => c.is_active == true).ToListAsync();
                    ViewBag.Venues = await _db.venues.Where(v => v.status == "AVAILABLE").ToListAsync();
                    return View(model);
                }
            }

            // Generate unique slug
            var baseSlug = !string.IsNullOrWhiteSpace(model.Slug)
                ? model.Slug.Trim().ToLower().Replace(' ', '-')
                : (!string.IsNullOrWhiteSpace(model.Title) ? model.Title.Trim().ToLower().Replace(' ', '-') : "campus-event");
            var uniqueSlug = $"{baseSlug}-{Guid.NewGuid().ToString("N")[..6]}";

            // Process Event Image Upload (if provided)
            string? uploadedImageUrl = model.ImageUrl;
            try
            {
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    uploadedImageUrl = await ProcessEventImageUploadAsync(model.ImageFile);
                }
            }
            catch (Exception imgEx)
            {
                ModelState.AddModelError("ImageFile", imgEx.Message);
                ViewBag.Categories = await _db.event_categories.Where(c => c.is_active == true).ToListAsync();
                ViewBag.Venues = await _db.venues.Where(v => v.status == "AVAILABLE").ToListAsync();
                return View(model);
            }

            // Lifecycle status: Admin/SuperAdmin are auto-approved; Faculty/Staff/Clubs go to PENDING approval
            string approvalStatus = isAdmin ? "APPROVED" : "PENDING";
            string eventStatus = isAdmin ? "PUBLISHED" : "DRAFT";
            bool isPublic = isAdmin ? model.IsPublished : false;

            var entity = new _event
            {
                title = model.Title ?? "Campus Event",
                slug = uniqueSlug,
                description = model.Description,
                short_description = model.ShortDescription,
                start_at = startAt,
                end_at = endAt,
                capacity = model.Capacity.HasValue ? (uint?)model.Capacity.Value : null,
                is_public = isPublic,
                image_url = uploadedImageUrl,
                organizer_id = organizerId.Value,
                category_id = categoryId,
                venue_id = venueId,
                event_mode = "IN_PERSON",
                status = eventStatus,
                approval_status = approvalStatus,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            var organizerUser = await _db.users.Include(u => u.department).FirstOrDefaultAsync(u => u.id == organizerId.Value);

            _db.events.Add(entity);
            await _db.SaveChangesAsync();

            // Personalization: Dispatch push alerts to students subscribed to this department
            if (entity.status == "PUBLISHED" && organizerUser?.department_id != null)
            {
                try
                {
                    var deptId = organizerUser.department_id.Value;
                    var deptName = organizerUser.department?.name ?? "Academic Department";
                    var subscribers = await _db.user_dept_subscriptions
                        .Where(s => s.department_id == deptId && s.notify_on_new_event)
                        .ToListAsync();

                    foreach (var sub in subscribers)
                    {
                        if (sub.user_id != organizerId.Value)
                        {
                            _db.notifications.Add(new Notification
                            {
                                user_id = sub.user_id,
                                title = $"New Event: {deptName}",
                                message = $"{deptName} posted a new event: '{entity.title}' on {entity.start_at:MMM dd, yyyy}. Reserve your spot now!",
                                notification_type = "EVENT",
                                related_entity_type = "EVENT",
                                related_entity_id = entity.id,
                                action_url = $"/Events/Details/{entity.id}",
                                is_read = false,
                                created_at = DateTime.UtcNow
                            });
                        }
                    }
                    await _db.SaveChangesAsync();
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Could not dispatch department event subscription alerts.");
                }
            }

            if (isAdmin)
            {
                TempData["SuccessMessage"] = "Event created and published successfully!";
            }
            else
            {
                TempData["SuccessMessage"] = "Event submitted successfully! It is now pending administrative approval before being published on campus.";
            }

            return RedirectToAction(nameof(Details), new { id = (long)entity.id });
        }

        // =========================================================
        // GET: /Events/Edit/5
        // Allowed: Event Organizer (Owner) OR Admin / SuperAdmin
        // =========================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(ulong? id)
        {
            if (id == null || id == 0) return NotFound();

            var currentUserId = GetCurrentUserId();
            var isAdmin = IsAdminOrSuperAdmin();

            var e = await _db.events
                .Include(x => x.category)
                .Include(x => x.venue)
                .Include(x => x.organizer)
                .FirstOrDefaultAsync(x => x.id == id.Value);

            if (e == null) return NotFound();

            // Enforce ownership or admin privilege
            if (e.organizer_id != currentUserId && !isAdmin)
            {
                TempData["ErrorMessage"] = "You are not authorized to edit this event.";
                return Forbid();
            }

            ViewBag.Categories = await _db.event_categories.Where(c => c.is_active == true).ToListAsync();
            ViewBag.Venues = await _db.venues.Where(v => v.status == "AVAILABLE").ToListAsync();

            return View(ToViewModel(e, currentUserId));
        }

        // =========================================================
        // POST: /Events/Edit/5
        // =========================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ulong id, Event model)
        {
            if (id != model.Id) return BadRequest();

            var currentUserId = GetCurrentUserId();
            var isAdmin = IsAdminOrSuperAdmin();

            var e = await _db.events.FindAsync(id);
            if (e == null) return NotFound();

            // Enforce ownership or admin privilege
            if (e.organizer_id != currentUserId && !isAdmin)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _db.event_categories.Where(c => c.is_active == true).ToListAsync();
                ViewBag.Venues = await _db.venues.Where(v => v.status == "AVAILABLE").ToListAsync();
                return View(model);
            }

            var startAt = model.EventDate.Date + model.StartTime;
            var endAt = (model.EndTime.HasValue && model.EndTime.Value > model.StartTime)
                ? model.EventDate.Date + model.EndTime.Value
                : startAt.AddHours(2);

            // Venue Overlap & Double-Booking Conflict Prevention
            if (e.venue_id.HasValue)
            {
                var conflictingEvent = await _db.events
                    .Include(ev => ev.venue)
                    .FirstOrDefaultAsync(ev => ev.id != id &&
                                              ev.venue_id == e.venue_id.Value &&
                                              (ev.status != "CANCELLED" && ev.approval_status != "REJECTED") &&
                                              (startAt < ev.end_at && endAt > ev.start_at));

                if (conflictingEvent != null)
                {
                    ModelState.AddModelError("Venue", $"The venue '{conflictingEvent.venue?.name ?? "Selected Venue"}' is already reserved for '{conflictingEvent.title}' on {conflictingEvent.start_at:MMM dd} from {conflictingEvent.start_at:hh:mm tt} to {conflictingEvent.end_at:hh:mm tt}. Please select a different time or venue.");
                    ViewBag.Categories = await _db.event_categories.Where(c => c.is_active == true).ToListAsync();
                    ViewBag.Venues = await _db.venues.Where(v => v.status == "AVAILABLE").ToListAsync();
                    return View(model);
                }
            }

            // Process Event Image Upload / Replacement / Removal
            try
            {
                e.image_url = await ProcessEventImageUploadAsync(model.ImageFile, e.image_url, model.RemoveImage);
            }
            catch (Exception imgEx)
            {
                ModelState.AddModelError("ImageFile", imgEx.Message);
                ViewBag.Categories = await _db.event_categories.Where(c => c.is_active == true).ToListAsync();
                ViewBag.Venues = await _db.venues.Where(v => v.status == "AVAILABLE").ToListAsync();
                return View(model);
            }

            // update fields
            e.title = model.Title ?? e.title;
            e.description = model.Description;
            e.short_description = model.ShortDescription;
            e.start_at = startAt;
            e.end_at = endAt;
            e.capacity = model.Capacity.HasValue ? (uint?)model.Capacity.Value : e.capacity;
            if (isAdmin)
            {
                e.is_public = model.IsPublished;
            }
            e.updated_at = DateTime.UtcNow;

            _db.events.Update(e);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Event updated successfully!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // =========================================================
        // GET: /Events/Delete/5
        // Allowed: Event Organizer (Owner) OR Admin / SuperAdmin
        // =========================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Delete(ulong? id)
        {
            if (id == null || id == 0) return NotFound();

            var currentUserId = GetCurrentUserId();
            var isAdmin = IsAdminOrSuperAdmin();

            var e = await _db.events
                .Include(x => x.category)
                .Include(x => x.venue)
                .Include(x => x.organizer)
                .FirstOrDefaultAsync(x => x.id == id.Value);

            if (e == null) return NotFound();

            // Enforce ownership or admin privilege
            if (e.organizer_id != currentUserId && !isAdmin)
            {
                TempData["ErrorMessage"] = "You are not authorized to delete this event.";
                return Forbid();
            }

            return View(ToViewModel(e, currentUserId));
        }

        // =========================================================
        // POST: /Events/Delete/5
        // Allowed: Event Organizer (Owner) OR Admin / SuperAdmin
        // =========================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(ulong id)
        {
            var currentUserId = GetCurrentUserId();
            var isAdmin = IsAdminOrSuperAdmin();

            var e = await _db.events.FindAsync(id);
            if (e == null) return NotFound();

            // Enforce ownership or admin privilege
            if (e.organizer_id != currentUserId && !isAdmin)
            {
                return Forbid();
            }

            _db.events.Remove(e);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Event deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // POST: /Events/RegisterEvent/5 (Student / Attendee Registration)
        // =========================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterEvent(ulong id)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Events", new { id }) });
            }

            var e = await _db.events
                .Include(ev => ev.registrations)
                .FirstOrDefaultAsync(ev => ev.id == id);

            if (e == null) return NotFound();

            // Check if already registered
            var existingReg = await _db.registrations
                .FirstOrDefaultAsync(r => r.event_id == id && r.user_id == currentUserId.Value);

            if (existingReg != null)
            {
                if (existingReg.status == "REGISTERED")
                {
                    TempData["InfoMessage"] = "You are already registered for this event!";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Re-activate if previously cancelled
                existingReg.status = "REGISTERED";
                existingReg.registered_at = DateTime.UtcNow;
                existingReg.cancelled_at = null;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Your event registration has been reactivated successfully!";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check capacity
            var activeCount = e.registrations.Count(r => r.status == "REGISTERED");
            string regStatus = (e.capacity.HasValue && activeCount >= e.capacity.Value) ? "WAITLISTED" : "REGISTERED";

            var regCode = $"REG-HU-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            var qrToken = Guid.NewGuid().ToString("N");

            var newReg = new Registration
            {
                event_id = id,
                user_id = currentUserId.Value,
                registration_code = regCode,
                qr_token = qrToken,
                status = regStatus,
                registered_at = DateTime.UtcNow
            };

            _db.registrations.Add(newReg);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["InfoMessage"] = "You are already registered for this event!";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Dispatch in-app attendee notification & ticket confirmation
            try
            {
                var notification = new Notification
                {
                    user_id = currentUserId.Value,
                    title = regStatus == "REGISTERED" ? "Event Registration Confirmed" : "Added to Event Waitlist",
                    message = regStatus == "REGISTERED"
                        ? $"You have successfully registered for '{e.title}'. Your ticket code is {regCode}."
                        : $"You have been placed on the waitlist for '{e.title}'. We will notify you if a slot opens.",
                    notification_type = "REGISTRATION",
                    related_entity_type = "EVENT",
                    related_entity_id = e.id,
                    action_url = $"/Events/Details/{e.id}",
                    is_read = false,
                    created_at = DateTime.UtcNow
                };
                _db.notifications.Add(notification);

                // Notify event organizer of new registration
                if (e.organizer_id > 0 && e.organizer_id != currentUserId.Value)
                {
                    var studentUser = await _db.users.FindAsync(currentUserId.Value);
                    var studentName = studentUser != null ? $"{studentUser.first_name} {studentUser.last_name}".Trim() : "A campus member";
                    _db.notifications.Add(new Notification
                    {
                        user_id = e.organizer_id,
                        title = "New Event Registration",
                        message = $"{studentName} has registered for '{e.title}' (Ticket: {regCode}).",
                        notification_type = "EVENT",
                        related_entity_type = "EVENT",
                        related_entity_id = e.id,
                        action_url = $"/Events/Details/{e.id}",
                        is_read = false,
                        created_at = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to dispatch registration confirmation notification.");
            }

            TempData["SuccessMessage"] = regStatus == "REGISTERED"
                ? $"Registration confirmed! Your ticket code is {regCode}."
                : "The event is currently at full capacity. You have been added to the waitlist.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // =========================================================
        // POST: /Events/CancelRegistration/5
        // =========================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRegistration(ulong id)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return Unauthorized();

            var reg = await _db.registrations
                .Include(r => r._event)
                .FirstOrDefaultAsync(r => r.event_id == id && r.user_id == currentUserId.Value && r.status == "REGISTERED");

            if (reg != null)
            {
                reg.status = "CANCELLED";
                reg.cancelled_at = DateTime.UtcNow;

                _db.notifications.Add(new Notification
                {
                    user_id = currentUserId.Value,
                    title = "Registration Cancelled",
                    message = $"Your registration for '{reg._event?.title ?? "the event"}' was cancelled.",
                    notification_type = "REGISTRATION",
                    related_entity_type = "EVENT",
                    related_entity_id = id,
                    action_url = $"/Events/Details/{id}",
                    is_read = false,
                    created_at = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Your registration has been cancelled.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // =========================================================
        // =========================================================
        // GET: /Events/MyEvents
        // =========================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyEvents()
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return View(new StudentMyEventsViewModel());
            }

            // 1. Events the user has registered to attend (Student RSVPs)
            var registeredEventIds = await _db.registrations
                .Where(r => r.user_id == currentUserId.Value && r.status == "REGISTERED")
                .Select(r => r.event_id)
                .ToListAsync();

            var registeredEvents = await _db.events
                .Include(x => x.category)
                .Include(x => x.venue)
                .Include(x => x.organizer)
                .Include(x => x.registrations)
                .Where(x => registeredEventIds.Contains(x.id))
                .OrderBy(x => x.start_at)
                .ToListAsync();

            // 2. Events organized by the user (if any)
            var organizedEvents = await _db.events
                .Include(x => x.category)
                .Include(x => x.venue)
                .Include(x => x.organizer)
                .Include(x => x.registrations)
                .Where(x => x.organizer_id == currentUserId.Value)
                .OrderByDescending(x => x.start_at)
                .ToListAsync();

            var vm = new StudentMyEventsViewModel
            {
                RegisteredEvents = registeredEvents.Select(e => ToViewModel(e, currentUserId)).ToList(),
                OrganizedEvents = organizedEvents.Select(e => ToViewModel(e, currentUserId)).ToList()
            };

            return View(vm);
        }

        // =========================================================
        // GET: /Events/Search?q=term
        // =========================================================
        public async Task<IActionResult> Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return View(new List<Event>());

            var currentUserId = GetCurrentUserId();

            var items = await _db.events
                .Where(x => x.title.Contains(q) && (x.is_public == true && x.approval_status == "APPROVED"))
                .Include(x => x.category)
                .Include(x => x.venue)
                .Include(x => x.organizer)
                .Include(x => x.registrations)
                .OrderByDescending(x => x.start_at)
                .ToListAsync();

            var vm = items.Select(e => ToViewModel(e, currentUserId));
            return View(vm);
        }

        // =========================================================
        // GET: /Events/Categories
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            try
            {
                var categories = await _db.event_categories
                    .Where(c => c.is_active == true)
                    .OrderBy(c => c.name)
                    .Select(c => new EventCategorySummaryViewModel
                    {
                        Id = c.id,
                        Name = c.name,
                        Slug = c.slug,
                        Description = c.description,
                        Icon = c.icon,
                        IsActive = c.is_active ?? true,
                        EventCount = c._events.Count(e => e.is_public == true && e.approval_status == "APPROVED" && (e.status == "PUBLISHED" || e.status == "UPCOMING"))
                    })
                    .ToListAsync();

                return View(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching event categories summary from database.");
                return View(new List<EventCategorySummaryViewModel>());
            }
        }

        // =========================================================
        // POST: /Events/AddComment
        // =========================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(ulong eventId, string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                TempData["ErrorMessage"] = "Comment cannot be empty.";
                return RedirectToAction(nameof(Details), new { id = eventId });
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return Unauthorized();

            var ev = await _db.events.FindAsync(eventId);
            if (ev == null) return NotFound();

            var newComment = new event_comment
            {
                event_id = eventId,
                user_id = currentUserId.Value,
                comment = comment.Trim(),
                is_edited = false,
                is_deleted = false,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            _db.event_comments.Add(newComment);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your comment has been posted!";
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        // =========================================================
        // POST: /Events/DeleteComment
        // =========================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(ulong commentId, ulong eventId)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return Unauthorized();

            var c = await _db.event_comments.FindAsync(commentId);
            if (c == null) return NotFound();

            var ev = await _db.events.FindAsync(eventId);
            var isOrganizer = ev != null && ev.organizer_id == currentUserId.Value;
            var isAuthor = c.user_id == currentUserId.Value;
            var isAdmin = IsAdminOrSuperAdmin();

            if (!isAuthor && !isOrganizer && !isAdmin)
            {
                return Forbid();
            }

            c.is_deleted = true;
            c.deleted_at = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Comment deleted.";
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        // =========================================================
        // POST: /Events/AddFeedback
        // =========================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFeedback(ulong eventId, byte rating, string? comment, bool isAnonymous = false)
        {
            if (rating < 1 || rating > 5)
            {
                TempData["ErrorMessage"] = "Rating must be between 1 and 5 stars.";
                return RedirectToAction(nameof(Details), new { id = eventId });
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return Unauthorized();

            var existingFeedback = await _db.event_feedbacks
                .FirstOrDefaultAsync(f => f.event_id == eventId && f.user_id == currentUserId.Value);

            if (existingFeedback != null)
            {
                existingFeedback.rating = rating;
                existingFeedback.comment = comment?.Trim();
                existingFeedback.is_anonymous = isAnonymous;
                existingFeedback.updated_at = DateTime.UtcNow;
                TempData["SuccessMessage"] = "Your event review and star rating have been updated!";
            }
            else
            {
                var newFeedback = new event_feedback
                {
                    event_id = eventId,
                    user_id = currentUserId.Value,
                    rating = rating,
                    comment = comment?.Trim(),
                    is_anonymous = isAnonymous,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };
                _db.event_feedbacks.Add(newFeedback);
                TempData["SuccessMessage"] = "Thank you for submitting your event rating and feedback!";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        // =========================================================
        // GET: /Events/ExportIcs/5
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> ExportIcs(ulong id)
        {
            var e = await _db.events
                .Include(ev => ev.venue)
                .Include(ev => ev.organizer)
                .FirstOrDefaultAsync(ev => ev.id == id);

            if (e == null) return NotFound();

            var startUtc = e.start_at.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
            var endUtc = e.end_at.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
            var title = (e.title ?? "Campus Event").Replace("\r", "").Replace("\n", " ");
            var desc = (e.description ?? "").Replace("\r", "").Replace("\n", "\\n");
            var venue = (e.venue?.name ?? "Hawassa University").Replace("\r", "").Replace("\n", " ");

            var ics = $"BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Hawassa University//HUCEMS Event//EN\r\nBEGIN:VEVENT\r\nUID:event-{e.id}@hawassa.edu.et\r\nDTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}\r\nDTSTART:{startUtc}\r\nDTEND:{endUtc}\r\nSUMMARY:{title}\r\nDESCRIPTION:{desc}\r\nLOCATION:{venue}\r\nSTATUS:CONFIRMED\r\nEND:VEVENT\r\nEND:VCALENDAR";

            var bytes = System.Text.Encoding.UTF8.GetBytes(ics);
            return File(bytes, "text/calendar", $"{e.slug ?? "campus_event"}.ics");
        }

        // =====================================================================
        // VENUES DIRECTORY & FACILITIES
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Venues()
        {
            var venues = await _db.venues
                .OrderBy(v => v.name)
                .ToListAsync();

            return View(venues);
        }
    }
}