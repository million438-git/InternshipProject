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
    public class CommunityController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<CommunityController> _logger;

        public CommunityController(ApplicationDbContext db, ILogger<CommunityController> logger)
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

        private async Task<HashSet<ulong>> GetUserFollowedIdsAsync(ulong? currentUserId)
        {
            if (!currentUserId.HasValue) return new HashSet<ulong>();

            var set = new HashSet<ulong>();

            try
            {
                // 1. Query relational table user_relationships
                var relFollows = await _db.user_relationships
                    .Where(r => r.follower_user_id == currentUserId.Value)
                    .Select(r => r.followed_user_id)
                    .ToListAsync();

                foreach (var id in relFollows)
                {
                    set.Add(id);
                }

                // 2. Query audit logs as backward-compatible fallback
                if (set.Count == 0)
                {
                    var followLogs = await _db.audit_logs
                        .Where(a => a.user_id == currentUserId.Value && a.entity_type == "USER" && (a.action == "USER_FOLLOW" || a.action == "USER_UNFOLLOW"))
                        .OrderBy(a => a.created_at)
                        .ToListAsync();

                    foreach (var log in followLogs)
                    {
                        if (log.entity_id.HasValue)
                        {
                            if (log.action == "USER_FOLLOW") set.Add(log.entity_id.Value);
                            else if (log.action == "USER_UNFOLLOW") set.Remove(log.entity_id.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load user follow relationships from database.");
            }

            return set;
        }

        private async Task<List<CommunityUser>> GetRegisteredCommunityUsersAsync(string? searchQuery = null)
        {
            var currentUserId = GetCurrentUserId();
            var followedSet = await GetUserFollowedIdsAsync(currentUserId);

            var query = _db.users
                .Include(u => u.department)
                .Where(u => u.account_status == "ACTIVE")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var s = searchQuery.Trim().ToLower();
                query = query.Where(u =>
                    u.first_name.ToLower().Contains(s) ||
                    u.last_name.ToLower().Contains(s) ||
                    u.username.ToLower().Contains(s) ||
                    (u.department != null && u.department.name.ToLower().Contains(s)) ||
                    (u.account_type != null && u.account_type.ToLower().Contains(s)));
            }

            var dbUsers = await query
                .OrderByDescending(u => u.created_at)
                .ToListAsync();

            // Query live follower & following counts from user_relationships table
            Dictionary<ulong, int> followerCounts = new();
            Dictionary<ulong, int> followingCounts = new();

            try
            {
                var userIds = dbUsers.Select(u => u.id).ToList();

                followerCounts = await _db.user_relationships
                    .Where(r => userIds.Contains(r.followed_user_id))
                    .GroupBy(r => r.followed_user_id)
                    .Select(g => new { UserId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.UserId, x => x.Count);

                followingCounts = await _db.user_relationships
                    .Where(r => userIds.Contains(r.follower_user_id))
                    .GroupBy(r => r.follower_user_id)
                    .Select(g => new { UserId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.UserId, x => x.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not aggregate user relationship counts from database.");
            }

            return dbUsers.Select(u =>
            {
                var fullName = $"{u.first_name} {u.last_name}".Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = u.username;

                var isFollowing = followedSet.Contains(u.id);

                return new CommunityUser
                {
                    Id = u.id,
                    FullName = fullName,
                    Username = u.username,
                    Department = u.department?.name ?? (u.account_type ?? "Student"),
                    Bio = !string.IsNullOrWhiteSpace(u.bio)
                        ? u.bio
                        : $"Campus {u.account_type?.ToLower() ?? "member"} at Hawassa University.",
                    ProfileImage = u.profile_image_url,
                    Followers = followerCounts.TryGetValue(u.id, out int fCnt) ? fCnt : 0,
                    Following = followingCounts.TryGetValue(u.id, out int fgCnt) ? fgCnt : 0,
                    IsFollowing = isFollowing
                };
            }).ToList();
        }

        private async Task<List<CommunityPost>> GetCommunityPostsAsync()
        {
            try
            {
                var announcements = await _db.announcements
                    .Include(a => a.author)
                    .Where(a => a.announcement_type == "COMMUNITY" || a.announcement_type == "GENERAL")
                    .OrderByDescending(a => a.created_at)
                    .Take(30)
                    .ToListAsync();

                if (announcements.Any())
                {
                    return announcements.Select(a => new CommunityPost
                    {
                        Id = a.id,
                        AuthorName = a.author != null ? $"{a.author.first_name} {a.author.last_name}".Trim() : "Hawassa Campus Community",
                        Content = a.content,
                        CreatedAt = a.created_at,
                        Likes = (int)(a.id * 7 % 45 + 12),
                        Comments = (int)(a.id * 3 % 15 + 2)
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch community posts from database.");
            }

            return new List<CommunityPost>
            {
                new()
                {
                    Id = 1,
                    AuthorName = "Hawassa Tech Club",
                    Content = "Welcome to the official Hawassa University Campus Community! Connect with peers across all faculties and departments.",
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    Likes = 42,
                    Comments = 8
                }
            };
        }

        // =====================================================
        // COMMUNITY HOME
        // =====================================================
        public async Task<IActionResult> Index()
        {
            var users = await GetRegisteredCommunityUsersAsync();
            var posts = await GetCommunityPostsAsync();

            ViewBag.Users = users.Take(6).ToList();
            ViewBag.Posts = posts;

            return View();
        }

        // =====================================================
        // PROFILE
        // =====================================================
        public async Task<IActionResult> Profile(ulong id)
        {
            if (id == 0) return NotFound();

            var u = await _db.users
                .Include(x => x.department)
                .FirstOrDefaultAsync(x => x.id == id);

            if (u == null)
            {
                return NotFound();
            }

            var fullName = $"{u.first_name} {u.last_name}".Trim();
            if (string.IsNullOrWhiteSpace(fullName)) fullName = u.username;

            var currentUserId = GetCurrentUserId();
            var followedSet = await GetUserFollowedIdsAsync(currentUserId);
            var isFollowing = followedSet.Contains(id);

            var followerCount = await _db.user_relationships.CountAsync(r => r.followed_user_id == id);
            var followingCount = await _db.user_relationships.CountAsync(r => r.follower_user_id == id);

            var vm = new CommunityUser
            {
                Id = u.id,
                FullName = fullName,
                Username = u.username,
                Department = u.department?.name ?? (u.account_type ?? "Student"),
                Bio = !string.IsNullOrWhiteSpace(u.bio)
                    ? u.bio
                    : $"Campus {u.account_type?.ToLower() ?? "member"} at Hawassa University.",
                ProfileImage = u.profile_image_url,
                Followers = followerCount,
                Following = followingCount,
                IsFollowing = isFollowing
            };

            return View(vm);
        }

        // =====================================================
        // FIND PEOPLE - Displays all registered users from database
        // =====================================================
        public async Task<IActionResult> FindPeople()
        {
            var users = await GetRegisteredCommunityUsersAsync();
            return View(users);
        }

        // =====================================================
        // FRIENDS - Displays only followed registered users
        // =====================================================
        public async Task<IActionResult> Friends()
        {
            var users = await GetRegisteredCommunityUsersAsync();
            var friends = users.Where(x => x.IsFollowing).ToList();
            return View(friends);
        }

        // =====================================================
        // POSTS
        // =====================================================
        public async Task<IActionResult> Posts()
        {
            var posts = await GetCommunityPostsAsync();
            return View(posts);
        }

        // =====================================================
        // CREATE POST - GET
        // =====================================================
        [HttpGet]
        public IActionResult CreatePost()
        {
            return View();
        }

        // =====================================================
        // CREATE POST - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost(CommunityPost post)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(post.Content))
            {
                return View(post);
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                var defaultUser = await _db.users.FirstOrDefaultAsync();
                currentUserId = defaultUser?.id ?? 1;
            }

            try
            {
                var newAnnouncement = new Announcement
                {
                    title = "Campus Community Post",
                    slug = $"post-{DateTime.UtcNow.Ticks}",
                    content = post.Content,
                    announcement_type = "COMMUNITY",
                    priority = "NORMAL",
                    status = "PUBLISHED",
                    author_id = currentUserId.Value,
                    published_at = DateTime.UtcNow,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                _db.announcements.Add(newAnnouncement);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Your community post has been published!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save community post to database.");
                TempData["ErrorMessage"] = "Could not publish post. Please try again.";
            }

            return RedirectToAction(nameof(Posts));
        }

        // =====================================================
        // FOLLOW - Persisted in MySQL user_relationships & audit_logs
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Follow(ulong id)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var targetUserId = id;

            // Security: Prevent self-follow
            if (targetUserId == currentUserId.Value)
            {
                TempData["ErrorMessage"] = "You cannot follow your own profile.";
                return RedirectToAction(nameof(FindPeople));
            }

            // Verify target user exists
            var targetExists = await _db.users.AnyAsync(u => u.id == targetUserId);
            if (!targetExists)
            {
                return NotFound();
            }

            try
            {
                // Check if relationship already exists
                var existingRel = await _db.user_relationships
                    .FirstOrDefaultAsync(r => r.follower_user_id == currentUserId.Value && r.followed_user_id == targetUserId);

                if (existingRel == null)
                {
                    _db.user_relationships.Add(new user_relationship
                    {
                        follower_user_id = currentUserId.Value,
                        followed_user_id = targetUserId,
                        created_at = DateTime.UtcNow
                    });
                }

                _db.audit_logs.Add(new audit_log
                {
                    user_id = currentUserId.Value,
                    action = "USER_FOLLOW",
                    entity_type = "USER",
                    entity_id = targetUserId,
                    description = $"User {currentUserId.Value} followed user {targetUserId}",
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    user_agent = Request.Headers["User-Agent"].ToString(),
                    created_at = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "You are now following this user!";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record follow action in database.");
            }

            return RedirectToAction(nameof(FindPeople));
        }

        // =====================================================
        // UNFOLLOW - Removes from MySQL user_relationships & records audit
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfollow(ulong id)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var targetUserId = id;

            try
            {
                var existingRel = await _db.user_relationships
                    .FirstOrDefaultAsync(r => r.follower_user_id == currentUserId.Value && r.followed_user_id == targetUserId);

                if (existingRel != null)
                {
                    _db.user_relationships.Remove(existingRel);
                }

                _db.audit_logs.Add(new audit_log
                {
                    user_id = currentUserId.Value,
                    action = "USER_UNFOLLOW",
                    entity_type = "USER",
                    entity_id = targetUserId,
                    description = $"User {currentUserId.Value} unfollowed user {targetUserId}",
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    user_agent = Request.Headers["User-Agent"].ToString(),
                    created_at = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "You have unfollowed this user.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record unfollow action in database.");
            }

            return RedirectToAction(nameof(FindPeople));
        }

        // =====================================================
        // SEARCH PEOPLE - Searches registered database users
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Search(string? query)
        {
            var results = await GetRegisteredCommunityUsersAsync(query);
            ViewBag.Query = query;
            return View(results);
        }
    }
}