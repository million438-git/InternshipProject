using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Models;

namespace HawassaUnifiedCampusEventManagementSystem.Controllers
{
    public class ClubsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ClubsController> _logger;

        public ClubsController(ApplicationDbContext db, ILogger<ClubsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // =====================================================================
        // 1. PUBLIC DIRECTORY & INTEREST RECOMMENDATION (GET: /Clubs)
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Index(string? search, ulong? categoryId, ulong? deptId, string? filter, string? status)
        {
            var isAdmin = IsAdminOrSuperAdmin();
            var (currentUserId, isAuthenticated) = GetCurrentUserId();

            var vm = new ClubListViewModel
            {
                SearchQuery = search,
                SelectedCategoryId = categoryId,
                SelectedDepartmentId = deptId,
                FilterType = filter,
                IsUserAdmin = isAdmin,
                StatusFilter = string.IsNullOrWhiteSpace(status) ? (isAdmin ? "ALL" : "ACTIVE") : status.Trim().ToUpper()
            };

            // Query live club counts for platform administrators
            if (isAdmin)
            {
                vm.TotalActiveCount = await _db.clubs.CountAsync(c => c.status == "ACTIVE");
                vm.TotalPendingCount = await _db.clubs.CountAsync(c => c.status == "PENDING");
                vm.TotalSuspendedCount = await _db.clubs.CountAsync(c => c.status == "SUSPENDED" || c.status == "INACTIVE");
            }
            else
            {
                vm.TotalActiveCount = await _db.clubs.CountAsync(c => c.status == "ACTIVE");
            }

            // Load student's interests if logged in
            List<ulong> userInterestCategoryIds = new();
            if (isAuthenticated && currentUserId.HasValue)
            {
                userInterestCategoryIds = await _db.user_category_interests
                    .Where(ui => ui.user_id == currentUserId.Value)
                    .Select(ui => ui.category_id)
                    .ToListAsync();

                vm.HasSelectedInterests = userInterestCategoryIds.Any();

                if (vm.HasSelectedInterests)
                {
                    vm.UserInterestNames = await _db.event_categories
                        .Where(c => userInterestCategoryIds.Contains(c.id))
                        .Select(c => c.name)
                        .ToListAsync();
                }
            }

            // Available Categories & Departments for filters
            var categories = await _db.event_categories.Where(c => c.is_active != false).OrderBy(c => c.name).ToListAsync();
            vm.AvailableCategories = categories.Select(c => new SelectListItem
            {
                Value = c.id.ToString(),
                Text = c.name,
                Selected = categoryId.HasValue && categoryId.Value == c.id
            }).ToList();

            var depts = await _db.departments.OrderBy(d => d.name).ToListAsync();
            vm.AvailableDepartments = depts.Select(d => new SelectListItem
            {
                Value = d.id.ToString(),
                Text = d.name,
                Selected = deptId.HasValue && deptId.Value == d.id
            }).ToList();

            // Base query with all relations
            var query = _db.clubs
                .Include(c => c.department)
                .Include(c => c.faculty)
                .Include(c => c.organization)
                .Include(c => c.president)
                .Include(c => c.club_interests)
                    .ThenInclude(ci => ci.category)
                .Include(c => c.club_followers)
                .Include(c => c.club_members)
                .AsQueryable();

            // Status filtering (Admin can view ALL/ACTIVE/PENDING/SUSPENDED; public users only see ACTIVE)
            if (isAdmin)
            {
                if (vm.StatusFilter == "ACTIVE")
                    query = query.Where(c => c.status == "ACTIVE");
                else if (vm.StatusFilter == "PENDING")
                    query = query.Where(c => c.status == "PENDING");
                else if (vm.StatusFilter == "SUSPENDED")
                    query = query.Where(c => c.status == "SUSPENDED" || c.status == "INACTIVE");
            }
            else
            {
                query = query.Where(c => c.status == "ACTIVE");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c => c.name.ToLower().Contains(s) ||
                                         (c.short_name != null && c.short_name.ToLower().Contains(s)) ||
                                         (c.description != null && c.description.ToLower().Contains(s)));
            }

            if (deptId.HasValue)
            {
                query = query.Where(c => c.department_id == deptId.Value);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(c => c.club_interests.Any(ci => ci.category_id == categoryId.Value));
            }

            var allClubsList = await query.ToListAsync();

            // Map to Card ViewModels
            var cardModels = allClubsList.Select(c =>
            {
                var isFollowing = currentUserId.HasValue && c.club_followers.Any(f => f.user_id == currentUserId.Value);
                var memberRecord = currentUserId.HasValue ? c.club_members.FirstOrDefault(m => m.user_id == currentUserId.Value) : null;
                var isPresidentOrAdmin = currentUserId.HasValue && (c.president_id == currentUserId.Value || IsAdminOrSuperAdmin());

                // Calculate Match Score with Student's Interests
                int matchScore = 0;
                string? reason = null;

                if (userInterestCategoryIds.Any())
                {
                    var matchingCats = c.club_interests
                        .Where(ci => userInterestCategoryIds.Contains(ci.category_id))
                        .Select(ci => ci.category?.name)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();

                    matchScore = matchingCats.Count;
                    if (matchScore > 0)
                    {
                        if (matchScore == 1)
                            reason = $"Because you are interested in {matchingCats.First()}";
                        else
                            reason = $"Matches {matchScore} of your interests: {string.Join(", ", matchingCats.Take(2))}";
                    }
                }

                return new ClubCardViewModel
                {
                    Id = c.id,
                    Name = c.name,
                    Slug = c.slug,
                    ShortName = c.short_name,
                    Description = c.description,
                    LogoUrl = c.logo_url,
                    CoverImageUrl = c.cover_image_url,
                    DepartmentName = c.department?.name,
                    FacultyName = c.faculty?.name,
                    OrganizationName = c.organization?.name,
                    PresidentName = c.president != null ? $"{c.president.first_name} {c.president.last_name}".Trim() : null,
                    Status = c.status,
                    Interests = c.club_interests.Select(ci => new ClubInterestBadge
                    {
                        Id = ci.category_id,
                        Name = ci.category?.name ?? "General",
                        Icon = ci.category?.icon ?? "bi-tag"
                    }).ToList(),
                    FollowerCount = c.club_followers.Count,
                    MemberCount = c.club_members.Count(m => m.status == "APPROVED"),
                    IsFollowing = isFollowing,
                    MembershipStatus = memberRecord != null ? memberRecord.status : "NONE",
                    MembershipRole = memberRecord?.membership_role,
                    MatchScore = matchScore,
                    RecommendationReason = reason,
                    IsPresidentOrAdmin = isPresidentOrAdmin
                };
            }).ToList();

            // Populate Recommended clubs (matchScore > 0, descending by score and popularity)
            if (vm.HasSelectedInterests)
            {
                vm.RecommendedClubs = cardModels
                    .Where(c => c.MatchScore > 0)
                    .OrderByDescending(c => c.MatchScore)
                    .ThenByDescending(c => c.FollowerCount)
                    .Take(6)
                    .ToList();
            }
            else
            {
                // If user hasn't chosen interests, recommend most popular active clubs
                vm.RecommendedClubs = cardModels
                    .OrderByDescending(c => c.FollowerCount + c.MemberCount)
                    .Take(4)
                    .ToList();
            }

            // Apply filter types if requested
            if (filter == "following" && currentUserId.HasValue)
            {
                cardModels = cardModels.Where(c => c.IsFollowing).ToList();
            }
            else if (filter == "my" && currentUserId.HasValue)
            {
                cardModels = cardModels.Where(c => c.MembershipStatus == "APPROVED" || c.MembershipStatus == "PENDING" || c.IsPresidentOrAdmin).ToList();
            }

            vm.AllClubs = cardModels.OrderByDescending(c => c.FollowerCount + c.MemberCount).ToList();

            return View(vm);
        }

        // =====================================================================
        // 2. CLUB PROFILE & DETAILS (GET: /Clubs/Details/{idOrSlug})
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            Club? club = null;
            if (ulong.TryParse(id, out ulong numericId))
            {
                club = await _db.clubs
                    .Include(c => c.department)
                    .Include(c => c.faculty)
                    .Include(c => c.organization)
                    .Include(c => c.president)
                    .Include(c => c.club_interests)
                        .ThenInclude(ci => ci.category)
                    .Include(c => c.club_followers)
                    .Include(c => c.club_members)
                        .ThenInclude(cm => cm.user)
                            .ThenInclude(u => u.department)
                    .FirstOrDefaultAsync(c => c.id == numericId);
            }
            else
            {
                club = await _db.clubs
                    .Include(c => c.department)
                    .Include(c => c.faculty)
                    .Include(c => c.organization)
                    .Include(c => c.president)
                    .Include(c => c.club_interests)
                        .ThenInclude(ci => ci.category)
                    .Include(c => c.club_followers)
                    .Include(c => c.club_members)
                        .ThenInclude(cm => cm.user)
                            .ThenInclude(u => u.department)
                    .FirstOrDefaultAsync(c => c.slug == id);
            }

            if (club == null) return NotFound();

            var (currentUserId, isAuthenticated) = GetCurrentUserId();

            var isFollowing = currentUserId.HasValue && club.club_followers.Any(f => f.user_id == currentUserId.Value);
            var memberRecord = currentUserId.HasValue ? club.club_members.FirstOrDefault(m => m.user_id == currentUserId.Value) : null;

            var isSuperOrAdmin = IsAdminOrSuperAdmin();
            var isPresident = currentUserId.HasValue && club.president_id == currentUserId.Value;
            var isOfficerOrLeader = memberRecord != null && memberRecord.status == "APPROVED" &&
                (memberRecord.membership_role == "PRESIDENT" || memberRecord.membership_role == "ADMIN" || 
                 memberRecord.membership_role == "OFFICER" || memberRecord.membership_role == "SECRETARY" || 
                 memberRecord.membership_role == "TREASURER");

            var canManage = isSuperOrAdmin || isPresident || isOfficerOrLeader;

            // Fetch upcoming events organized by this club or its organization
            var interestCategoryIds = club.club_interests.Select(ci => ci.category_id).ToList();

            var upcomingEvents = await _db.events
                .Include(e => e.venue)
                .Include(e => e.category)
                .Where(e => (club.organization_id != null && e.organization_id == club.organization_id) ||
                            (club.president_id != null && e.organizer_id == club.president_id) ||
                            (interestCategoryIds.Contains(e.category_id) && e.status == "PUBLISHED"))
                .Where(e => e.start_at >= DateTime.UtcNow.AddDays(-1))
                .OrderBy(e => e.start_at)
                .Take(6)
                .Select(e => new ClubEventItem
                {
                    Id = e.id,
                    Title = e.title,
                    ShortDescription = e.short_description,
                    ImageUrl = e.image_url,
                    StartAt = e.start_at,
                    VenueName = e.venue != null ? e.venue.name : "Campus Venue",
                    CategoryName = e.category != null ? e.category.name : "Academic"
                })
                .ToListAsync();

            // Fetch announcements
            var announcements = await _db.announcements
                .Where(a => a.status == "PUBLISHED" && ((club.president_id != null && a.author_id == club.president_id) || (club.department_id != null && a.department_id == club.department_id)))
                .OrderByDescending(a => a.created_at)
                .Take(4)
                .Select(a => new ClubAnnouncementItem
                {
                    Id = a.id,
                    Title = a.title,
                    Summary = a.summary ?? (a.content.Length > 100 ? a.content.Substring(0, 100) + "..." : a.content),
                    CreatedAt = a.created_at,
                    Priority = a.priority
                })
                .ToListAsync();

            var vm = new ClubDetailsViewModel
            {
                Id = club.id,
                Name = club.name,
                Slug = club.slug,
                ShortName = club.short_name,
                Description = club.description,
                LogoUrl = club.logo_url,
                CoverImageUrl = club.cover_image_url,
                FacultyName = club.faculty?.name,
                DepartmentName = club.department?.name,
                OrganizationName = club.organization?.name,
                PresidentName = club.president != null ? $"{club.president.first_name} {club.president.last_name}".Trim() : null,
                PresidentEmail = club.president?.email,
                PresidentId = club.president_id,
                Status = club.status,
                CreatedAt = club.created_at,
                Interests = club.club_interests.Select(ci => new ClubInterestBadge
                {
                    Id = ci.category_id,
                    Name = ci.category?.name ?? "General",
                    Icon = ci.category?.icon ?? "bi-tag"
                }).ToList(),
                FollowerCount = club.club_followers.Count,
                MemberCount = club.club_members.Count(m => m.status == "APPROVED"),
                PendingRequestsCount = club.club_members.Count(m => m.status == "PENDING"),
                IsFollowing = isFollowing,
                MembershipStatus = memberRecord != null ? memberRecord.status : "NONE",
                MembershipRole = memberRecord?.membership_role,
                CanManage = canManage,
                IsUserAdmin = isSuperOrAdmin,
                UpcomingEvents = upcomingEvents,
                Announcements = announcements,
                Officers = club.club_members
                    .Where(m => m.status == "APPROVED" && m.membership_role != "MEMBER")
                    .Select(m => new ClubMemberItem
                    {
                        MemberRecordId = m.id,
                        UserId = m.user_id,
                        FullName = $"{m.user.first_name} {m.user.last_name}".Trim(),
                        Email = m.user.email,
                        DepartmentName = m.user.department?.name,
                        Role = m.membership_role,
                        Status = m.status,
                        AppliedAt = m.applied_at
                    }).ToList(),
                Members = club.club_members
                    .Where(m => m.status == "APPROVED" && m.membership_role == "MEMBER")
                    .Select(m => new ClubMemberItem
                    {
                        MemberRecordId = m.id,
                        UserId = m.user_id,
                        FullName = $"{m.user.first_name} {m.user.last_name}".Trim(),
                        Email = m.user.email,
                        DepartmentName = m.user.department?.name,
                        Role = m.membership_role,
                        Status = m.status,
                        AppliedAt = m.applied_at
                    }).ToList()
            };

            return View(vm);
        }

        // =====================================================================
        // 3. STUDENT INTEREST PREFERENCE MANAGER (GET & POST: /Clubs/Interests)
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Interests(string? returnUrl)
        {
            var (userId, _) = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var allCategories = await _db.event_categories
                .Where(c => c.is_active != false)
                .OrderBy(c => c.name)
                .ToListAsync();

            var userInterests = await _db.user_category_interests
                .Where(ui => ui.user_id == userId.Value)
                .ToListAsync();

            var selectedDict = userInterests.ToDictionary(ui => ui.category_id, ui => ui.interest_level);

            var vm = new UserInterestsViewModel
            {
                ReturnUrl = returnUrl,
                SelectedCategoryIds = userInterests.Select(ui => ui.category_id).ToList(),
                Categories = allCategories.Select(c => new InterestSelectionItem
                {
                    Id = c.id,
                    Name = c.name,
                    Description = c.description,
                    Icon = c.icon,
                    IsSelected = selectedDict.ContainsKey(c.id),
                    InterestLevel = selectedDict.ContainsKey(c.id) ? selectedDict[c.id] : "MEDIUM"
                }).ToList()
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveInterests(List<ulong> categoryIds, string? returnUrl)
        {
            var (userId, _) = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            try
            {
                // Remove existing user interests
                var existing = await _db.user_category_interests
                    .Where(ui => ui.user_id == userId.Value)
                    .ToListAsync();

                _db.user_category_interests.RemoveRange(existing);

                // Add new user interests
                if (categoryIds != null && categoryIds.Any())
                {
                    foreach (var catId in categoryIds.Distinct())
                    {
                        _db.user_category_interests.Add(new user_category_interest
                        {
                            user_id = userId.Value,
                            category_id = catId,
                            interest_level = "HIGH",
                            created_at = DateTime.UtcNow
                        });
                    }
                }

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Your campus interests have been successfully saved! Club recommendations updated.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user interests.");
                TempData["ErrorMessage"] = "Failed to save interests: " + ex.Message;
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        // =====================================================================
        // 4. FOLLOW / UNFOLLOW (POST: /Clubs/Follow/{id}, /Clubs/Unfollow/{id})
        // =====================================================================
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Follow(ulong id)
        {
            var (userId, _) = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var club = await _db.clubs.FindAsync(id);
            if (club == null) return NotFound();

            if (club.status == "SUSPENDED" || club.status == "INACTIVE")
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = $"Club '{club.name}' is currently suspended." });
                }
                TempData["ErrorMessage"] = $"Club '{club.name}' is currently suspended.";
                return RedirectToAction(nameof(Details), new { id = club.slug });
            }

            var existingFollow = await _db.club_followers
                .FirstOrDefaultAsync(f => f.club_id == id && f.user_id == userId.Value);

            if (existingFollow == null)
            {
                _db.club_followers.Add(new ClubFollower
                {
                    club_id = id,
                    user_id = userId.Value,
                    followed_at = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            var totalFollowers = await _db.club_followers.CountAsync(f => f.club_id == id);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, isFollowing = true, followerCount = totalFollowers, message = $"You are now following {club.name}." });
            }

            TempData["SuccessMessage"] = $"You are now following {club.name}!";
            return RedirectToAction(nameof(Details), new { id = club.slug });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Unfollow(ulong id)
        {
            var (userId, _) = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var club = await _db.clubs.FindAsync(id);
            if (club == null) return NotFound();

            var existingFollow = await _db.club_followers
                .FirstOrDefaultAsync(f => f.club_id == id && f.user_id == userId.Value);

            if (existingFollow != null)
            {
                _db.club_followers.Remove(existingFollow);
                await _db.SaveChangesAsync();
            }

            var totalFollowers = await _db.club_followers.CountAsync(f => f.club_id == id);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, isFollowing = false, followerCount = totalFollowers, message = $"Unfollowed {club.name}." });
            }

            TempData["SuccessMessage"] = $"Unfollowed {club.name}.";
            return RedirectToAction(nameof(Details), new { id = club.slug });
        }

        // =====================================================================
        // 5. MEMBERSHIP REQUEST & APPROVAL LIFECYCLE
        // =====================================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(ulong id, string? requestNotes)
        {
            var (userId, _) = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var club = await _db.clubs.FindAsync(id);
            if (club == null) return NotFound();

            if (club.status == "SUSPENDED" || club.status == "INACTIVE")
            {
                TempData["ErrorMessage"] = $"Club '{club.name}' is currently suspended and cannot accept new membership applications.";
                return RedirectToAction(nameof(Details), new { id = club.slug });
            }

            var existingMember = await _db.club_members
                .FirstOrDefaultAsync(m => m.club_id == id && m.user_id == userId.Value);

            if (existingMember != null)
            {
                if (existingMember.status == "APPROVED")
                {
                    TempData["InfoMessage"] = "You are already an official member of this club!";
                    return RedirectToAction(nameof(Details), new { id = club.slug });
                }
                else if (existingMember.status == "PENDING")
                {
                    TempData["InfoMessage"] = "Your membership request is currently under review by club leaders.";
                    return RedirectToAction(nameof(Details), new { id = club.slug });
                }
                else if (existingMember.status == "REJECTED")
                {
                    // Re-apply
                    existingMember.status = "PENDING";
                    existingMember.request_notes = requestNotes;
                    existingMember.applied_at = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Membership request re-submitted to club leadership.";
                    return RedirectToAction(nameof(Details), new { id = club.slug });
                }
            }

            // Create new membership application
            _db.club_members.Add(new ClubMember
            {
                club_id = id,
                user_id = userId.Value,
                membership_role = "MEMBER",
                status = "PENDING",
                request_notes = requestNotes,
                applied_at = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // Also automatically follow club when requesting membership
            var isFollowing = await _db.club_followers.AnyAsync(f => f.club_id == id && f.user_id == userId.Value);
            if (!isFollowing)
            {
                _db.club_followers.Add(new ClubFollower
                {
                    club_id = id,
                    user_id = userId.Value,
                    followed_at = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            // Also notify club president of new membership request
            if (club.president_id.HasValue && club.president_id.Value != userId.Value)
            {
                var applicant = await _db.users.FindAsync(userId.Value);
                var applicantName = applicant != null ? $"{applicant.first_name} {applicant.last_name}".Trim() : "A campus student";
                _db.notifications.Add(new Notification
                {
                    user_id = club.president_id.Value,
                    title = "New Club Membership Application",
                    message = $"{applicantName} has applied to join '{club.name}'.",
                    notification_type = "CLUB",
                    related_entity_type = "CLUB",
                    related_entity_id = club.id,
                    action_url = $"/Clubs/ManageMembers/{club.id}",
                    is_read = false,
                    created_at = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Your membership request for '{club.name}' has been submitted for review.";
            return RedirectToAction(nameof(Details), new { id = club.slug });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMembership(ulong id)
        {
            var (userId, _) = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var club = await _db.clubs.FindAsync(id);
            if (club == null) return NotFound();

            var existingMember = await _db.club_members
                .FirstOrDefaultAsync(m => m.club_id == id && m.user_id == userId.Value);

            if (existingMember != null)
            {
                _db.club_members.Remove(existingMember);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Membership request cancelled / Left club.";
            }

            return RedirectToAction(nameof(Details), new { id = club.slug });
        }

        // =====================================================================
        // 6. CLUB LEADER MEMBERSHIP PORTAL (GET: /Clubs/ManageMembers/{id})
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ManageMembers(ulong id)
        {
            var club = await _db.clubs
                .Include(c => c.club_followers)
                    .ThenInclude(f => f.user)
                        .ThenInclude(u => u.department)
                .Include(c => c.club_members)
                    .ThenInclude(m => m.user)
                        .ThenInclude(u => u.department)
                .FirstOrDefaultAsync(c => c.id == id);

            if (club == null) return NotFound();

            var (currentUserId, _) = GetCurrentUserId();
            var isSuperOrAdmin = IsAdminOrSuperAdmin();
            var isPresident = currentUserId.HasValue && club.president_id == currentUserId.Value;
            var isOfficer = currentUserId.HasValue && club.club_members.Any(m => m.user_id == currentUserId.Value && m.status == "APPROVED" && (m.membership_role == "ADMIN" || m.membership_role == "OFFICER" || m.membership_role == "SECRETARY" || m.membership_role == "TREASURER" || m.membership_role == "PRESIDENT"));

            if (!isSuperOrAdmin && !isPresident && !isOfficer)
            {
                TempData["ErrorMessage"] = "Access Denied: Only club leaders or administrators can manage membership requests.";
                return RedirectToAction(nameof(Details), new { id = club.slug });
            }

            var vm = new ClubManageMembersViewModel
            {
                ClubId = club.id,
                ClubName = club.name,
                ClubSlug = club.slug,
                IsPresidentOrAdmin = isSuperOrAdmin || isPresident,
                TotalMembersCount = club.club_members.Count(m => m.status == "APPROVED"),
                PendingRequestsCount = club.club_members.Count(m => m.status == "PENDING"),
                FollowersCount = club.club_followers.Count,
                PendingRequests = club.club_members
                    .Where(m => m.status == "PENDING")
                    .OrderByDescending(m => m.applied_at)
                    .Select(m => new ClubMemberItem
                    {
                        MemberRecordId = m.id,
                        UserId = m.user_id,
                        FullName = $"{m.user.first_name} {m.user.last_name}".Trim(),
                        Email = m.user.email,
                        DepartmentName = m.user.department?.name,
                        Role = m.membership_role,
                        Status = m.status,
                        AppliedAt = m.applied_at,
                        RequestNotes = m.request_notes
                    }).ToList(),
                ActiveMembers = club.club_members
                    .Where(m => m.status == "APPROVED")
                    .OrderBy(m => m.membership_role == "PRESIDENT" ? 0 : m.membership_role == "ADMIN" ? 1 : m.membership_role == "OFFICER" ? 2 : m.membership_role == "SECRETARY" ? 3 : m.membership_role == "TREASURER" ? 4 : 5)
                    .Select(m => new ClubMemberItem
                    {
                        MemberRecordId = m.id,
                        UserId = m.user_id,
                        FullName = $"{m.user.first_name} {m.user.last_name}".Trim(),
                        Email = m.user.email,
                        DepartmentName = m.user.department?.name,
                        Role = m.membership_role,
                        Status = m.status,
                        AppliedAt = m.applied_at
                    }).ToList(),
                Followers = club.club_followers
                    .Select(f => new ClubMemberItem
                    {
                        UserId = f.user_id,
                        FullName = $"{f.user.first_name} {f.user.last_name}".Trim(),
                        Email = f.user.email,
                        DepartmentName = f.user.department?.name,
                        Role = "FOLLOWER",
                        Status = "FOLLOWING",
                        AppliedAt = f.followed_at
                    }).ToList()
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveMember(ulong memberRecordId, string? role)
        {
            var member = await _db.club_members
                .Include(m => m.club)
                .Include(m => m.user)
                .FirstOrDefaultAsync(m => m.id == memberRecordId);

            if (member == null) return NotFound();

            var (currentUserId, _) = GetCurrentUserId();
            var isSuperOrAdmin = IsAdminOrSuperAdmin();
            var isPresident = currentUserId.HasValue && member.club.president_id == currentUserId.Value;
            var isOfficer = currentUserId.HasValue && await _db.club_members.AnyAsync(m => m.club_id == member.club_id && m.user_id == currentUserId.Value && m.status == "APPROVED" && (m.membership_role == "ADMIN" || m.membership_role == "OFFICER" || m.membership_role == "SECRETARY" || m.membership_role == "TREASURER" || m.membership_role == "PRESIDENT"));

            if (!isSuperOrAdmin && !isPresident && !isOfficer)
            {
                TempData["ErrorMessage"] = "Access Denied: You do not have permission to approve members for this club.";
                return RedirectToAction(nameof(Details), new { id = member.club.slug });
            }

            member.status = "APPROVED";
            if (!string.IsNullOrWhiteSpace(role))
            {
                var normRole = role.Trim().ToUpper();
                var validRoles = new[] { "MEMBER", "OFFICER", "SECRETARY", "TREASURER", "ADMIN", "PRESIDENT" };
                if (validRoles.Contains(normRole))
                {
                    member.membership_role = normRole;
                }
            }
            member.reviewed_at = DateTime.UtcNow;
            member.reviewed_by = currentUserId;

            _db.notifications.Add(new Notification
            {
                user_id = member.user_id,
                title = "Club Membership Approved!",
                message = $"Congratulations! Your membership request for '{member.club.name}' was APPROVED as {member.membership_role}.",
                notification_type = "CLUB",
                related_entity_type = "CLUB",
                related_entity_id = member.club_id,
                action_url = $"/Clubs/Details/{member.club.slug}",
                is_read = false,
                created_at = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await LogAuditAsync("CLUB_MEMBER_APPROVED", "CLUB_MEMBER", member.id, $"Approved membership for {member.user.first_name} {member.user.last_name} in {member.club.name} as {member.membership_role}");
            TempData["SuccessMessage"] = $"Approved membership for {member.user.first_name} {member.user.last_name} as '{member.membership_role}'.";

            return RedirectToAction(nameof(ManageMembers), new { id = member.club_id });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectMember(ulong memberRecordId)
        {
            var member = await _db.club_members
                .Include(m => m.club)
                .Include(m => m.user)
                .FirstOrDefaultAsync(m => m.id == memberRecordId);

            if (member == null) return NotFound();

            var (currentUserId, _) = GetCurrentUserId();
            var isSuperOrAdmin = IsAdminOrSuperAdmin();
            var isPresident = currentUserId.HasValue && member.club.president_id == currentUserId.Value;
            var isOfficer = currentUserId.HasValue && await _db.club_members.AnyAsync(m => m.club_id == member.club_id && m.user_id == currentUserId.Value && m.status == "APPROVED" && (m.membership_role == "ADMIN" || m.membership_role == "OFFICER" || m.membership_role == "SECRETARY" || m.membership_role == "TREASURER" || m.membership_role == "PRESIDENT"));

            if (!isSuperOrAdmin && !isPresident && !isOfficer)
            {
                TempData["ErrorMessage"] = "Access Denied: You do not have permission to reject members for this club.";
                return RedirectToAction(nameof(Details), new { id = member.club.slug });
            }

            member.status = "REJECTED";
            member.reviewed_at = DateTime.UtcNow;
            member.reviewed_by = currentUserId;

            _db.notifications.Add(new Notification
            {
                user_id = member.user_id,
                title = "Club Membership Update",
                message = $"Your membership request for '{member.club.name}' has been reviewed by club leadership.",
                notification_type = "CLUB",
                related_entity_type = "CLUB",
                related_entity_id = member.club_id,
                action_url = $"/Clubs/Details/{member.club.slug}",
                is_read = false,
                created_at = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await LogAuditAsync("CLUB_MEMBER_REJECTED", "CLUB_MEMBER", member.id, $"Rejected membership request for {member.user.first_name} {member.user.last_name} in {member.club.name}");
            TempData["InfoMessage"] = $"Rejected membership application for {member.user.first_name} {member.user.last_name}.";

            return RedirectToAction(nameof(ManageMembers), new { id = member.club_id });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMemberRole(ulong memberRecordId, string role)
        {
            var member = await _db.club_members
                .Include(m => m.club)
                .Include(m => m.user)
                .FirstOrDefaultAsync(m => m.id == memberRecordId);

            if (member == null) return NotFound();

            var (currentUserId, _) = GetCurrentUserId();
            var isSuperOrAdmin = IsAdminOrSuperAdmin();
            var isPresident = currentUserId.HasValue && member.club.president_id == currentUserId.Value;
            var isOfficerAdmin = currentUserId.HasValue && await _db.club_members.AnyAsync(m => m.club_id == member.club_id && m.user_id == currentUserId.Value && m.status == "APPROVED" && (m.membership_role == "ADMIN" || m.membership_role == "PRESIDENT"));

            if (!isSuperOrAdmin && !isPresident && !isOfficerAdmin)
            {
                TempData["ErrorMessage"] = "Access Denied: Only club presidents or administrators can update leadership roles.";
                return RedirectToAction(nameof(ManageMembers), new { id = member.club_id });
            }

            var validRoles = new[] { "MEMBER", "OFFICER", "SECRETARY", "TREASURER", "ADMIN", "PRESIDENT" };
            var normalizedRole = role?.Trim().ToUpper() ?? "MEMBER";
            if (!validRoles.Contains(normalizedRole))
            {
                TempData["ErrorMessage"] = $"Invalid role specified: {role}";
                return RedirectToAction(nameof(ManageMembers), new { id = member.club_id });
            }

            member.membership_role = normalizedRole;
            await _db.SaveChangesAsync();

            await LogAuditAsync("CLUB_MEMBER_ROLE_UPDATED", "CLUB_MEMBER", member.id, $"Updated role of {member.user.first_name} {member.user.last_name} to {normalizedRole} in club {member.club.name}");
            TempData["SuccessMessage"] = $"Updated role for {member.user.first_name} {member.user.last_name} to '{normalizedRole}'.";

            return RedirectToAction(nameof(ManageMembers), new { id = member.club_id });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(ulong memberRecordId)
        {
            var member = await _db.club_members
                .Include(m => m.club)
                .Include(m => m.user)
                .FirstOrDefaultAsync(m => m.id == memberRecordId);

            if (member == null) return NotFound();

            var (currentUserId, _) = GetCurrentUserId();
            var isSuperOrAdmin = IsAdminOrSuperAdmin();
            var isPresident = currentUserId.HasValue && member.club.president_id == currentUserId.Value;

            if (!isSuperOrAdmin && !isPresident)
            {
                TempData["ErrorMessage"] = "Access Denied: Only club presidents or administrators can remove members.";
                return RedirectToAction(nameof(ManageMembers), new { id = member.club_id });
            }

            var clubId = member.club_id;
            var memberName = $"{member.user.first_name} {member.user.last_name}".Trim();
            _db.club_members.Remove(member);
            await _db.SaveChangesAsync();

            await LogAuditAsync("CLUB_MEMBER_REMOVED", "CLUB_MEMBER", memberRecordId, $"Removed {memberName} from club {member.club.name}");
            TempData["SuccessMessage"] = $"Removed {memberName} from the membership roster.";

            return RedirectToAction(nameof(ManageMembers), new { id = clubId });
        }

        // =====================================================================
        // 7. CREATE & EDIT CLUB (Authorized Roles)
        // =====================================================================
        [Authorize(Roles = "SuperAdmin,Admin,Faculty,Staff,Organization,SUPERADMIN,ADMIN,FACULTY,STAFF,ORGANIZATION")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new ClubCreateEditViewModel();
            await PopulateClubFormDropdownsAsync(vm);
            return View(vm);
        }

        [Authorize(Roles = "SuperAdmin,Admin,Faculty,Staff,Organization,SUPERADMIN,ADMIN,FACULTY,STAFF,ORGANIZATION")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClubCreateEditViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError("Name", "Club name is required.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateClubFormDropdownsAsync(model);
                return View(model);
            }

            var slug = GenerateSlug(model.Name);
            var slugBase = slug;
            int counter = 1;
            while (await _db.clubs.AnyAsync(c => c.slug == slug))
            {
                slug = $"{slugBase}-{counter++}";
            }

            var (currentUserId, _) = GetCurrentUserId();

            var club = new Club
            {
                name = model.Name.Trim(),
                slug = slug,
                short_name = model.ShortName?.Trim(),
                description = model.Description?.Trim(),
                logo_url = model.LogoUrl?.Trim(),
                cover_image_url = model.CoverImageUrl?.Trim(),
                faculty_id = model.FacultyId,
                department_id = model.DepartmentId,
                organization_id = model.OrganizationId,
                president_id = model.PresidentId ?? currentUserId,
                status = "ACTIVE",
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            _db.clubs.Add(club);
            await _db.SaveChangesAsync();

            // Add selected interests
            if (model.SelectedCategoryIds != null && model.SelectedCategoryIds.Any())
            {
                foreach (var catId in model.SelectedCategoryIds.Distinct())
                {
                    _db.club_interests.Add(new ClubInterest
                    {
                        club_id = club.id,
                        category_id = catId,
                        created_at = DateTime.UtcNow
                    });
                }
            }

            // Assign President in ClubMembers
            if (club.president_id.HasValue)
            {
                _db.club_members.Add(new ClubMember
                {
                    club_id = club.id,
                    user_id = club.president_id.Value,
                    membership_role = "PRESIDENT",
                    status = "APPROVED",
                    applied_at = DateTime.UtcNow,
                    reviewed_at = DateTime.UtcNow,
                    reviewed_by = currentUserId
                });
            }

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Club '{club.name}' created successfully with {model.SelectedCategoryIds?.Count ?? 0} interests assigned!";
            return RedirectToAction(nameof(Details), new { id = club.slug });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(ulong id)
        {
            var club = await _db.clubs
                .Include(c => c.club_interests)
                .FirstOrDefaultAsync(c => c.id == id);

            if (club == null) return NotFound();

            var (currentUserId, _) = GetCurrentUserId();
            var canManage = IsAdminOrSuperAdmin() || (currentUserId.HasValue && club.president_id == currentUserId.Value);

            if (!canManage)
            {
                TempData["ErrorMessage"] = "Access Denied: You do not have permission to edit this club.";
                return RedirectToAction(nameof(Details), new { id = club.slug });
            }

            var vm = new ClubCreateEditViewModel
            {
                Id = club.id,
                Name = club.name,
                Slug = club.slug,
                ShortName = club.short_name,
                Description = club.description,
                LogoUrl = club.logo_url,
                CoverImageUrl = club.cover_image_url,
                FacultyId = club.faculty_id,
                DepartmentId = club.department_id,
                OrganizationId = club.organization_id,
                PresidentId = club.president_id,
                Status = club.status,
                SelectedCategoryIds = club.club_interests.Select(ci => ci.category_id).ToList()
            };

            await PopulateClubFormDropdownsAsync(vm);
            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ulong id, ClubCreateEditViewModel model)
        {
            var club = await _db.clubs
                .Include(c => c.club_interests)
                .FirstOrDefaultAsync(c => c.id == id);

            if (club == null) return NotFound();

            var (currentUserId, _) = GetCurrentUserId();
            var canManage = IsAdminOrSuperAdmin() || (currentUserId.HasValue && club.president_id == currentUserId.Value);

            if (!canManage)
            {
                TempData["ErrorMessage"] = "Access Denied: You do not have permission to edit this club.";
                return RedirectToAction(nameof(Details), new { id = club.slug });
            }

            club.name = model.Name.Trim();
            club.short_name = model.ShortName?.Trim();
            club.description = model.Description?.Trim();
            club.logo_url = model.LogoUrl?.Trim();
            club.cover_image_url = model.CoverImageUrl?.Trim();
            club.faculty_id = model.FacultyId;
            club.department_id = model.DepartmentId;
            club.organization_id = model.OrganizationId;
            club.president_id = model.PresidentId;
            club.status = model.Status;
            club.updated_at = DateTime.UtcNow;

            // Sync Interests
            _db.club_interests.RemoveRange(club.club_interests);
            if (model.SelectedCategoryIds != null && model.SelectedCategoryIds.Any())
            {
                foreach (var catId in model.SelectedCategoryIds.Distinct())
                {
                    _db.club_interests.Add(new ClubInterest
                    {
                        club_id = club.id,
                        category_id = catId,
                        created_at = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync();
            await LogAuditAsync("CLUB_UPDATED", "CLUB", club.id, $"Updated club details for '{club.name}'");
            TempData["SuccessMessage"] = $"Club '{club.name}' updated successfully.";

            return RedirectToAction(nameof(Details), new { id = club.slug });
        }

        // =====================================================================
        // 8. ADMIN CLUB GOVERNANCE: STATUS TOGGLE & DELETION
        // =====================================================================
        [Authorize(Roles = "SuperAdmin,Admin,SUPERADMIN,ADMIN")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(ulong id, string status)
        {
            var club = await _db.clubs.FindAsync(id);
            if (club == null) return NotFound();

            var validStatuses = new[] { "ACTIVE", "PENDING", "SUSPENDED", "INACTIVE" };
            var normalized = status?.Trim().ToUpper() ?? "ACTIVE";
            if (!validStatuses.Contains(normalized))
            {
                TempData["ErrorMessage"] = $"Invalid club status: {status}";
                return RedirectToAction(nameof(Details), new { id = club.slug });
            }

            var oldStatus = club.status;
            club.status = normalized;
            club.updated_at = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogAuditAsync("CLUB_STATUS_CHANGED", "CLUB", club.id, $"Changed club '{club.name}' status from {oldStatus} to {normalized}");
            TempData["SuccessMessage"] = $"Club '{club.name}' status changed to '{normalized}'.";

            return RedirectToAction(nameof(Details), new { id = club.slug });
        }

        [Authorize(Roles = "SuperAdmin,Admin,SUPERADMIN,ADMIN")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(ulong id)
        {
            var club = await _db.clubs.FindAsync(id);
            if (club == null) return NotFound();

            var clubName = club.name;
            _db.clubs.Remove(club);
            await _db.SaveChangesAsync();

            await LogAuditAsync("CLUB_DELETED", "CLUB", id, $"Permanently deleted club '{clubName}'");
            TempData["SuccessMessage"] = $"Club '{clubName}' has been permanently deleted.";

            return RedirectToAction(nameof(Index));
        }

        // =====================================================================
        // 9. MY CLUBS HUB (GET: /Clubs/MyClubs)
        // =====================================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyClubs()
        {
            var (userId, _) = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var followedClubIds = await _db.club_followers
                .Where(f => f.user_id == userId.Value)
                .Select(f => f.club_id)
                .ToListAsync();

            var memberRecords = await _db.club_members
                .Where(m => m.user_id == userId.Value)
                .ToListAsync();

            var membershipClubIds = memberRecords.Select(m => m.club_id).ToList();

            var allRelatedClubIds = followedClubIds.Union(membershipClubIds).Distinct().ToList();

            var clubs = await _db.clubs
                .Include(c => c.department)
                .Include(c => c.faculty)
                .Include(c => c.president)
                .Include(c => c.club_interests).ThenInclude(ci => ci.category)
                .Include(c => c.club_followers)
                .Include(c => c.club_members)
                .Where(c => allRelatedClubIds.Contains(c.id) || c.president_id == userId.Value)
                .ToListAsync();

            var memberDict = memberRecords.ToDictionary(m => m.club_id, m => m);

            var vm = new MyClubsViewModel();

            foreach (var club in clubs)
            {
                var isFollowing = followedClubIds.Contains(club.id);
                var member = memberDict.ContainsKey(club.id) ? memberDict[club.id] : null;
                var isPresident = club.president_id == userId.Value;

                var card = new ClubCardViewModel
                {
                    Id = club.id,
                    Name = club.name,
                    Slug = club.slug,
                    ShortName = club.short_name,
                    Description = club.description,
                    LogoUrl = club.logo_url,
                    DepartmentName = club.department?.name,
                    FacultyName = club.faculty?.name,
                    Interests = club.club_interests.Select(ci => new ClubInterestBadge { Id = ci.category_id, Name = ci.category?.name ?? "General" }).ToList(),
                    FollowerCount = club.club_followers.Count,
                    MemberCount = club.club_members.Count(m => m.status == "APPROVED"),
                    IsFollowing = isFollowing,
                    MembershipStatus = member?.status ?? "NONE",
                    MembershipRole = member?.membership_role,
                    IsPresidentOrAdmin = isPresident || IsAdminOrSuperAdmin()
                };

                if (isFollowing) vm.FollowedClubs.Add(card);
                if (member != null) vm.MembershipClubs.Add(card);
                if (isPresident) vm.ManagedClubs.Add(card);
            }

            return View(vm);
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private (ulong? UserId, bool IsAuthenticated) GetCurrentUserId()
        {
            if (User.Identity?.IsAuthenticated != true) return (null, false);

            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("id")?.Value
                ?? User.FindFirst("nameid")?.Value;

            if (ulong.TryParse(idClaim, out ulong uid))
                return (uid, true);

            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                var user = _db.users.FirstOrDefault(u => u.email == email);
                if (user != null) return (user.id, true);
            }

            var name = User.Identity?.Name;
            if (!string.IsNullOrEmpty(name))
            {
                var user = _db.users.FirstOrDefault(u => u.username == name || (u.first_name + " " + u.last_name).Trim() == name);
                if (user != null) return (user.id, true);
            }

            return (null, true);
        }

        private bool IsAdminOrSuperAdmin()
        {
            return User.IsInRole("Admin") || User.IsInRole("SuperAdmin") ||
                   User.IsInRole("ADMIN") || User.IsInRole("SUPERADMIN");
        }

        private static string GenerateSlug(string text)
        {
            var str = text.ToLowerInvariant();
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
            str = Regex.Replace(str, @"\s+", " ").Trim();
            str = Regex.Replace(str, @"\s", "-");
            return str;
        }

        private async Task PopulateClubFormDropdownsAsync(ClubCreateEditViewModel vm)
        {
            var faculties = await _db.faculties.OrderBy(f => f.name).ToListAsync();
            vm.AvailableFaculties = faculties.Select(f => new SelectListItem { Value = f.id.ToString(), Text = f.name }).ToList();

            var depts = await _db.departments.OrderBy(d => d.name).ToListAsync();
            vm.AvailableDepartments = depts.Select(d => new SelectListItem { Value = d.id.ToString(), Text = d.name }).ToList();

            var orgs = await _db.organizations.Where(o => o.status == "ACTIVE").OrderBy(o => o.name).ToListAsync();
            vm.AvailableOrganizations = orgs.Select(o => new SelectListItem { Value = o.id.ToString(), Text = o.name }).ToList();

            var users = await _db.users.Where(u => u.account_status == "ACTIVE").OrderBy(u => u.first_name).Take(50).ToListAsync();
            vm.AvailableUsers = users.Select(u => new SelectListItem { Value = u.id.ToString(), Text = $"{u.first_name} {u.last_name} ({u.email})" }).ToList();

            var categories = await _db.event_categories.Where(c => c.is_active != false).OrderBy(c => c.name).ToListAsync();
            vm.AvailableCategories = categories.Select(c => new InterestCategoryOption
            {
                Id = c.id,
                Name = c.name,
                Description = c.description,
                Icon = c.icon,
                IsSelected = vm.SelectedCategoryIds != null && vm.SelectedCategoryIds.Contains(c.id)
            }).ToList();
        }

        private async Task LogAuditAsync(string action, string? entityType = null, ulong? entityId = null, string? description = null)
        {
            try
            {
                var (uid, _) = GetCurrentUserId();
                var audit = new audit_log
                {
                    user_id = uid,
                    action = action,
                    entity_type = entityType,
                    entity_id = entityId,
                    description = description,
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    user_agent = Request.Headers["User-Agent"].ToString(),
                    created_at = DateTime.UtcNow
                };
                _db.audit_logs.Add(audit);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record audit log for action: {Action}", action);
            }
        }
    }
}
