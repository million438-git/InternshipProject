using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("account_type", Name = "idx_users_account_type")]
[Index("department_id", Name = "idx_users_department")]
[Index("account_status", Name = "idx_users_status")]
[Index("email", Name = "uq_users_email", IsUnique = true)]
[Index("employee_id", Name = "uq_users_employee_id", IsUnique = true)]
[Index("student_id", Name = "uq_users_student_id", IsUnique = true)]
[Index("username", Name = "uq_users_username", IsUnique = true)]
public partial class User
{
    [Key]
    public ulong id { get; set; }

    public ulong? department_id { get; set; }

    [StringLength(100)]
    public string username { get; set; } = null!;

    public string email { get; set; } = null!;

    [StringLength(255)]
    public string password_hash { get; set; } = null!;

    [StringLength(100)]
    public string first_name { get; set; } = null!;

    [StringLength(100)]
    public string? middle_name { get; set; }

    [StringLength(100)]
    public string last_name { get; set; } = null!;

    [StringLength(100)]
    public string? student_id { get; set; }

    [StringLength(100)]
    public string? employee_id { get; set; }

    [StringLength(50)]
    public string? phone { get; set; }

    [StringLength(1000)]
    public string? profile_image_url { get; set; }

    [Column(TypeName = "text")]
    public string? bio { get; set; }

    [Column(TypeName = "enum('STUDENT','STAFF','FACULTY','ORGANIZATION')")]
    public string account_type { get; set; } = null!;

    [Column(TypeName = "enum('PENDING','ACTIVE','SUSPENDED','LOCKED','INACTIVE')")]
    public string account_status { get; set; } = null!;

    public bool email_verified { get; set; }

    public bool phone_verified { get; set; }

    [MaxLength(6)]
    public DateTime? last_login_at { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [InverseProperty("approved_byNavigation")]
    public virtual ICollection<_event> _eventapproved_byNavigations { get; set; } = new List<_event>();

    [InverseProperty("organizer")]
    public virtual ICollection<_event> _eventorganizers { get; set; } = new List<_event>();

    [InverseProperty("author")]
    public virtual ICollection<Announcement> announcements { get; set; } = new List<Announcement>();

    [InverseProperty("user")]
    public virtual ICollection<audit_log> audit_logs { get; set; } = new List<audit_log>();

    [InverseProperty("user")]
    public virtual ICollection<auth_token> auth_tokens { get; set; } = new List<auth_token>();

    [ForeignKey("department_id")]
    [InverseProperty("users")]
    public virtual Department? department { get; set; }

    [InverseProperty("user")]
    public virtual ICollection<event_comment> event_comments { get; set; } = new List<event_comment>();

    [InverseProperty("user")]
    public virtual ICollection<event_feedback> event_feedbacks { get; set; } = new List<event_feedback>();

    [InverseProperty("user")]
    public virtual ICollection<Notification> notifications { get; set; } = new List<Notification>();

    [InverseProperty("user")]
    public virtual ICollection<organization_member> organization_members { get; set; } = new List<organization_member>();

    [InverseProperty("user")]
    public virtual ICollection<Registration> registrations { get; set; } = new List<Registration>();

    [InverseProperty("user")]
    public virtual ICollection<Session> sessions { get; set; } = new List<Session>();

    [InverseProperty("user")]
    public virtual ICollection<user_category_interest> user_category_interests { get; set; } = new List<user_category_interest>();

    [InverseProperty("user")]
    public virtual ICollection<user_dept_subscription> user_dept_subscriptions { get; set; } = new List<user_dept_subscription>();

    [InverseProperty("user")]
    public virtual user_preference? user_preference { get; set; }

    [InverseProperty("assigned_byNavigation")]
    public virtual ICollection<user_role> user_roleassigned_byNavigations { get; set; } = new List<user_role>();

    [InverseProperty("user")]
    public virtual ICollection<user_role> user_roleusers { get; set; } = new List<user_role>();
}
