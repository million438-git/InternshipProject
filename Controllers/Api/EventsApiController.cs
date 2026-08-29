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
using HawassaUnifiedCampusEventManagementSystem.Services;

namespace HawassaUnifiedCampusEventManagementSystem.Controllers.Api
{
    [ApiController]
    [Route("api/events")]
    [Produces("application/json")]
    public class EventsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<EventsApiController> _logger;

        public EventsApiController(ApplicationDbContext db, ILogger<EventsApiController> logger)
        {
            _db = db;
            _logger = logger;
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

        // =====================================================================
        // 0. GET /api/events/calendar - Dedicated Calendar Feed (ISO 8601)
        // =====================================================================
        [HttpGet("calendar")]
        public async Task<IActionResult> GetCalendarEvents(
            [FromQuery] DateTime? start = null,
            [FromQuery] DateTime? end = null)
        {
            try
            {
                var query = _db.events
                    .Include(e => e.category)
                    .Include(e => e.venue)
                    .Include(e => e.organizer)
                    .Where(e => e.is_public == true && e.approval_status == "APPROVED" && (e.status == "PUBLISHED" || e.status == "APPROVED"))
                    .AsQueryable();

                if (start.HasValue)
                {
                    query = query.Where(e => e.start_at >= start.Value);
                }

                if (end.HasValue)
                {
                    query = query.Where(e => e.start_at <= end.Value);
                }

                var list = await query
                    .OrderBy(e => e.start_at)
                    .Select(e => new
                    {
                        id = e.id,
                        title = e.title,
                        slug = e.slug,
                        start = e.start_at.ToString("o"),
                        end = e.end_at > e.start_at ? e.end_at.ToString("o") : null,
                        location = e.venue != null ? e.venue.name : "Main Campus",
                        category = e.category != null ? e.category.name.ToLower() : "academic",
                        eventMode = e.event_mode,
                        organizer = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : "Campus Member",
                        description = e.short_description ?? e.description ?? e.title
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    count = list.Count,
                    data = list
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error retrieving calendar events");
                return StatusCode(500, new { success = false, message = "Could not retrieve calendar events." });
            }
        }

        // =====================================================================
        // 1. GET /api/events - List Events (With Search, Filter, Pagination)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> GetEvents(
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] ulong? venueId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _db.events
                    .Include(e => e.category)
                    .Include(e => e.venue)
                    .Include(e => e.organizer)
                    .Include(e => e.registrations)
                    .Where(e => e.is_public == true && e.approval_status == "APPROVED")
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(e => e.title.ToLower().Contains(s) || (e.description != null && e.description.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(category))
                {
                    query = query.Where(e => e.category != null && e.category.name.ToLower() == category.Trim().ToLower());
                }

                if (venueId.HasValue)
                {
                    query = query.Where(e => e.venue_id == venueId.Value);
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(e => e.start_at >= fromDate.Value);
                }

                var totalCount = await query.CountAsync();

                var events = await query
                    .OrderByDescending(e => e.start_at)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new
                    {
                        id = e.id,
                        title = e.title,
                        slug = e.slug,
                        shortDescription = e.short_description,
                        category = e.category != null ? e.category.name : null,
                        categoryIcon = e.category != null ? e.category.icon : null,
                        venue = e.venue != null ? e.venue.name : "Main Campus",
                        venueLocation = e.venue != null ? $"{e.venue.building_name} {e.venue.room_number}".Trim() : null,
                        startAt = e.start_at,
                        endAt = e.end_at,
                        capacity = e.capacity,
                        registeredCount = e.registrations.Count(r => r.status == "REGISTERED"),
                        isFull = e.capacity.HasValue && e.registrations.Count(r => r.status == "REGISTERED") >= e.capacity.Value,
                        imageUrl = e.image_url,
                        organizer = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : null,
                        status = e.status,
                        isFeatured = e.is_featured
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    total = totalCount,
                    page,
                    pageSize,
                    data = events
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error retrieving events");
                return StatusCode(500, new { success = false, message = "An error occurred while fetching events." });
            }
        }

        // =====================================================================
        // 2. GET /api/events/{id} - Get Event Details
        // =====================================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEventById(ulong id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                var e = await _db.events
                    .Include(x => x.category)
                    .Include(x => x.venue)
                    .Include(x => x.organizer)
                    .Include(x => x.registrations)
                    .FirstOrDefaultAsync(x => x.id == id);

                if (e == null)
                {
                    return NotFound(new { success = false, message = $"Event with ID {id} not found." });
                }

                var isRegistered = currentUserId.HasValue &&
                                   e.registrations.Any(r => r.user_id == currentUserId.Value && r.status == "REGISTERED");

                var registeredCount = e.registrations.Count(r => r.status == "REGISTERED");

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = e.id,
                        title = e.title,
                        slug = e.slug,
                        description = e.description,
                        shortDescription = e.short_description,
                        category = e.category?.name,
                        categoryId = e.category_id,
                        venue = e.venue?.name,
                        venueId = e.venue_id,
                        venueLocation = e.venue != null ? $"{e.venue.building_name} {e.venue.room_number}".Trim() : null,
                        startAt = e.start_at,
                        endAt = e.end_at,
                        capacity = e.capacity,
                        registeredCount,
                        availableSeats = e.capacity.HasValue ? Math.Max(0, (int)e.capacity.Value - registeredCount) : (int?)null,
                        imageUrl = e.image_url,
                        organizerId = e.organizer_id,
                        organizerName = e.organizer != null ? $"{e.organizer.first_name} {e.organizer.last_name}".Trim() : null,
                        organizerEmail = e.organizer?.email,
                        status = e.status,
                        approvalStatus = e.approval_status,
                        isUserRegistered = isRegistered,
                        createdAt = e.created_at
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error retrieving event {Id}", id);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching event details." });
            }
        }

        // =====================================================================
        // 3. POST /api/events/{id}/register - Student / User Event Registration
        // =====================================================================
        [Authorize]
        [HttpPost("{id}/register")]
        public async Task<IActionResult> RegisterForEvent(ulong id)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Authentication is required to register for events." });
            }

            try
            {
                var evt = await _db.events
                    .Include(e => e.registrations)
                    .FirstOrDefaultAsync(e => e.id == id);

                if (evt == null)
                {
                    return NotFound(new { success = false, message = "Event not found." });
                }

                if (evt.approval_status != "APPROVED")
                {
                    return BadRequest(new { success = false, message = "Registration is not open for unapproved events." });
                }

                // Check existing registration
                var existing = await _db.registrations
                    .FirstOrDefaultAsync(r => r.event_id == id && r.user_id == currentUserId.Value);

                if (existing != null)
                {
                    if (existing.status == "REGISTERED")
                    {
                        return Ok(new
                        {
                            success = true,
                            message = "You are already registered for this event.",
                            ticketCode = existing.registration_code,
                            qrToken = existing.qr_token,
                            status = existing.status
                        });
                    }

                    // Reactivate cancelled registration
                    existing.status = "REGISTERED";
                    existing.registered_at = DateTime.UtcNow;
                    existing.cancelled_at = null;
                    await _db.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        message = "Your registration has been reactivated successfully.",
                        ticketCode = existing.registration_code,
                        qrToken = existing.qr_token,
                        status = existing.status
                    });
                }

                var activeCount = evt.registrations.Count(r => r.status == "REGISTERED");
                string regStatus = (evt.capacity.HasValue && activeCount >= evt.capacity.Value) ? "WAITLISTED" : "REGISTERED";

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
                    return Ok(new
                    {
                        success = true,
                        message = "You are already registered for this event."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = regStatus == "REGISTERED" ? "Registration confirmed!" : "Event is full. Added to waitlist.",
                    ticketCode = regCode,
                    qrToken = qrToken,
                    status = regStatus,
                    registeredAt = newReg.registered_at
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error registering user {UserId} for event {EventId}", currentUserId, id);
                return StatusCode(500, new { success = false, message = "Registration processing failed." });
            }
        }

        // =====================================================================
        // 4. POST /api/events/{id}/cancel-registration - Cancel Registration
        // =====================================================================
        [Authorize]
        [HttpPost("{id}/cancel-registration")]
        public async Task<IActionResult> CancelRegistration(ulong id)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return Unauthorized();

            var reg = await _db.registrations
                .FirstOrDefaultAsync(r => r.event_id == id && r.user_id == currentUserId.Value && r.status == "REGISTERED");

            if (reg == null)
            {
                return NotFound(new { success = false, message = "Active registration not found for this event." });
            }

            reg.status = "CANCELLED";
            reg.cancelled_at = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Registration cancelled successfully." });
        }

        // =====================================================================
        // 5. POST /api/events/verify-ticket - Ticket Check-In / QR Validation
        // =====================================================================
        [Authorize(Roles = "Faculty,Staff,Organization,Club,Admin,SuperAdmin,FACULTY,STAFF,ORGANIZATION,ADMIN,SUPERADMIN")]
        [HttpPost("verify-ticket")]
        public async Task<IActionResult> VerifyTicket([FromBody] TicketVerificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TokenOrCode))
            {
                return BadRequest(new { success = false, message = "Please provide a ticket code or QR token." });
            }

            var token = request.TokenOrCode.Trim();

            var reg = await _db.registrations
                .Include(r => r.user)
                .Include(r => r._event)
                .FirstOrDefaultAsync(r => r.qr_token == token || r.registration_code == token);

            if (reg == null || reg._event == null)
            {
                return NotFound(new { success = false, isValid = false, message = "Invalid ticket code or QR token." });
            }

            var currentUserId = GetCurrentUserId();
            var isOrganizer = currentUserId.HasValue && reg._event.organizer_id == currentUserId.Value;
            if (!IsAdminOrSuperAdmin() && !isOrganizer)
            {
                return StatusCode(403, new { success = false, isValid = false, message = "You are not authorized to check in tickets for this event." });
            }

            if (reg.status == "CANCELLED")
            {
                return BadRequest(new { success = false, isValid = false, message = "This ticket was cancelled by the attendee." });
            }

            if (reg.status == "ATTENDED")
            {
                return Ok(new
                {
                    success = true,
                    isValid = true,
                    alreadyCheckedIn = true,
                    message = $"Attendee was already checked in at {reg.checked_in_at:hh:mm tt}.",
                    attendee = $"{reg.user.first_name} {reg.user.last_name}".Trim(),
                    studentId = reg.user.student_id,
                    eventTitle = reg._event.title
                });
            }

            // Mark checked-in
            reg.status = "ATTENDED";
            reg.checked_in_at = DateTime.UtcNow;
            reg.check_in_method = "QR";
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                isValid = true,
                alreadyCheckedIn = false,
                message = "Ticket verified successfully. Check-in recorded!",
                attendee = $"{reg.user.first_name} {reg.user.last_name}".Trim(),
                studentId = reg.user.student_id,
                email = reg.user.email,
                eventTitle = reg._event.title,
                checkInTime = reg.checked_in_at
            });
        }
    }

    public class TicketVerificationRequest
    {
        public string TokenOrCode { get; set; } = string.Empty;
    }
}
