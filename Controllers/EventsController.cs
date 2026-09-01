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
            return User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
                   User.IsInRole("ADMIN") || User.IsInRole("SUPERADMIN");
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
        // Allowed: All Authenticated Campus Members (Students, Faculty, Staff, Org, Admin)
        // =========================================================
        [Authorize]
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
        [Authorize]
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
                var firstUser = await _db.users.FirstOrDefaultAsync();
                organizerId = firstUser?.id ?? 1;
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
            await _db.SaveChangesAsync();

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
        // GET: /Events/Search?q=term or ?query=term
        // =========================================================
        public async Task<IActionResult> Search(string? q, string? query)
        {
            var searchTerm = !string.IsNullOrWhiteSpace(q) ? q : query;
            ViewBag.Query = searchTerm;

            if (string.IsNullOrWhiteSpace(searchTerm)) return View(new List<Event>());

            var currentUserId = GetCurrentUserId();

            var items = await _db.events
                .Where(x => (x.title.Contains(searchTerm) || (x.description != null && x.description.Contains(searchTerm)) || (x.short_description != null && x.short_description.Contains(searchTerm))) && (x.is_public == true && x.approval_status == "APPROVED"))
                .Include(x => x.category)
                .Include(x => x.venue)
                .Include(x => x.organizer)
                .Include(x => x.registrations)
                .OrderByDescending(x => x.start_at)
                .ToListAsync();

            var vm = items.Select(e => ToViewModel(e, currentUserId)).ToList();
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
        // 1. INTERACTIVE MULTI-CAMPUS VISUAL MAP & VENUES EXPLORER
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Venues(string? campus = null, string? viewMode = "map")
        {
            var activeCampus = string.IsNullOrWhiteSpace(campus) ? "MAIN" : campus.ToUpperInvariant();

            var campuses = new List<CampusInfo>
            {
                new()
                {
                    Id = "MAIN",
                    Name = "Main Campus",
                    ShortName = "Main Campus",
                    Description = "Central Administration, College of Social Science, Natural Sciences & Central Library.",
                    LocationTag = "Hawassa Main Campus • Menaharia Sub-City",
                    Lat = 7.0504,
                    Lng = 38.4955
                },
                new()
                {
                    Id = "IOT",
                    Name = "Institute of Technology (IoT)",
                    ShortName = "Tech Campus",
                    Description = "Engineering, Computing, Innovation Hubs, and Advanced Technical Laboratories.",
                    LocationTag = "IoT Campus • Yirgalem Road",
                    Lat = 7.0345,
                    Lng = 38.4821
                },
                new()
                {
                    Id = "CMHS",
                    Name = "College of Medicine & Health Sciences",
                    ShortName = "Medical Campus",
                    Description = "Referral Hospital, Health Sciences Auditoriums & Clinical Research Centers.",
                    LocationTag = "CMHS Campus • Hospital Road",
                    Lat = 7.0621,
                    Lng = 38.4719
                },
                new()
                {
                    Id = "WONDO",
                    Name = "College of Agriculture & Forestry",
                    ShortName = "Wondo Genet",
                    Description = "Natural Resources, Forestry Research Amphitheater & Agri Innovation Complex.",
                    LocationTag = "Wondo Genet Campus • 25km South",
                    Lat = 7.0982,
                    Lng = 38.6190
                }
            };

            var allVenues = await _db.venues
                .AsNoTracking()
                .OrderBy(v => v.name)
                .ToListAsync();

            // Query upcoming approved events per venue
            var now = DateTime.UtcNow;
            var upcomingEvents = await _db.events
                .Include(e => e.category)
                .Include(e => e.organizer)
                .AsNoTracking()
                .Where(e => e.start_at >= now.AddDays(-1) && e.venue_id.HasValue && e.approval_status == "APPROVED")
                .OrderBy(e => e.start_at)
                .Take(50)
                .ToListAsync();

            var venueMapItems = new List<VenueMapItem>();
            int index = 0;

            foreach (var v in allVenues)
            {
                // Infer campus based on building name or name
                string venueCampus = "MAIN";
                string venueCampusName = "Main Campus";
                var combined = $"{v.name} {v.building_name} {v.description}".ToLowerInvariant();
                if (combined.Contains("iot") || combined.Contains("tech") || combined.Contains("computer") || combined.Contains("engineering"))
                {
                    venueCampus = "IOT";
                    venueCampusName = "Institute of Technology (IoT)";
                }
                else if (combined.Contains("med") || combined.Contains("health") || combined.Contains("hospital") || combined.Contains("clinical"))
                {
                    venueCampus = "CMHS";
                    venueCampusName = "College of Medicine & Health Sciences";
                }
                else if (combined.Contains("wondo") || combined.Contains("agri") || combined.Contains("forest"))
                {
                    venueCampus = "WONDO";
                    venueCampusName = "College of Agriculture & Forestry";
                }

                // Real Hawassa University GPS coordinates
                double lat = v.latitude.HasValue ? (double)v.latitude.Value : 7.0504;
                double lng = v.longitude.HasValue ? (double)v.longitude.Value : 38.4955;

                if (!v.latitude.HasValue || !v.longitude.HasValue)
                {
                    if (venueCampus == "MAIN")
                    {
                        lat = 7.0504 + ((index % 5) * 0.0008) - 0.0015;
                        lng = 38.4955 + (((index * 3) % 7) * 0.0007) - 0.0020;
                    }
                    else if (venueCampus == "IOT")
                    {
                        lat = 7.0345 + ((index % 4) * 0.0007) - 0.0010;
                        lng = 38.4821 + (((index * 2) % 5) * 0.0006) - 0.0012;
                    }
                    else if (venueCampus == "CMHS")
                    {
                        lat = 7.0621 + ((index % 3) * 0.0006) - 0.0008;
                        lng = 38.4719 + (((index * 2) % 4) * 0.0007) - 0.0010;
                    }
                    else if (venueCampus == "WONDO")
                    {
                        lat = 7.0982 + ((index % 3) * 0.0009) - 0.0010;
                        lng = 38.6190 + (((index * 2) % 4) * 0.0008) - 0.0012;
                    }
                }

                // Visual percentage positions for fallback
                int posX = 20 + ((index * 23) % 65);
                int posY = 25 + ((index * 29) % 55);
                index++;

                var equipment = new List<string>();
                if (!string.IsNullOrWhiteSpace(v.amenities))
                {
                    equipment.AddRange(v.amenities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
                if (equipment.Count == 0)
                {
                    equipment.AddRange(new[] { "HD Projector", "PA Sound System", "Campus Wi-Fi", "Air Conditioning" });
                }

                var venueUpcoming = upcomingEvents
                    .Where(e => e.venue_id == v.id)
                    .Select(e => new UpcomingVenueEvent
                    {
                        Id = e.id,
                        Title = e.title,
                        StartAt = e.start_at,
                        EndAt = e.end_at,
                        CategoryName = e.category?.name ?? "General",
                        OrganizerName = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "University"
                    })
                    .ToList();

                venueMapItems.Add(new VenueMapItem
                {
                    Id = v.id,
                    Name = v.name,
                    Campus = venueCampus,
                    CampusName = venueCampusName,
                    BuildingName = v.building_name,
                    RoomNumber = v.room_number,
                    Capacity = (int)v.capacity,
                    VenueType = v.venue_type ?? "Auditorium",
                    Status = string.IsNullOrWhiteSpace(v.status) ? "AVAILABLE" : v.status,
                    Description = v.description,
                    Amenities = v.amenities,
                    Lat = lat,
                    Lng = lng,
                    EquipmentList = equipment,
                    UpcomingEvents = venueUpcoming
                });
            }

            foreach (var c in campuses)
            {
                c.VenueCount = venueMapItems.Count(v => v.Campus == c.Id);
            }

            var vm = new CampusMapViewModel
            {
                ActiveCampus = activeCampus,
                Campuses = campuses,
                Venues = venueMapItems,
                TotalVenuesCount = allVenues.Count,
                AvailableVenuesCount = allVenues.Count(v => v.status == "AVAILABLE" || string.IsNullOrEmpty(v.status)),
                TotalCapacityCount = Convert.ToInt32(allVenues.Sum(v => (long)v.capacity))
            };

            ViewBag.ViewMode = string.Equals(viewMode, "grid", StringComparison.OrdinalIgnoreCase) ? "grid" : "map";
            return View(vm);
        }

        // =====================================================================
        // 2. LIVE CAMERA DOOR CHECK-IN TERMINAL
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CheckIn(ulong id)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return RedirectToAction("Login", "Account");

            var evt = await _db.events
                .Include(e => e.venue)
                .Include(e => e.registrations)
                    .ThenInclude(r => r.user)
                        .ThenInclude(u => u.department)
                .FirstOrDefaultAsync(e => e.id == id);

            if (evt == null) return NotFound();

            // Authorization: Event organizer or Admin/SuperAdmin
            if (evt.organizer_id != currentUserId.Value && !IsAdminOrSuperAdmin())
            {
                TempData["ErrorMessage"] = "Access Denied: Only event organizers and campus administrators can operate the check-in terminal.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var attendeeList = evt.registrations
                .OrderByDescending(r => r.registered_at)
                .Select(r => new AttendeeCheckInItem
                {
                    RegistrationId = r.id,
                    UserId = r.user_id,
                    FullName = r.user != null ? $"{r.user.first_name} {r.user.last_name}".Trim() : "Attendee",
                    StudentId = r.user?.student_id ?? r.user?.employee_id ?? "UGR/---",
                    Email = r.user?.email ?? "",
                    Department = r.user?.department?.name ?? "Campus Member",
                    RegistrationCode = r.registration_code ?? $"HUCEMS-REG-{r.id:D5}",
                    Status = r.status ?? "REGISTERED",
                    RegisteredAt = r.registered_at,
                    AttendedAt = r.status == "ATTENDED" ? (r.checked_in_at ?? r.registered_at) : null
                })
                .ToList();

            var vm = new CheckInViewModel
            {
                EventId = evt.id,
                EventTitle = evt.title,
                EventDate = evt.start_at,
                VenueName = evt.venue?.name ?? "Main Campus",
                Capacity = evt.capacity.HasValue ? (int)evt.capacity.Value : 0,
                TotalRegisteredCount = attendeeList.Count,
                AttendedCount = attendeeList.Count(a => a.Status == "ATTENDED"),
                Attendees = attendeeList
            };

            return View(vm);
        }

        // =====================================================================
        // 3. FAST AJAX TICKET VERIFICATION (QR CODE / MANUAL CODE)
        // =====================================================================
        [Authorize]
        [HttpPost]
        [Route("Events/VerifyTicket")]
        public async Task<IActionResult> VerifyTicket([FromBody] VerifyTicketRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TicketCode) || request.EventId == 0)
            {
                return Json(new VerifyTicketResponse
                {
                    Success = false,
                    Message = "Invalid ticket scan request. Please provide ticket code.",
                    Status = "INVALID"
                });
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Json(new VerifyTicketResponse { Success = false, Message = "Unauthorized session.", Status = "INVALID" });
            }

            var evt = await _db.events.FirstOrDefaultAsync(e => e.id == request.EventId);
            if (evt == null)
            {
                return Json(new VerifyTicketResponse { Success = false, Message = "Event not found.", Status = "INVALID" });
            }

            if (evt.organizer_id != currentUserId.Value && !IsAdminOrSuperAdmin())
            {
                return Json(new VerifyTicketResponse { Success = false, Message = "Permission denied.", Status = "INVALID" });
            }

            var cleanCode = request.TicketCode.Trim().ToUpperInvariant();

            // Locate registration record by registration_code, qr_token, or registration ID
            var registration = await _db.registrations
                .Include(r => r.user)
                    .ThenInclude(u => u.department)
                .FirstOrDefaultAsync(r => r.event_id == request.EventId &&
                    (r.registration_code == cleanCode ||
                     r.qr_token == cleanCode ||
                     cleanCode.Contains(r.id.ToString()) ||
                     (cleanCode.StartsWith("HUCEMS-REG-") && cleanCode.EndsWith(r.id.ToString()))));

            if (registration == null)
            {
                return Json(new VerifyTicketResponse
                {
                    Success = false,
                    Message = "❌ Ticket Not Found: No registration record matches this ticket code.",
                    Status = "INVALID"
                });
            }

            var totalReg = await _db.registrations.CountAsync(r => r.event_id == request.EventId);

            // If already attended
            if (registration.status == "ATTENDED")
            {
                var currentAttended = await _db.registrations.CountAsync(r => r.event_id == request.EventId && r.status == "ATTENDED");
                return Json(new VerifyTicketResponse
                {
                    Success = true,
                    Message = $"⚠️ Already Checked In: {registration.user.first_name} {registration.user.last_name} was checked in previously.",
                    Status = "ALREADY_ATTENDED",
                    FullName = $"{registration.user.first_name} {registration.user.last_name}".Trim(),
                    StudentId = registration.user.student_id ?? registration.user.employee_id ?? "Verified",
                    Department = registration.user.department?.name ?? "Campus Member",
                    TicketCode = registration.registration_code ?? $"HUCEMS-REG-{registration.id:D5}",
                    AttendedAt = registration.checked_in_at ?? registration.registered_at,
                    TotalAttended = currentAttended,
                    TotalRegistered = totalReg
                });
            }

            // Mark as ATTENDED
            registration.status = "ATTENDED";
            registration.checked_in_at = DateTime.UtcNow;
            registration.check_in_method = "QR";
            await _db.SaveChangesAsync();

            var totalAttendedCount = await _db.registrations.CountAsync(r => r.event_id == request.EventId && r.status == "ATTENDED");

            return Json(new VerifyTicketResponse
            {
                Success = true,
                Message = $"✅ Verified & Checked In: Welcome, {registration.user.first_name} {registration.user.last_name}!",
                Status = "VERIFIED",
                FullName = $"{registration.user.first_name} {registration.user.last_name}".Trim(),
                StudentId = registration.user.student_id ?? registration.user.employee_id ?? "Verified",
                Department = registration.user.department?.name ?? "Campus Member",
                TicketCode = registration.registration_code ?? $"HUCEMS-REG-{registration.id:D5}",
                AttendedAt = DateTime.UtcNow,
                TotalAttended = totalAttendedCount,
                TotalRegistered = totalReg
            });
        }

        // =====================================================================
        // 4. AUTOMATED VERIFIABLE CERTIFICATE OF PARTICIPATION
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Certificate(ulong id)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return RedirectToAction("Login", "Account");

            // Look up registration either by registration ID or event ID for current user
            var registration = await _db.registrations
                .Include(r => r.user)
                    .ThenInclude(u => u.department)
                        .ThenInclude(d => d != null ? d.faculty : null)
                .Include(r => r._event)
                    .ThenInclude(e => e.venue)
                .Include(r => r._event)
                    .ThenInclude(e => e.category)
                .Include(r => r._event)
                    .ThenInclude(e => e.organizer)
                .FirstOrDefaultAsync(r => (r.id == id || (r.event_id == id && r.user_id == currentUserId.Value)) &&
                                          (r.user_id == currentUserId.Value || IsAdminOrSuperAdmin()));

            if (registration == null || registration._event == null)
            {
                TempData["ErrorMessage"] = "Certificate not found or you are not registered for this event.";
                return RedirectToAction(nameof(MyEvents));
            }

            // Generate deterministic cryptographic verification hash
            var rawHashSource = $"HUCEMS-CERT-{registration.id}-{registration.user_id}-{registration.event_id}-HAWASSA-2026";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawHashSource));
            var securityHash = Convert.ToHexString(hashBytes).Substring(0, 16);

            var certNumber = $"HU-CERT-{registration._event.start_at:yyyy}-{registration.id:D6}";
            var verificationUrl = Url.Action(nameof(VerifyCertificate), "Events", new { code = certNumber }, Request.Scheme)
                                  ?? $"https://hucems.hawassa.edu.et/Events/VerifyCertificate?code={certNumber}";

            var vm = new CertificateViewModel
            {
                RegistrationId = registration.id,
                CertificateNumber = certNumber,
                StudentFullName = $"{registration.user.first_name} {registration.user.last_name}".Trim(),
                StudentIdNumber = registration.user.student_id ?? registration.user.employee_id ?? "UGR/2026",
                DepartmentName = registration.user.department?.name ?? "Hawassa University",
                FacultyName = registration.user.department?.faculty?.name ?? "Academic Programs Directorate",
                EventTitle = registration._event.title,
                EventCategory = registration._event.category?.name ?? "Academic Conference",
                EventDate = registration._event.start_at,
                VenueName = registration._event.venue?.name ?? "Hawassa University Main Campus",
                OrganizerName = registration._event.organizer != null ? $"{registration._event.organizer.first_name} {registration._event.organizer.last_name}".Trim() : "Student Affairs & Event Directorate",
                IssueDate = registration.checked_in_at ?? registration.registered_at,
                VerificationUrl = verificationUrl,
                SecurityHash = securityHash
            };

            return View(vm);
        }

        // =====================================================================
        // 5. PUBLIC CERTIFICATE VERIFICATION PORTAL
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> VerifyCertificate(string? code = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                ViewBag.Status = "SEARCH";
                return View();
            }

            var clean = code.Trim().ToUpperInvariant();
            var regIdStr = clean.Replace("HU-CERT-", "").Split('-').LastOrDefault();

            if (ulong.TryParse(regIdStr, out ulong regId))
            {
                var reg = await _db.registrations
                    .Include(r => r.user)
                        .ThenInclude(u => u.department)
                    .Include(r => r._event)
                        .ThenInclude(e => e.venue)
                    .FirstOrDefaultAsync(r => r.id == regId);

                if (reg != null && reg._event != null)
                {
                    ViewBag.Status = "VALID";
                    ViewBag.CertificateNumber = clean;
                    ViewBag.StudentName = $"{reg.user.first_name} {reg.user.last_name}".Trim();
                    ViewBag.StudentId = reg.user.student_id ?? "Verified";
                    ViewBag.Department = reg.user.department?.name ?? "Hawassa University";
                    ViewBag.EventTitle = reg._event.title;
                    ViewBag.EventDate = reg._event.start_at;
                    ViewBag.Venue = reg._event.venue?.name ?? "Main Campus";
                    return View();
                }
            }

            ViewBag.Status = "INVALID";
            ViewBag.SearchedCode = code;
            return View();
        }
    }
}