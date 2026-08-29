using System;
using System.Linq;
using System.Threading.Tasks;
using HawassaUnifiedCampusEventManagementSystem.Models;
using HawassaUnifiedCampusEventManagementSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HawassaUnifiedCampusEventManagementSystem.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();

            try
            {
                // Ensure Roles exist
                var superAdminRole = await db.roles.FirstOrDefaultAsync(r => r.name == "SuperAdmin" || r.name == "SUPERADMIN");
                if (superAdminRole == null)
                {
                    superAdminRole = new Role
                    {
                        name = "SuperAdmin",
                        description = "Full System Administrator with root clearance",
                        is_system_role = true,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    db.roles.Add(superAdminRole);
                    await db.SaveChangesAsync();
                }

                var adminRole = await db.roles.FirstOrDefaultAsync(r => r.name == "Admin" || r.name == "ADMIN");
                if (adminRole == null)
                {
                    adminRole = new Role
                    {
                        name = "Admin",
                        description = "Campus Event & Operations Administrator",
                        is_system_role = true,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    db.roles.Add(adminRole);
                    await db.SaveChangesAsync();
                }

                // 1. SUPERADMIN ACCOUNT
                var superAdminUser = await db.users.FirstOrDefaultAsync(u => u.username == "superadmin" || u.email == "superadmin@hawassa.edu.et");
                if (superAdminUser == null)
                {
                    superAdminUser = new User
                    {
                        username = "superadmin",
                        email = "superadmin@hawassa.edu.et",
                        first_name = "Master",
                        last_name = "SuperAdmin",
                        employee_id = "EMP-SA-001",
                        phone = "+251911000001",
                        account_type = "STAFF",
                        account_status = "ACTIVE",
                        email_verified = true,
                        phone_verified = true,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    superAdminUser.password_hash = passwords.HashPassword("SuperAdmin@2026!");
                    db.users.Add(superAdminUser);
                    await db.SaveChangesAsync();

                    db.user_roles.Add(new user_role
                    {
                        user_id = superAdminUser.id,
                        role_id = superAdminRole.id,
                        assigned_at = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                    logger.LogInformation("Seeded master SuperAdmin account: superadmin@hawassa.edu.et");
                }

                // 2. ADMIN ACCOUNT (Campus Operational Administrator)
                var adminUser = await db.users.FirstOrDefaultAsync(u => u.username == "admin" || u.email == "admin@hawassa.edu.et");
                if (adminUser == null)
                {
                    adminUser = new User
                    {
                        username = "admin",
                        email = "admin@hawassa.edu.et",
                        first_name = "Campus",
                        last_name = "Administrator",
                        employee_id = "EMP-ADM-002",
                        phone = "+251911000002",
                        account_type = "STAFF",
                        account_status = "ACTIVE",
                        email_verified = true,
                        phone_verified = true,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    adminUser.password_hash = passwords.HashPassword("Admin@2026!");
                    db.users.Add(adminUser);
                    await db.SaveChangesAsync();

                    db.user_roles.Add(new user_role
                    {
                        user_id = adminUser.id,
                        role_id = adminRole.id,
                        assigned_at = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                    logger.LogInformation("Seeded campus Admin account: admin@hawassa.edu.et");
                }

                // 3. ENSURE CLUB TABLES EXIST IN MYSQL
                var createClubsSql = @"
CREATE TABLE IF NOT EXISTS clubs (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name VARCHAR(200) NOT NULL,
    slug VARCHAR(220) NOT NULL,
    short_name VARCHAR(100) NULL,
    description TEXT NULL,
    logo_url VARCHAR(1000) NULL,
    cover_image_url VARCHAR(1000) NULL,
    faculty_id BIGINT UNSIGNED NULL,
    department_id BIGINT UNSIGNED NULL,
    organization_id BIGINT UNSIGNED NULL,
    president_id BIGINT UNSIGNED NULL,
    status ENUM('ACTIVE', 'PENDING', 'SUSPENDED', 'INACTIVE') NOT NULL DEFAULT 'ACTIVE',
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_clubs_slug (slug),
    KEY idx_clubs_faculty (faculty_id),
    KEY idx_clubs_dept (department_id),
    KEY idx_clubs_org (organization_id),
    KEY idx_clubs_president (president_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS club_interests (
    club_id BIGINT UNSIGNED NOT NULL,
    category_id BIGINT UNSIGNED NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (club_id, category_id),
    KEY idx_club_interests_category (category_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS club_followers (
    club_id BIGINT UNSIGNED NOT NULL,
    user_id BIGINT UNSIGNED NOT NULL,
    followed_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (club_id, user_id),
    KEY idx_club_followers_user (user_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS club_members (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    club_id BIGINT UNSIGNED NOT NULL,
    user_id BIGINT UNSIGNED NOT NULL,
    membership_role ENUM('MEMBER', 'OFFICER', 'SECRETARY', 'TREASURER', 'PRESIDENT', 'ADMIN') NOT NULL DEFAULT 'MEMBER',
    status ENUM('PENDING', 'APPROVED', 'REJECTED') NOT NULL DEFAULT 'PENDING',
    request_notes TEXT NULL,
    applied_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    reviewed_at DATETIME(6) NULL,
    reviewed_by BIGINT UNSIGNED NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_club_user_member (club_id, user_id),
    KEY idx_club_members_user (user_id),
    KEY idx_club_members_club (club_id)
) ENGINE=InnoDB;";

                await db.Database.ExecuteSqlRawAsync(createClubsSql);

                // 4. SEED RICH INTEREST CATEGORIES
                var defaultCategories = new (string Name, string Slug, string Desc, string Icon)[]
                {
                    ("Artificial Intelligence", "artificial-intelligence", "Machine Learning, Deep Learning, NLP, and AI Systems.", "bi-cpu"),
                    ("Cybersecurity", "cybersecurity", "Ethical Hacking, Network Security, Cryptography, and Defense.", "bi-shield-lock"),
                    ("Programming & Software", "programming-software", "Full-Stack Development, Mobile Apps, Cloud, and Algorithms.", "bi-code-slash"),
                    ("Robotics & IoT", "robotics-iot", "Embedded Systems, Hardware, Microcontrollers, and Automation.", "bi-robot"),
                    ("Data Science & Analytics", "data-science", "Big Data, Statistical Modeling, Python Analytics, and Visualization.", "bi-bar-chart"),
                    ("Sports & Athletics", "sports-athletics", "Football, Basketball, Athletics, and Campus Tournaments.", "bi-trophy"),
                    ("Arts & Culture", "arts-culture", "Music, Theater, Heritage, Photography, and Visual Arts.", "bi-palette"),
                    ("Debate & Public Speaking", "debate-public-speaking", "Model UN, Eloquence, Toastmasters, and Policy Debates.", "bi-megaphone"),
                    ("Entrepreneurship & Business", "entrepreneurship-business", "Startups, Venture Incubation, Tech Business, and Innovation.", "bi-lightbulb"),
                    ("Health & Medicine", "health-medicine", "Public Health, Biomedical Sciences, and Clinical Outreach.", "bi-heart-pulse")
                };

                foreach (var cat in defaultCategories)
                {
                    var existing = await db.event_categories.FirstOrDefaultAsync(c => c.slug == cat.Slug || c.name == cat.Name);
                    if (existing == null)
                    {
                        db.event_categories.Add(new event_category
                        {
                            name = cat.Name,
                            slug = cat.Slug,
                            description = cat.Desc,
                            icon = cat.Icon,
                            is_active = true,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        });
                    }
                }
                await db.SaveChangesAsync();

                // 5. SEED INITIAL CLUBS IF EMPTY
                if (!await db.clubs.AnyAsync())
                {
                    var aiCat = await db.event_categories.FirstOrDefaultAsync(c => c.slug == "artificial-intelligence");
                    var cyberCat = await db.event_categories.FirstOrDefaultAsync(c => c.slug == "cybersecurity");
                    var progCat = await db.event_categories.FirstOrDefaultAsync(c => c.slug == "programming-software");
                    var robotCat = await db.event_categories.FirstOrDefaultAsync(c => c.slug == "robotics-iot");
                    var dataCat = await db.event_categories.FirstOrDefaultAsync(c => c.slug == "data-science");
                    var entCat = await db.event_categories.FirstOrDefaultAsync(c => c.slug == "entrepreneurship-business");

                    var csDept = await db.departments.FirstOrDefaultAsync();

                    // Club 1: AI & ML Club
                    var aiClub = new Club
                    {
                        name = "AI & Machine Learning Club",
                        slug = "ai-machine-learning-club",
                        short_name = "AIML-HU",
                        description = "Hawassa University's premier community for Artificial Intelligence, Neural Networks, Computer Vision, and Generative Models. We host weekly hands-on workshops and Kaggle hackathons.",
                        logo_url = "https://images.unsplash.com/photo-1677442136019-21780efad99a?w=400&auto=format&fit=crop&q=80",
                        department_id = csDept?.id,
                        president_id = adminUser?.id,
                        status = "ACTIVE",
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    db.clubs.Add(aiClub);

                    // Club 2: Cybersecurity Guild
                    var cyberClub = new Club
                    {
                        name = "Hawassa Cybersecurity & Ethical Hacking Guild",
                        slug = "hawassa-cybersecurity-guild",
                        short_name = "HUCyber",
                        description = "Dedicated to ethical hacking, Capture The Flag (CTF) competitions, reverse engineering, web security forensics, and cyber defense training across Ethiopian universities.",
                        logo_url = "https://images.unsplash.com/photo-1550751827-4bd374c3f58b?w=400&auto=format&fit=crop&q=80",
                        department_id = csDept?.id,
                        president_id = adminUser?.id,
                        status = "ACTIVE",
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    db.clubs.Add(cyberClub);

                    // Club 3: Campus Coding Society
                    var codeClub = new Club
                    {
                        name = "Campus Coding & Open Source Society",
                        slug = "campus-coding-society",
                        short_name = "HU-Code",
                        description = "Empowering students in modern software engineering, web architectures, mobile app development, and open-source contributions with active mentor sessions.",
                        logo_url = "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=400&auto=format&fit=crop&q=80",
                        department_id = csDept?.id,
                        president_id = adminUser?.id,
                        status = "ACTIVE",
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    db.clubs.Add(codeClub);

                    // Club 4: Robotics & Automation Club
                    var robotClub = new Club
                    {
                        name = "Robotics & IoT Automation Society",
                        slug = "robotics-iot-society",
                        short_name = "HU-Robo",
                        description = "Hardware prototyping, Arduino/Raspberry Pi microcontrollers, drone engineering, and industrial automation project labs.",
                        logo_url = "https://images.unsplash.com/photo-1485827404703-89b55fcc595e?w=400&auto=format&fit=crop&q=80",
                        department_id = csDept?.id,
                        president_id = adminUser?.id,
                        status = "ACTIVE",
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };
                    db.clubs.Add(robotClub);

                    await db.SaveChangesAsync();

                    // Assign Interests to Clubs
                    if (aiCat != null) db.club_interests.Add(new ClubInterest { club_id = aiClub.id, category_id = aiCat.id });
                    if (dataCat != null) db.club_interests.Add(new ClubInterest { club_id = aiClub.id, category_id = dataCat.id });
                    if (progCat != null) db.club_interests.Add(new ClubInterest { club_id = aiClub.id, category_id = progCat.id });

                    if (cyberCat != null) db.club_interests.Add(new ClubInterest { club_id = cyberClub.id, category_id = cyberCat.id });
                    if (progCat != null) db.club_interests.Add(new ClubInterest { club_id = cyberClub.id, category_id = progCat.id });

                    if (progCat != null) db.club_interests.Add(new ClubInterest { club_id = codeClub.id, category_id = progCat.id });
                    if (entCat != null) db.club_interests.Add(new ClubInterest { club_id = codeClub.id, category_id = entCat.id });

                    if (robotCat != null) db.club_interests.Add(new ClubInterest { club_id = robotClub.id, category_id = robotCat.id });
                    if (progCat != null) db.club_interests.Add(new ClubInterest { club_id = robotClub.id, category_id = progCat.id });

                    // Add president as approved member
                    if (adminUser != null)
                    {
                        db.club_members.Add(new ClubMember
                        {
                            club_id = aiClub.id,
                            user_id = adminUser.id,
                            membership_role = "PRESIDENT",
                            status = "APPROVED",
                            applied_at = DateTime.UtcNow,
                            reviewed_at = DateTime.UtcNow,
                            reviewed_by = adminUser.id
                        });
                        db.club_members.Add(new ClubMember
                        {
                            club_id = cyberClub.id,
                            user_id = adminUser.id,
                            membership_role = "PRESIDENT",
                            status = "APPROVED",
                            applied_at = DateTime.UtcNow,
                            reviewed_at = DateTime.UtcNow,
                            reviewed_by = adminUser.id
                        });
                    }

                    await db.SaveChangesAsync();
                    logger.LogInformation("Seeded initial interest-based campus clubs.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("DbInitializer skipped: {Message}", ex.Message);
            }
        }
    }
}
