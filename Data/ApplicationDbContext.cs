using System;
using System.Collections.Generic;
using HawassaUnifiedCampusEventManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<_event> events { get; set; }

    public virtual DbSet<Announcement> announcements { get; set; }

    public virtual DbSet<audit_log> audit_logs { get; set; }

    public virtual DbSet<auth_token> auth_tokens { get; set; }

    public virtual DbSet<class_schedule> class_schedules { get; set; }

    public virtual DbSet<Department> departments { get; set; }

    public virtual DbSet<event_category> event_categories { get; set; }

    public virtual DbSet<event_comment> event_comments { get; set; }

    public virtual DbSet<event_feedback> event_feedbacks { get; set; }

    public virtual DbSet<event_tag> event_tags { get; set; }

    public virtual DbSet<Faculty> faculties { get; set; }

    public virtual DbSet<Notification> notifications { get; set; }

    public virtual DbSet<Organization> organizations { get; set; }

    public virtual DbSet<organization_member> organization_members { get; set; }

    public virtual DbSet<Permission> permissions { get; set; }

    public virtual DbSet<Registration> registrations { get; set; }

    public virtual DbSet<Role> roles { get; set; }

    public virtual DbSet<role_permission> role_permissions { get; set; }

    public virtual DbSet<Session> sessions { get; set; }

    public virtual DbSet<User> users { get; set; }

    public virtual DbSet<user_category_interest> user_category_interests { get; set; }

    public virtual DbSet<user_dept_subscription> user_dept_subscriptions { get; set; }

    public virtual DbSet<user_preference> user_preferences { get; set; }

    public virtual DbSet<user_role> user_roles { get; set; }

    public virtual DbSet<Venue> venues { get; set; }

    // ============================================================
    // NEW ADMIN / SYSTEM TABLES
    // ============================================================

    public virtual DbSet<EventApproval> event_approvals { get; set; }

    public virtual DbSet<SystemSetting> system_settings { get; set; }

    public virtual DbSet<ContentReport> content_reports { get; set; }

    public virtual DbSet<UserSuspension> user_suspensions { get; set; }

    // ============================================================
    // INTEREST-BASED CLUB & COMMUNITY SYSTEM
    // ============================================================
    public virtual DbSet<Club> clubs { get; set; }
    public virtual DbSet<ClubInterest> club_interests { get; set; }
    public virtual DbSet<ClubFollower> club_followers { get; set; }
    public virtual DbSet<ClubMember> club_members { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Configure options only when not supplied via Dependency Injection
        if (!optionsBuilder.IsConfigured)
        {
            var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION");

            if (!string.IsNullOrWhiteSpace(connStr))
            {
                optionsBuilder.UseMySQL(connStr);
            }
        }
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ============================================================
        // EVENTS
        // ============================================================

        modelBuilder.Entity<_event>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.approval_status)
                .HasDefaultValueSql("'PENDING'");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.event_mode)
                .HasDefaultValueSql("'IN_PERSON'");

            entity.Property(e => e.is_public)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.registration_required)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.status)
                .HasDefaultValueSql("'DRAFT'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.approved_byNavigation)
                .WithMany(p => p._eventapproved_byNavigations)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_events_approved_by");

            entity.HasOne(d => d.category)
                .WithMany(p => p._events)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_events_category");

            entity.HasOne(d => d.organization)
                .WithMany(p => p._events)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_events_organization");

            entity.HasOne(d => d.organizer)
                .WithMany(p => p._eventorganizers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_events_organizer");

            entity.HasOne(d => d.venue)
                .WithMany(p => p._events)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_events_venue");

            entity.HasMany(d => d.tags)
                .WithMany(p => p.events)
                .UsingEntity<Dictionary<string, object>>(
                    "event_tag_map",
                    r => r.HasOne<event_tag>()
                        .WithMany()
                        .HasForeignKey("tag_id")
                        .HasConstraintName("fk_event_tag_map_tag"),

                    l => l.HasOne<_event>()
                        .WithMany()
                        .HasForeignKey("event_id")
                        .HasConstraintName("fk_event_tag_map_event"),

                    j =>
                    {
                        j.HasKey("event_id", "tag_id")
                            .HasName("PRIMARY");

                        j.ToTable("event_tag_map");

                        j.HasIndex(
                            new[] { "tag_id" },
                            "idx_event_tag_map_tag"
                        );
                    });
        });


        // ============================================================
        // ANNOUNCEMENTS
        // ============================================================

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.announcement_type)
                .HasDefaultValueSql("'GENERAL'");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.priority)
                .HasDefaultValueSql("'NORMAL'");

            entity.Property(e => e.status)
                .HasDefaultValueSql("'DRAFT'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.author)
                .WithMany(p => p.announcements)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_announcements_author");

            entity.HasOne(d => d.department)
                .WithMany(p => p.announcements)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_announcements_department");
        });


        // ============================================================
        // AUDIT LOGS
        // ============================================================

        modelBuilder.Entity<audit_log>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.user)
                .WithMany(p => p.audit_logs)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_audit_logs_user");
        });


        // ============================================================
        // AUTH TOKENS
        // ============================================================

        modelBuilder.Entity<auth_token>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.user)
                .WithMany(p => p.auth_tokens)
                .HasConstraintName("fk_auth_tokens_user");
        });


        // ============================================================
        // CALENDAR SYNCS
        // ============================================================



        // ============================================================
        // CLASS SCHEDULES
        // ============================================================

        modelBuilder.Entity<class_schedule>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.department)
                .WithMany(p => p.class_schedules)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_class_schedules_department");
        });


        // ============================================================
        // DEPARTMENTS
        // ============================================================

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.is_active)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.faculty)
                .WithMany(p => p.departments)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_departments_faculty");
        });


        // ============================================================
        // DEVICE TOKENS
        // ============================================================



        // ============================================================
        // EMPLOYERS
        // ============================================================



        // ============================================================
        // EVENT CATEGORIES
        // ============================================================

        modelBuilder.Entity<event_category>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.is_active)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
        });


        // ============================================================
        // EVENT COMMENTS
        // ============================================================

        modelBuilder.Entity<event_comment>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d._event)
                .WithMany(p => p.event_comments)
                .HasConstraintName("fk_event_comments_event");

            entity.HasOne(d => d.parent_comment)
                .WithMany(p => p.Inverseparent_comment)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_event_comments_parent");

            entity.HasOne(d => d.user)
                .WithMany(p => p.event_comments)
                .HasConstraintName("fk_event_comments_user");
        });


        // ============================================================
        // EVENT FEEDBACK
        // ============================================================

        modelBuilder.Entity<event_feedback>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d._event)
                .WithMany(p => p.event_feedbacks)
                .HasConstraintName("fk_event_feedback_event");

            entity.HasOne(d => d.user)
                .WithMany(p => p.event_feedbacks)
                .HasConstraintName("fk_event_feedback_user");
        });


        // ============================================================
        // EVENT TAGS
        // ============================================================

        modelBuilder.Entity<event_tag>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
        });


        // ============================================================
        // FACULTIES
        // ============================================================

        modelBuilder.Entity<Faculty>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.is_active)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
        });


        // ============================================================
        // INTERVIEW BOOKINGS
        // ============================================================



        // ============================================================
        // INTERVIEW SLOTS
        // ============================================================



        // ============================================================
        // JOB POSTINGS
        // ============================================================



        // ============================================================
        // NOTIFICATIONS
        // ============================================================

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.notification_type)
                .HasDefaultValueSql("'SYSTEM'");

            entity.HasOne(d => d.user)
                .WithMany(p => p.notifications)
                .HasConstraintName("fk_notifications_user");
        });


        // ============================================================
        // ORGANIZATIONS
        // ============================================================

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.organization_type)
                .HasDefaultValueSql("'CLUB'");

            entity.Property(e => e.status)
                .HasDefaultValueSql("'PENDING'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.department)
                .WithMany(p => p.organizations)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_organizations_department");
        });


        // ============================================================
        // ORGANIZATION MEMBERS
        // ============================================================

        modelBuilder.Entity<organization_member>(entity =>
        {
            entity.HasKey(e => new
            {
                e.organization_id,
                e.user_id
            }).HasName("PRIMARY");

            entity.Property(e => e.is_active)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.joined_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.membership_role)
                .HasDefaultValueSql("'MEMBER'");

            entity.HasOne(d => d.organization)
                .WithMany(p => p.organization_members)
                .HasConstraintName("fk_org_members_organization");

            entity.HasOne(d => d.user)
                .WithMany(p => p.organization_members)
                .HasConstraintName("fk_org_members_user");
        });


        // ============================================================
        // PERMISSIONS
        // ============================================================

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
        });


        // ============================================================
        // POLLS
        // ============================================================



        // ============================================================
        // POLL OPTIONS
        // ============================================================



        // ============================================================
        // POLL RESPONSES
        // ============================================================



        // ============================================================
        // REGISTRATIONS
        // ============================================================

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.registered_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.status)
                .HasDefaultValueSql("'REGISTERED'");

            entity.HasOne(d => d._event)
                .WithMany(p => p.registrations)
                .HasConstraintName("fk_registrations_event");

            entity.HasOne(d => d.user)
                .WithMany(p => p.registrations)
                .HasConstraintName("fk_registrations_user");
        });


        // ============================================================
        // ROLES
        // ============================================================

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.is_system_role)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
        });


        // ============================================================
        // ROLE PERMISSIONS
        // ============================================================

        modelBuilder.Entity<role_permission>(entity =>
        {
            entity.HasKey(e => new
            {
                e.role_id,
                e.permission_id
            }).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.permission)
                .WithMany(p => p.role_permissions)
                .HasConstraintName("fk_role_permissions_permission");

            entity.HasOne(d => d.role)
                .WithMany(p => p.role_permissions)
                .HasConstraintName("fk_role_permissions_role");
        });


        // ============================================================
        // SESSIONS
        // ============================================================

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.last_activity_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.started_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.user)
                .WithMany(p => p.sessions)
                .HasConstraintName("fk_sessions_user");
        });


        // ============================================================
        // STUDY GROUPS
        // ============================================================



        // ============================================================
        // STUDY GROUP MEMBERS
        // ============================================================



        // ============================================================
        // USERS
        // ============================================================

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.account_status)
                .HasDefaultValueSql("'PENDING'");

            entity.Property(e => e.account_type)
                .HasDefaultValueSql("'STUDENT'");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.department)
                .WithMany(p => p.users)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_users_department");
        });


        // ============================================================
        // USER CATEGORY INTERESTS
        // ============================================================

        modelBuilder.Entity<user_category_interest>(entity =>
        {
            entity.HasKey(e => e.interest_id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.interest_level)
                .HasDefaultValueSql("'MEDIUM'");

            entity.HasOne(d => d.category)
                .WithMany(p => p.user_category_interests)
                .HasForeignKey(d => d.category_id)
                .HasConstraintName("fk_user_category_category");

            entity.HasOne(d => d.user)
                .WithMany(p => p.user_category_interests)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_user_category_user");
        });


        // ============================================================
        // USER DEPARTMENT SUBSCRIPTIONS
        // ============================================================

        modelBuilder.Entity<user_dept_subscription>(entity =>
        {
            entity.HasKey(e => e.sub_id).HasName("PRIMARY");

            entity.Property(e => e.notify_on_new_event)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.subscribed_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.department)
                .WithMany(p => p.user_dept_subscriptions)
                .HasForeignKey(d => d.department_id)
                .HasConstraintName("fk_user_dept_sub_department");

            entity.HasOne(d => d.user)
                .WithMany(p => p.user_dept_subscriptions)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_user_dept_sub_user");
        });


        // ============================================================
        // USER PREFERENCES
        // ============================================================

        modelBuilder.Entity<user_preference>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.announcement_notifications)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.career_notifications)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.comment_notifications)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.email_notifications)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.event_reminders)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.preferred_language)
                .HasDefaultValueSql("'en'");

            entity.Property(e => e.push_notifications)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.reminder_minutes)
                .HasDefaultValueSql("'30'");

            entity.Property(e => e.timezone)
                .HasDefaultValueSql("'Africa/Addis_Ababa'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.user)
                .WithOne(p => p.user_preference)
                .HasConstraintName("fk_user_preferences_user");
        });


        // ============================================================
        // USER ROLES
        // ============================================================

        modelBuilder.Entity<user_role>(entity =>
        {
            entity.HasKey(e => new
            {
                e.user_id,
                e.role_id
            }).HasName("PRIMARY");

            entity.Property(e => e.assigned_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.assigned_byNavigation)
                .WithMany(p => p.user_roleassigned_byNavigations)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_user_roles_assigned_by");

            entity.HasOne(d => d.role)
                .WithMany(p => p.user_roles)
                .HasConstraintName("fk_user_roles_role");

            entity.HasOne(d => d.user)
                .WithMany(p => p.user_roleusers)
                .HasConstraintName("fk_user_roles_user");
        });


        // ============================================================
        // VENUES
        // ============================================================

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.capacity)
                .HasDefaultValueSql("'1'");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.status)
                .HasDefaultValueSql("'AVAILABLE'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.venue_type)
                .HasDefaultValueSql("'CLASSROOM'");
        });


        // ============================================================
        // EVENT APPROVALS
        // ============================================================

        modelBuilder.Entity<EventApproval>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.ToTable("event_approvals");

            entity.Property(e => e.id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.action)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.reason);

            entity.Property(e => e.reviewed_at)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.HasOne<_event>()
                .WithMany()
                .HasForeignKey(e => e.event_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_event_approvals_event");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.reviewer_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_event_approvals_reviewer");
        });


        // ============================================================
        // SYSTEM SETTINGS
        // ============================================================

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.ToTable("system_settings");

            entity.Property(e => e.id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.setting_key)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(e => e.setting_key)
                .IsUnique();

            entity.Property(e => e.setting_value);

            entity.Property(e => e.description)
                .HasMaxLength(500);

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.updated_by)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_system_settings_updated_by");
        });


        // ============================================================
        // CONTENT REPORTS
        // ============================================================

        modelBuilder.Entity<ContentReport>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.ToTable("content_reports");

            entity.Property(e => e.id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.content_type)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.reason)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.status)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("PENDING");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.reporter_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_content_reports_reporter");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.reviewed_by)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_content_reports_reviewer");
        });


        // ============================================================
        // USER SUSPENSIONS
        // ============================================================

        modelBuilder.Entity<UserSuspension>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.ToTable("user_suspensions");

            entity.Property(e => e.id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.reason)
                .IsRequired();

            entity.Property(e => e.status)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("ACTIVE");

            entity.Property(e => e.start_date)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.user_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user_suspensions_user");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.suspended_by)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_user_suspensions_admin");
        });


        // ============================================================
        // CLUBS & COMMUNITIES
        // ============================================================

        modelBuilder.Entity<Club>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.status)
                .HasDefaultValueSql("'ACTIVE'");

            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.faculty)
                .WithMany()
                .HasForeignKey(d => d.faculty_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_clubs_faculty");

            entity.HasOne(d => d.department)
                .WithMany()
                .HasForeignKey(d => d.department_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_clubs_department");

            entity.HasOne(d => d.organization)
                .WithMany()
                .HasForeignKey(d => d.organization_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_clubs_organization");

            entity.HasOne(d => d.president)
                .WithMany()
                .HasForeignKey(d => d.president_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_clubs_president");
        });

        modelBuilder.Entity<ClubInterest>(entity =>
        {
            entity.HasKey(e => new { e.club_id, e.category_id }).HasName("PRIMARY");

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.club)
                .WithMany(p => p.club_interests)
                .HasForeignKey(d => d.club_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_club_interests_club");

            entity.HasOne(d => d.category)
                .WithMany()
                .HasForeignKey(d => d.category_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_club_interests_category");
        });

        modelBuilder.Entity<ClubFollower>(entity =>
        {
            entity.HasKey(e => new { e.club_id, e.user_id }).HasName("PRIMARY");

            entity.Property(e => e.followed_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.club)
                .WithMany(p => p.club_followers)
                .HasForeignKey(d => d.club_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_club_followers_club");

            entity.HasOne(d => d.user)
                .WithMany()
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_club_followers_user");
        });

        modelBuilder.Entity<ClubMember>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity.Property(e => e.applied_at)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.Property(e => e.membership_role)
                .HasDefaultValueSql("'MEMBER'");

            entity.Property(e => e.status)
                .HasDefaultValueSql("'PENDING'");

            entity.HasOne(d => d.club)
                .WithMany(p => p.club_members)
                .HasForeignKey(d => d.club_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_club_members_club");

            entity.HasOne(d => d.user)
                .WithMany()
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_club_members_user");

            entity.HasOne(d => d.reviewer)
                .WithMany()
                .HasForeignKey(d => d.reviewed_by)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_club_members_reviewer");
        });


        // ============================================================
        // FINAL MODEL CONFIGURATION
        // ============================================================

        OnModelCreatingPartial(modelBuilder);
    }


    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
