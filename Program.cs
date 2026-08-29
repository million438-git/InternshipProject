using System.Text;
using HawassaUnifiedCampusEventManagementSystem.Data;
using HawassaUnifiedCampusEventManagementSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// 1. SERVICES & APPLICATION HELPERS
// ======================================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IEmailSender, CampusEmailSender>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();

// ======================================================
// 2. DATABASE & CONNECTION RESILIENCY
// ======================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection is not configured. Set ConnectionStrings:DefaultConnection " +
        "(Development: appsettings.Development.json or user secrets) or DATABASE_CONNECTION_STRING.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseMySQL(connectionString);
});

// ======================================================
// 3. AUTHENTICATION & AUTHORIZATION (DUAL COOKIE + JWT)
// ======================================================
var jwtConfig = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtConfig["SecretKey"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "JWT signing key is not configured. Set Jwt:SecretKey (Development) or JWT_SECRET_KEY. " +
        "The key must be at least 32 characters.");
}
var jwtIssuer = jwtConfig["Issuer"] ?? "HawassaUnifiedCampusEventManagementSystem";
var jwtAudience = jwtConfig["Audience"] ?? "HawassaUnifiedCampusEventManagementSystem_Clients";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "HUCEMS.AuthSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
        CookieAuthenticationDefaults.AuthenticationScheme,
        JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

// ======================================================
// 4. ANTI-FORGERY SECURITY CONFIGURATION
// ======================================================
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "HUCEMS.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ======================================================
// 5. FORWARDED HEADERS (FOR NGINX / IIS / DOCKER / CLOUD)
// ======================================================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ======================================================
// 6. MVC & API CONTROLLERS
// ======================================================
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ======================================================
// 7. HTTP REQUEST PIPELINE & SECURITY HEADERS
// ======================================================
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();

// Cache static files in production for optimal performance
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // 30 days cache for static assets with cache-busting tokens
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=2592000");
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ======================================================
// 8. ROUTING CONFIGURATION
// ======================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ======================================================
// 9. AUTOMATIC DATABASE SEEDING
// ======================================================
await DbInitializer.InitializeAsync(app.Services);

app.Run();
