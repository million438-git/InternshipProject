using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Models;
using HawassaUnifiedCampusEventManagementSystem.Services;

namespace HawassaUnifiedCampusEventManagementSystem.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthApiController> _logger;
        private readonly IPasswordService _passwords;

        public AuthApiController(
            ApplicationDbContext db,
            IConfiguration configuration,
            ILogger<AuthApiController> logger,
            IPasswordService passwords)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
            _passwords = passwords;
        }

        // =====================================================================
        // 1. POST /api/auth/login - JSON API Login (Issues JWT & optional Cookie)
        // =====================================================================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiLoginRequest request)
        {
            return await AuthenticateAndIssueTokenAsync(request, issueCookie: true);
        }

        // =====================================================================
        // 2. POST /api/auth/token - Dedicated JWT Bearer Token Endpoint
        // =====================================================================
        [HttpPost("token")]
        public async Task<IActionResult> Token([FromBody] ApiLoginRequest request)
        {
            return await AuthenticateAndIssueTokenAsync(request, issueCookie: false);
        }

        private async Task<IActionResult> AuthenticateAndIssueTokenAsync(ApiLoginRequest request, bool issueCookie)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "Email/Username and Password are required." });
            }

            var identifier = request.Email.Trim();

            try
            {
                var dbUser = await _db.users
                    .Include(u => u.user_roleusers)
                        .ThenInclude(ur => ur.role)
                    .Include(u => u.department)
                    .FirstOrDefaultAsync(u => u.email.ToLower() == identifier.ToLower() || u.username.ToLower() == identifier.ToLower());

                if (dbUser == null || !_passwords.VerifyPassword(dbUser, request.Password, dbUser.password_hash))
                {
                    return Unauthorized(new { success = false, message = "Invalid email/username or password." });
                }

                if (dbUser.account_status == "PENDING" || dbUser.account_status == "PENDING_APPROVAL")
                {
                    return StatusCode(403, new { success = false, message = "Your account is pending SuperAdmin approval before activation." });
                }

                if (dbUser.account_status == "SUSPENDED" || dbUser.account_status == "LOCKED" || dbUser.account_status == "INACTIVE")
                {
                    return StatusCode(403, new { success = false, message = "Account is inactive, suspended, or locked. Please contact campus administration." });
                }

                if (dbUser.account_status != "ACTIVE")
                {
                    return StatusCode(403, new { success = false, message = "Account is not active." });
                }

                dbUser.last_login_at = DateTime.UtcNow;
                if (_passwords.IsLegacyHash(dbUser.password_hash))
                {
                    dbUser.password_hash = _passwords.HashPassword(request.Password);
                }
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "API Auth: Could not update last login or rehash password.");
                }

                // Resolve user role
                var role = ResolveRole(dbUser);

                // Generate JWT Token
                var tokenString = GenerateJwtToken(dbUser, role, out DateTime expiresAt);

                // Sign in with cookie session if requested
                if (issueCookie)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, dbUser.id.ToString()),
                        new Claim(ClaimTypes.Name, $"{dbUser.first_name} {dbUser.last_name}".Trim()),
                        new Claim(ClaimTypes.Email, dbUser.email),
                        new Claim(ClaimTypes.Role, role)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
                }

                return Ok(new
                {
                    success = true,
                    message = "Authentication successful.",
                    token = tokenString,
                    tokenType = "Bearer",
                    expiresAt = expiresAt,
                    user = new
                    {
                        id = dbUser.id,
                        fullName = $"{dbUser.first_name} {dbUser.last_name}".Trim(),
                        username = dbUser.username,
                        email = dbUser.email,
                        role = role,
                        accountType = dbUser.account_type,
                        studentId = dbUser.student_id,
                        employeeId = dbUser.employee_id,
                        department = dbUser.department?.name ?? "Campus Member",
                        avatar = dbUser.profile_image_url
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Auth: Authentication error");
                return StatusCode(500, new { success = false, message = "An internal server error occurred." });
            }
        }

        private string GenerateJwtToken(User user, string role, out DateTime expiresAt)
        {
            var jwtConfig = _configuration.GetSection("Jwt");
            var secretKey = jwtConfig["SecretKey"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
            {
                throw new InvalidOperationException("Jwt:SecretKey or JWT_SECRET_KEY must be configured and at least 32 characters.");
            }
            var issuer = jwtConfig["Issuer"] ?? "HawassaUnifiedCampusEventManagementSystem";
            var audience = jwtConfig["Audience"] ?? "HawassaUnifiedCampusEventManagementSystem_Clients";
            var expiryMinutes = int.TryParse(jwtConfig["ExpiryMinutes"], out int exp) ? exp : 1440;

            expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.id.ToString()),
                new Claim(ClaimTypes.Name, $"{user.first_name} {user.last_name}".Trim()),
                new Claim(ClaimTypes.Email, user.email),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Sub, user.id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // =====================================================================
        // 3. GET /api/auth/me - Current User Info
        // =====================================================================
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!ulong.TryParse(userIdStr, out ulong uid))
            {
                return Unauthorized(new { success = false, message = "User not authenticated." });
            }

            var dbUser = await _db.users
                .Include(u => u.department)
                .Include(u => u.user_roleusers)
                    .ThenInclude(ur => ur.role)
                .FirstOrDefaultAsync(u => u.id == uid);

            if (dbUser == null)
            {
                return NotFound(new { success = false, message = "User record not found." });
            }

            var role = ResolveRole(dbUser);

            return Ok(new
            {
                success = true,
                user = new
                {
                    id = dbUser.id,
                    fullName = $"{dbUser.first_name} {dbUser.last_name}".Trim(),
                    username = dbUser.username,
                    email = dbUser.email,
                    role = role,
                    accountType = dbUser.account_type,
                    studentId = dbUser.student_id,
                    employeeId = dbUser.employee_id,
                    department = dbUser.department?.name,
                    bio = dbUser.bio,
                    avatar = dbUser.profile_image_url,
                    createdAt = dbUser.created_at
                }
            });
        }

        // =====================================================================
        // 3. POST /api/auth/logout - JSON API Logout
        // =====================================================================
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { success = true, message = "Logged out successfully." });
        }

        private static string ResolveRole(User u)
        {
            if (u.user_roleusers != null && u.user_roleusers.Any())
            {
                foreach (var ur in u.user_roleusers)
                {
                    var rName = ur.role?.name?.Trim();
                    if (string.IsNullOrEmpty(rName)) continue;
                    if (rName.Contains("Super", StringComparison.OrdinalIgnoreCase)) return "SuperAdmin";
                    if (rName.Contains("Admin", StringComparison.OrdinalIgnoreCase)) return "Admin";
                    if (rName.Contains("Faculty", StringComparison.OrdinalIgnoreCase)) return "Faculty";
                    if (rName.Contains("Staff", StringComparison.OrdinalIgnoreCase)) return "Staff";
                    if (rName.Contains("Org", StringComparison.OrdinalIgnoreCase) || rName.Contains("Club", StringComparison.OrdinalIgnoreCase)) return "Organization";
                }
            }

            var accType = u.account_type?.Trim().ToUpperInvariant() ?? "STUDENT";
            return accType switch
            {
                "SUPERADMIN" => "SuperAdmin",
                "ADMIN" => "Admin",
                "FACULTY" => "Faculty",
                "STAFF" => "Staff",
                "ORGANIZATION" => "Organization",
                _ => "Student"
            };
        }
    }

    public class ApiLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
