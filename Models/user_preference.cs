using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("user_id", Name = "uq_user_preferences_user", IsUnique = true)]
public partial class user_preference
{
    [Key]
    public ulong id { get; set; }

    public ulong user_id { get; set; }

    [Required]
    public bool? email_notifications { get; set; }

    [Required]
    public bool? push_notifications { get; set; }

    public bool sms_notifications { get; set; }

    [Required]
    public bool? event_reminders { get; set; }

    [Required]
    public bool? announcement_notifications { get; set; }

    [Required]
    public bool? career_notifications { get; set; }

    [Required]
    public bool? comment_notifications { get; set; }

    public uint reminder_minutes { get; set; }

    [StringLength(20)]
    public string preferred_language { get; set; } = null!;

    [StringLength(100)]
    public string timezone { get; set; } = null!;

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [ForeignKey("user_id")]
    [InverseProperty("user_preference")]
    public virtual User user { get; set; } = null!;
}
