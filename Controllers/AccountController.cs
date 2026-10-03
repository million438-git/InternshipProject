using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Models;
using HawassaUnifiedCampusEventManagementSystem.Services;

namespace HawassaUnifiedCampusEventManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _env;
        private readonly IPasswordService _passwords;

        public AccountController(
            ApplicationDbContext db, 
            ILogger<AccountController> logger,
            IEmailSender emailSender,
            IWebHostEnvironment env,
            IPasswordService passwords)
        {
            _db = db;
            _logger = logger;
            _emailSender = emailSender;
            _env = env;
            _passwords = passwords;
        }

        private static string GenerateSecureToken()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string HashToken(string token)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token.Trim()));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        // =====================================================
        // LOGIN
        // =====================================================

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (Request.Query.ContainsKey("tooManyAttempts"))
            {
                ViewBag.Error = "Too many login attempts. Please wait a few minutes and try again.";
            }

            if (User.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "Student";
                return userRole switch
                {
                    "SuperAdmin" => RedirectToAction("SuperAdmin", "Admin"),
                    "Admin" => RedirectToAction("Index", "Admin"),
                    "Faculty" or "Staff" => RedirectToAction("Staff", "Dashboard"),
                    "Organization" => RedirectToAction("Organization", "Dashboard"),
                    _ => RedirectToAction("Student", "Dashboard")
                };
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe = false, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please enter your username or email.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter your password.";
                return View();
            }

            var identifier = email.Trim();

            // Query user from database by username or email
            User? dbUser = null;
            try
            {
                dbUser = await _db.users
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.role)
                    .FirstOrDefaultAsync(u => u.email.ToLower() == identifier.ToLower() || u.username.ToLower() == identifier.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database lookup failed during login.");
                ViewBag.Error = "A database connection error occurred. Please ensure the database server is running.";
                return View();
            }

            if (dbUser == null || !_passwords.VerifyPassword(dbUser, password, dbUser.password_hash))
            {
                ViewBag.Error = "Invalid username or password. Please try again.";
                return View();
            }

            if (dbUser.account_status == "PENDING" || dbUser.account_status == "PENDING_APPROVAL")
            {
                ViewBag.Error = "Your account registration is pending administrator approval.";
                return View();
            }

            if (dbUser.account_status != "ACTIVE")
            {
                ViewBag.Error = "Your account is currently unavailable. Please contact an administrator.";
                return View();
            }

            // Upgrade legacy hash format if needed and update last login
            try
            {
                dbUser.last_login_at = DateTime.UtcNow;
                if (_passwords.IsLegacyHash(dbUser.password_hash))
                {
                    dbUser.password_hash = _passwords.HashPassword(password);
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not update last login timestamp.");
            }

            // Resolve user role
            var userRole = ResolveUserRole(dbUser);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, dbUser.id.ToString()),
                new Claim(ClaimTypes.Name, $"{dbUser.first_name} {dbUser.last_name}".Trim()),
                new Claim(ClaimTypes.Email, dbUser.email),
                new Claim(ClaimTypes.Role, userRole)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProps = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : (DateTimeOffset?)null
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProps);

            TempData["SuccessMessage"] = $"Welcome back, {dbUser.first_name}!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // Direct route based on authenticated role
            return userRole switch
            {
                "SuperAdmin" => RedirectToAction("SuperAdmin", "Admin"),
                "Admin" => RedirectToAction("Index", "Admin"),
                "Faculty" or "Staff" => RedirectToAction("Staff", "Dashboard"),
                "Organization" => RedirectToAction("Organization", "Dashboard"),
                _ => RedirectToAction("Student", "Dashboard")
            };
        }

        private static string ResolveUserRole(User u)
        {
            // Check explicit role assignments first
            if (u.user_roleusers != null && u.user_roleusers.Any())
            {
                foreach (var ur in u.user_roleusers)
                {
                    var rName = ur.role?.name?.Trim();
                    if (string.IsNullOrEmpty(rName)) continue;

                    if (rName.Contains("Super", StringComparison.OrdinalIgnoreCase)) return "SuperAdmin";
                    if (rName.Contains("Admin", StringComparison.OrdinalIgnoreCase)) return "Admin";
                    if (rName.Contains("Faculty", StringComparison.OrdinalIgnoreCase) || rName.Contains("Professor", StringComparison.OrdinalIgnoreCase)) return "Faculty";
                    if (rName.Contains("Staff", StringComparison.OrdinalIgnoreCase) || rName.Contains("Officer", StringComparison.OrdinalIgnoreCase)) return "Staff";
                    if (rName.Contains("Organization", StringComparison.OrdinalIgnoreCase) || rName.Contains("Organizer", StringComparison.OrdinalIgnoreCase) || rName.Contains("Club", StringComparison.OrdinalIgnoreCase)) return "Organization";
                }
            }

            // Fallback to account_type
            var accType = u.account_type?.Trim().ToUpperInvariant() ?? "STUDENT";
            return accType switch
            {
                "SUPERADMIN" or "SUPER_ADMIN" => "SuperAdmin",
                "ADMIN" or "ADMINISTRATOR" => "Admin",
                "FACULTY" or "PROFESSOR" or "INSTRUCTOR" => "Faculty",
                "STAFF" or "EMPLOYEE" => "Staff",
                "ORGANIZATION" or "ORGANIZER" or "CLUB" => "Organization",
                _ => "Student"
            };
        }

        private async Task LoadDepartmentsViewBagAsync()
        {
            try
            {
                ViewBag.Departments = await _db.departments
                    .Where(d => d.is_active == null || d.is_active == true)
                    .OrderBy(d => d.name)
                    .ToListAsync();
            }
            catch
            {
                ViewBag.Departments = new List<Department>();
            }
        }

        // =====================================================
        // REGISTER (CAMPUS SELF-REGISTRATION - ADMIN REVIEW REQUIRED)
        // =====================================================

        // GET: /Account/Register
        [HttpGet]
        public async Task<IActionResult> Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("SUPERADMIN") || User.IsInRole("ADMIN"))
                {
                    return RedirectToAction("Users", "Admin");
                }
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.ReturnUrl = returnUrl;
            await LoadDepartmentsViewBagAsync();
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string? returnUrl,
            string? accountType,
            string? fullName,
            string? username,
            string? email,
            string? phone,
            ulong? departmentId,
            string? studentId,
            string? employeeId,
            string? organizationName,
            string? organizationType,
            string? bio,
            string? password,
            string? confirmPassword)
        {
            try
            {
                var normType = (accountType ?? "STUDENT").Trim().ToUpperInvariant();
                if (normType == "ADMIN" || normType == "SUPERADMIN")
                {
                    ViewBag.Error = "Administrator accounts cannot be self-registered. They must be provisioned by a SuperAdmin.";
                    ViewBag.ReturnUrl = returnUrl;
                    await LoadDepartmentsViewBagAsync();
                    return View();
                }

                // 1. Resolve & validate full name
                string finalFirst = "";
                string finalLast = "";
                string? finalMiddle = null;
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    var parts = fullName.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                    finalFirst = parts.Length > 0 ? parts[0] : "";
                    if (parts.Length == 2)
                    {
                        finalLast = parts[1];
                    }
                    else if (parts.Length >= 3)
                    {
                        finalMiddle = parts[1];
                        finalLast = parts[2];
                    }
                    else
                    {
                        finalLast = finalFirst;
                    }
                }

                if (string.IsNullOrWhiteSpace(finalFirst) || finalFirst.Length < 2)
                {
                    ViewBag.Error = "Please enter your full legal name.";
                    ViewBag.ReturnUrl = returnUrl;
                    await LoadDepartmentsViewBagAsync();
                    return View();
                }
                if (string.IsNullOrWhiteSpace(finalLast))
                {
                    finalLast = finalFirst;
                }

                // 2. Validate Username
                if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
                {
                    ViewBag.Error = "Username must be at least 3 characters long.";
                    ViewBag.ReturnUrl = returnUrl;
                    await LoadDepartmentsViewBagAsync();
                    return View();
                }
                var cleanUsername = username.Trim().ToLowerInvariant().Replace(" ", "_");
                if (await _db.users.AnyAsync(u => u.username.ToLower() == cleanUsername))
                {
                    ViewBag.Error = $"The username '{cleanUsername}' is already taken. Please select another username.";
                    ViewBag.ReturnUrl = returnUrl;
                    await LoadDepartmentsViewBagAsync();
                    return View();
                }

                // 3. Validate Email
                if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
                {
                    ViewBag.Error = "Please provide a valid official email address.";
                    ViewBag.ReturnUrl = returnUrl;
                    await LoadDepartmentsViewBagAsync();
                    return View();
                }
                var cleanEmail = email.Trim().ToLowerInvariant();
                if (await _db.users.AnyAsync(u => u.email.ToLower() == cleanEmail))
                {
                    ViewBag.Error = $"An account with email '{cleanEmail}' already exists in the system.";
                    ViewBag.ReturnUrl = returnUrl;
                    await LoadDepartmentsViewBagAsync();
                    return View();
                }

                // 4. Validate Password
                if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                {
                    ViewBag.Error = "Password must be at least 8 characters long.";
                    ViewBag.ReturnUrl = returnUrl;
                    await LoadDepartmentsViewBagAsync();
                    return View();
                }
                if (password != confirmPassword)
                {
                    ViewBag.Error = "Passwords do not match. Please verify both fields.";
                    ViewBag.ReturnUrl = returnUrl;
                    await LoadDepartmentsViewBagAsync();
                    return View();
                }

                // 5. Validate Role-Specific Identifiers
                if (normType == "STUDENT" && !string.IsNullOrWhiteSpace(studentId))
                {
                    var sid = studentId.Trim();
                    if (await _db.users.AnyAsync(u => u.student_id != null && u.student_id.ToLower() == sid.ToLower()))
                    {
                        ViewBag.Error = $"Student ID '{sid}' is already registered with an existing account.";
                        ViewBag.ReturnUrl = returnUrl;
                        await LoadDepartmentsViewBagAsync();
                        return View();
                    }
                }
                else if ((normType == "STAFF" || normType == "FACULTY") && !string.IsNullOrWhiteSpace(employeeId))
                {
                    var eid = employeeId.Trim();
                    if (await _db.users.AnyAsync(u => u.employee_id != null && u.employee_id.ToLower() == eid.ToLower()))
                    {
                        ViewBag.Error = $"Employee ID '{eid}' is already registered with an existing account.";
                        ViewBag.ReturnUrl = returnUrl;
                        await LoadDepartmentsViewBagAsync();
                        return View();
                    }
                }

                // 6. Validate Department if provided
                ulong? validDeptId = null;
                if (departmentId.HasValue && departmentId.Value > 0)
                {
                    if (await _db.departments.AnyAsync(d => d.id == departmentId.Value))
                    {
                        validDeptId = departmentId.Value;
                    }
                }

                // 7. Compose Bio Metadata
                string? finalBio = !string.IsNullOrWhiteSpace(bio) ? bio.Trim() : null;
                if (string.IsNullOrWhiteSpace(finalBio))
                {
                    if (normType == "STUDENT")
                    {
                        finalBio = "Student Account";
                    }
                    else if (normType == "FACULTY")
                    {
                        finalBio = "Faculty Member";
                    }
                    else if (normType == "STAFF")
                    {
                        finalBio = "Staff Member";
                    }
                    else if (normType == "ORGANIZATION")
                    {
                        var orgN = !string.IsNullOrWhiteSpace(organizationName) ? organizationName.Trim() : "Student Organization";
                        finalBio = $"{orgN} - {organizationType ?? "Club"}";
                    }
                }

                // 8. Create User in PENDING Status
                var newUser = new User
                {
                    username = cleanUsername,
                    email = cleanEmail,
                    phone = phone?.Trim(),
                    password_hash = _passwords.HashPassword(password),
                    first_name = finalFirst,
                    middle_name = finalMiddle,
                    last_name = finalLast,
                    department_id = validDeptId,
                    student_id = normType == "STUDENT" ? studentId?.Trim() : null,
                    employee_id = (normType == "STAFF" || normType == "FACULTY") ? employeeId?.Trim() : null,
                    bio = finalBio,
                    account_type = normType,
                    account_status = "PENDING",
                    email_verified = false,
                    phone_verified = false,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                _db.users.Add(newUser);
                await _db.SaveChangesAsync();

                // 9. Assign Corresponding Role
                string roleName = normType switch
                {
                    "FACULTY" => "Faculty",
                    "STAFF" => "Staff",
                    "ORGANIZATION" => "Organization",
                    _ => "Student"
                };

                var roleObj = await _db.roles.FirstOrDefaultAsync(r => r.name.ToLower() == roleName.ToLower());
                if (roleObj != null)
                {
                    _db.user_roles.Add(new user_role
                    {
                        user_id = newUser.id,
                        role_id = roleObj.id,
                        assigned_at = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                }

                // 10. Record Security Audit Log
                _db.audit_logs.Add(new audit_log
                {
                    user_id = newUser.id,
                    action = "USER_REGISTERED",
                    entity_type = "USER",
                    entity_id = newUser.id,
                    description = $"Self-registered campus account ({normType}) pending administrative verification.",
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    user_agent = Request.Headers["User-Agent"].ToString(),
                    created_at = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Registration submitted successfully! Your account is pending administrator approval. You will be able to sign in once an administrator approves your account.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing student/campus self-registration");
                ViewBag.Error = "An error occurred while processing your registration. Please verify your details and try again.";
                ViewBag.ReturnUrl = returnUrl;
                await LoadDepartmentsViewBagAsync();
                return View();
            }
        }

        // =====================================================
        // LOGOUT
        // =====================================================

        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }

        // =====================================================
        // PROFILE
        // =====================================================

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            ViewData["Title"] = "My Profile";
            ViewBag.Departments = await _db.departments.Where(d => d.is_active == true).OrderBy(d => d.name).ToListAsync();

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            if (!string.IsNullOrEmpty(userIdStr) && ulong.TryParse(userIdStr, out ulong uid))
            {
                try
                {
                    var dbUser = await _db.users
                        .Include(u => u.department)
                        .FirstOrDefaultAsync(u => u.id == uid);

                    if (dbUser != null)
                    {
                        ViewData["UserName"] = $"{dbUser.first_name} {dbUser.last_name}".Trim();
                        ViewData["FirstName"] = dbUser.first_name;
                        ViewData["LastName"] = dbUser.last_name;
                        ViewData["Phone"] = dbUser.phone ?? "";
                        ViewData["Bio"] = dbUser.bio ?? "";
                        ViewData["DepartmentId"] = dbUser.department_id;
                        ViewData["Email"] = dbUser.email;
                        ViewData["Role"] = dbUser.account_type ?? "Student";
                        ViewData["Department"] = dbUser.department?.name ?? "Not assigned";
                        ViewData["University"] = "Hawassa University";
                        ViewData["UserId"] = $"HUCEMS-{dbUser.id:D4}";

                        // Personalization Metrics
                        ViewData["SubscribedDeptsCount"] = await _db.user_dept_subscriptions.CountAsync(s => s.user_id == uid);
                        ViewData["SelectedInterestsCount"] = await _db.user_category_interests.CountAsync(i => i.user_id == uid);
                        ViewData["UserInterests"] = await _db.user_category_interests
                            .Include(i => i.category)
                            .Where(i => i.user_id == uid)
                            .Select(i => i.category.name)
                            .ToListAsync();

                        return View();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load user profile from DB.");
                }
            }

            ViewData["UserName"] = User.Identity?.Name ?? "Campus Member";
            ViewData["FirstName"] = "Campus";
            ViewData["LastName"] = "Member";
            ViewData["Email"] = userEmail ?? string.Empty;
            ViewData["Role"] = User.FindFirstValue(ClaimTypes.Role) ?? "Student";
            ViewData["Department"] = "Not assigned";
            ViewData["University"] = "Hawassa University";
            ViewData["UserId"] = string.IsNullOrEmpty(userIdStr) ? string.Empty : $"HUCEMS-{userIdStr}";

            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string firstName, string lastName, string? phone, string? bio, ulong? departmentId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !ulong.TryParse(userIdStr, out ulong uid))
            {
                return Unauthorized();
            }

            var user = await _db.users.FindAsync(uid);
            if (user == null) return NotFound();

            user.first_name = !string.IsNullOrWhiteSpace(firstName) ? firstName.Trim() : user.first_name;
            user.last_name = !string.IsNullOrWhiteSpace(lastName) ? lastName.Trim() : user.last_name;
            user.phone = phone?.Trim();
            user.bio = bio?.Trim();
            if (departmentId.HasValue && departmentId.Value > 0)
            {
                user.department_id = departmentId.Value;
            }
            user.updated_at = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Your profile information has been updated successfully!";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["ErrorMessage"] = "Please provide both current and new passwords.";
                return RedirectToAction(nameof(Profile));
            }

            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "New password and confirmation do not match.";
                return RedirectToAction(nameof(Profile));
            }

            if (newPassword.Length < 8)
            {
                TempData["ErrorMessage"] = "New password must be at least 8 characters long.";
                return RedirectToAction(nameof(Profile));
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !ulong.TryParse(userIdStr, out ulong uid))
            {
                return Unauthorized();
            }

            var user = await _db.users.FindAsync(uid);
            if (user == null) return NotFound();

            if (!_passwords.VerifyPassword(user, currentPassword, user.password_hash))
            {
                TempData["ErrorMessage"] = "Current password is incorrect.";
                return RedirectToAction(nameof(Profile));
            }

            user.password_hash = _passwords.HashPassword(newPassword);
            user.updated_at = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your account password has been changed successfully!";
            return RedirectToAction(nameof(Profile));
        }

        // =====================================================
        // FORGOT PASSWORD
        // =====================================================

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please enter your university email address.";
                return View();
            }

            var cleanEmail = email.Trim().ToLowerInvariant();
            var user = await _db.users.FirstOrDefaultAsync(u => u.email.ToLower() == cleanEmail || u.username.ToLower() == cleanEmail);

            if (user != null)
            {
                try
                {
                    // 1. Invalidate any existing unused reset tokens for this user
                    var oldTokens = await _db.auth_tokens
                        .Where(t => t.user_id == user.id && t.token_type == "PASSWORD_RESET" && t.used_at == null)
                        .ToListAsync();

                    foreach (var old in oldTokens)
                    {
                        old.used_at = DateTime.UtcNow;
                    }

                    // 2. Generate cryptographically strong raw token (32 random bytes)
                    var rawToken = GenerateSecureToken();
                    var tokenHash = HashToken(rawToken);

                    // 3. Store SHA-256 hash in database with 30-minute expiration
                    var authToken = new auth_token
                    {
                        user_id = user.id,
                        token_hash = tokenHash,
                        token_type = "PASSWORD_RESET",
                        expires_at = DateTime.UtcNow.AddMinutes(30),
                        created_at = DateTime.UtcNow
                    };

                    _db.auth_tokens.Add(authToken);

                    // 4. Record audit trail
                    _db.audit_logs.Add(new audit_log
                    {
                        user_id = user.id,
                        action = "PASSWORD_RESET_REQUESTED",
                        entity_type = "USER",
                        entity_id = user.id,
                        description = $"Password reset token generated and dispatched for user {user.username}",
                        ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                        user_agent = Request.Headers["User-Agent"].ToString(),
                        created_at = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync();

                    // 5. Construct secure reset link and dispatch via IEmailSender
                    var resetUrl = Url.Action("ResetPassword", "Account", new { token = rawToken, email = user.email }, Request.Scheme);
                    if (!string.IsNullOrEmpty(resetUrl))
                    {
                        await _emailSender.SendEmailAsync(
                            user.email,
                            "HUCEMS Account Password Reset Request",
                            $"<h3>Hawassa University Event Management System</h3><p>Hello {user.first_name},</p><p>We received a request to reset your password. Please click the link below to set a new password:</p><p><a href='{resetUrl}'><strong>Reset My Password</strong></a></p><p>This link expires in 30 minutes. If you did not request this, please ignore this email.</p>");
                    }

                    // In local development, provide token in TempData for manual testing without an SMTP server
                    if (_env.IsDevelopment())
                    {
                        TempData["DevResetLink"] = resetUrl;
                        TempData["DevRawToken"] = rawToken;
                        TempData["ResetEmail"] = user.email;
                        TempData["ResetToken"] = rawToken;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing password reset token generation.");
                }
            }

            // Generic anti-enumeration response
            TempData["SuccessMessage"] = "If an account matching that email address exists in the campus directory, a secure password reset link has been dispatched to your inbox.";
            return RedirectToAction(nameof(ResetPassword));
        }


        // =====================================================
        // RESET PASSWORD
        // =====================================================

        // GET: /Account/ResetPassword
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? token = null, string? email = null)
        {
            var rawToken = token ?? TempData["ResetToken"] as string;
            var accountEmail = email ?? TempData["ResetEmail"] as string;

            if (!string.IsNullOrWhiteSpace(rawToken))
            {
                var tokenHash = HashToken(rawToken);
                var tokenRecord = await _db.auth_tokens
                    .Include(t => t.user)
                    .FirstOrDefaultAsync(t => t.token_hash == tokenHash && t.token_type == "PASSWORD_RESET");

                if (tokenRecord == null)
                {
                    ViewBag.Error = "Invalid reset token. Please request a new password reset link.";
                }
                else if (tokenRecord.used_at != null)
                {
                    ViewBag.Error = "This password reset token has already been used. Please request a new one.";
                }
                else if (tokenRecord.expires_at <= DateTime.UtcNow)
                {
                    ViewBag.Error = "This password reset token has expired (30-minute validity exceeded). Please request a new link.";
                }
                else if (tokenRecord.user != null)
                {
                    accountEmail = tokenRecord.user.email;
                }
            }

            ViewBag.Token = rawToken;
            ViewBag.Email = accountEmail;
            return View();
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            string email,
            string? token,
            string password,
            string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please specify your university account email address.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Error = "A valid security reset token is required. Please check your reset link or request a new one.";
                ViewBag.Email = email;
                return View();
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                ViewBag.Error = "New password must be at least 8 characters long.";
                ViewBag.Email = email;
                ViewBag.Token = token;
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                ViewBag.Email = email;
                ViewBag.Token = token;
                return View();
            }

            var tokenHash = HashToken(token);
            var cleanEmail = email.Trim().ToLowerInvariant();

            var tokenRecord = await _db.auth_tokens
                .Include(t => t.user)
                .FirstOrDefaultAsync(t => t.token_hash == tokenHash && t.token_type == "PASSWORD_RESET");

            if (tokenRecord == null)
            {
                ViewBag.Error = "Security Verification Failed: The provided token is invalid.";
                ViewBag.Email = email;
                return View();
            }

            if (tokenRecord.used_at != null)
            {
                ViewBag.Error = "Security Verification Failed: This reset token has already been consumed.";
                ViewBag.Email = email;
                return View();
            }

            if (tokenRecord.expires_at <= DateTime.UtcNow)
            {
                ViewBag.Error = "Security Verification Failed: This reset token has expired.";
                ViewBag.Email = email;
                return View();
            }

            var user = tokenRecord.user;
            if (user == null || (!string.Equals(user.email.Trim(), cleanEmail, StringComparison.OrdinalIgnoreCase) && !string.Equals(user.username.Trim(), cleanEmail, StringComparison.OrdinalIgnoreCase)))
            {
                ViewBag.Error = "Security Verification Failed: The reset token does not match the specified account email.";
                ViewBag.Email = email;
                ViewBag.Token = token;
                return View();
            }

            // Invalidate token
            tokenRecord.used_at = DateTime.UtcNow;

            // Hash new password with PBKDF2
            user.password_hash = _passwords.HashPassword(password);
            user.updated_at = DateTime.UtcNow;

            try
            {
                _db.audit_logs.Add(new audit_log
                {
                    user_id = user.id,
                    action = "PASSWORD_RESET_SUCCESS",
                    entity_type = "USER",
                    entity_id = user.id,
                    description = $"Password successfully updated and verified via secure token for {user.username}",
                    ip_address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    user_agent = Request.Headers["User-Agent"].ToString(),
                    created_at = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Your password has been successfully updated and verified. Please sign in with your new credentials.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist new password hash to database.");
                ViewBag.Error = "An internal error occurred while updating your password. Please try again.";
                ViewBag.Email = email;
                ViewBag.Token = token;
                return View();
            }
        }

        // =====================================================
        // PERSONALIZATION & NOTIFICATION PREFERENCES
        // =====================================================

        // GET: /Account/Preferences
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Preferences()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !ulong.TryParse(userIdStr, out ulong uid))
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _db.users
                .Include(u => u.department)
                .FirstOrDefaultAsync(u => u.id == uid);

            if (user == null) return NotFound();

            var vm = new PersonalizationPreferencesViewModel
            {
                UserId = user.id,
                UserName = $"{user.first_name} {user.last_name}".Trim(),
                UserEmail = user.email,
                UserRole = user.account_type ?? "Student",
                PrimaryDepartmentName = user.department?.name
            };

            // 1. Load User's Category Interests
            var userInterests = await _db.user_category_interests
                .Where(i => i.user_id == uid)
                .ToDictionaryAsync(i => i.category_id);

            var allCategories = await _db.event_categories
                .Include(c => c._events)
                .Where(c => c.is_active == true)
                .OrderBy(c => c.name)
                .ToListAsync();

            vm.Categories = allCategories.Select(c =>
            {
                var isSelected = userInterests.TryGetValue(c.id, out var userInt);
                return new CategoryInterestItemViewModel
                {
                    CategoryId = c.id,
                    InterestId = isSelected ? userInt?.interest_id : null,
                    CategoryName = c.name,
                    Description = c.description,
                    Icon = c.icon,
                    ColorHex = null,
                    IsSelected = isSelected,
                    InterestLevel = isSelected && userInt != null ? userInt.interest_level : "MEDIUM",
                    CreatedAt = isSelected && userInt != null ? userInt.created_at : null,
                    AssociatedEventsCount = c._events.Count(e => e.status == "PUBLISHED")
                };
            }).ToList();

            // 2. Load User's Department Subscriptions
            var userDeptSubs = await _db.user_dept_subscriptions
                .Where(s => s.user_id == uid)
                .ToDictionaryAsync(s => s.department_id);

            var allDepts = await _db.departments
                .Include(d => d.faculty)
                .Include(d => d.users)
                    .ThenInclude(u => u._eventorganizers)
                .Where(d => d.is_active == true)
                .OrderBy(d => d.name)
                .ToListAsync();

            vm.DepartmentSubscriptions = allDepts.Select(d =>
            {
                var isSubbed = userDeptSubs.TryGetValue(d.id, out var sub);
                return new DepartmentSubscriptionItemViewModel
                {
                    SubId = isSubbed ? sub?.sub_id : null,
                    DepartmentId = d.id,
                    DepartmentName = d.name,
                    DepartmentCode = d.code,
                    FacultyName = d.faculty?.name,
                    Building = null,
                    IsSubscribed = isSubbed,
                    NotifyOnNewEvent = isSubbed ? (sub?.notify_on_new_event ?? true) : true,
                    SubscribedAt = isSubbed ? sub?.subscribed_at : null,
                    ActiveEventsCount = d.users.SelectMany(u => u._eventorganizers).Count(e => e.status == "PUBLISHED" && e.start_at >= DateTime.UtcNow)
                };
            }).ToList();

            return View(vm);
        }

        // POST: /Account/SaveCategoryInterests
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCategoryInterests([FromBody] SaveInterestsRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !ulong.TryParse(userIdStr, out ulong uid))
            {
                return Json(new { success = false, message = "User session expired." });
            }

            try
            {
                var existing = await _db.user_category_interests
                    .Where(i => i.user_id == uid)
                    .ToListAsync();

                _db.user_category_interests.RemoveRange(existing);

                if (request.CategoryIds != null && request.CategoryIds.Any())
                {
                    foreach (var catId in request.CategoryIds.Distinct())
                    {
                        _db.user_category_interests.Add(new user_category_interest
                        {
                            user_id = uid,
                            category_id = catId,
                            interest_level = !string.IsNullOrWhiteSpace(request.InterestLevel) ? request.InterestLevel : "HIGH",
                            created_at = DateTime.UtcNow
                        });
                    }
                }

                await _db.SaveChangesAsync();
                return Json(new { success = true, count = request.CategoryIds?.Count ?? 0, message = "Category interests saved successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving category interests for user {UserId}", uid);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Account/ToggleDepartmentSubscription
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDepartmentSubscription([FromBody] DeptSubscriptionToggleRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !ulong.TryParse(userIdStr, out ulong uid))
            {
                return Json(new { success = false, message = "User session expired." });
            }

            try
            {
                var dept = await _db.departments.FindAsync(request.DepartmentId);
                if (dept == null)
                {
                    return Json(new { success = false, message = "Department not found." });
                }

                var existingSub = await _db.user_dept_subscriptions
                    .FirstOrDefaultAsync(s => s.user_id == uid && s.department_id == request.DepartmentId);

                bool isSubscribed;
                bool notifyOnNewEvent = true;

                if (existingSub != null)
                {
                    _db.user_dept_subscriptions.Remove(existingSub);
                    isSubscribed = false;
                }
                else
                {
                    notifyOnNewEvent = request.NotifyOnNewEvent ?? true;
                    var newSub = new user_dept_subscription
                    {
                        user_id = uid,
                        department_id = request.DepartmentId,
                        notify_on_new_event = notifyOnNewEvent,
                        subscribed_at = DateTime.UtcNow
                    };
                    _db.user_dept_subscriptions.Add(newSub);
                    isSubscribed = true;
                }

                await _db.SaveChangesAsync();
                var totalSubs = await _db.user_dept_subscriptions.CountAsync(s => s.user_id == uid);

                return Json(new
                {
                    success = true,
                    isSubscribed,
                    notifyOnNewEvent,
                    totalSubscribedCount = totalSubs,
                    departmentName = dept.name,
                    message = isSubscribed
                        ? $"Subscribed to '{dept.name}'. Push alerts for new events are ACTIVE."
                        : $"Unsubscribed from '{dept.name}'."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling department subscription for user {UserId}", uid);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Account/ToggleDepartmentAlert
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDepartmentAlert([FromBody] DeptSubscriptionToggleRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !ulong.TryParse(userIdStr, out ulong uid))
            {
                return Json(new { success = false, message = "User session expired." });
            }

            try
            {
                var existingSub = await _db.user_dept_subscriptions
                    .Include(s => s.department)
                    .FirstOrDefaultAsync(s => s.user_id == uid && s.department_id == request.DepartmentId);

                if (existingSub == null)
                {
                    return Json(new { success = false, message = "Subscription record not found. Please subscribe to this department first." });
                }

                existingSub.notify_on_new_event = request.NotifyOnNewEvent ?? !existingSub.notify_on_new_event;
                await _db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    notifyOnNewEvent = existingSub.notify_on_new_event,
                    departmentName = existingSub.department?.name,
                    message = existingSub.notify_on_new_event
                        ? $"Push alerts turned ON for {existingSub.department?.name} events."
                        : $"Push alerts muted for {existingSub.department?.name} events."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alert status for user {UserId}", uid);
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}