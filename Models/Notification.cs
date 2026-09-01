using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("expires_at", Name = "idx_notifications_expires")]
[Index("user_id", "is_read", Name = "idx_notifications_unread")]
[Index("user_id", Name = "idx_notifications_user")]
public partial class Notification
{
    [Key]
    public ulong id { get; set; }

    public ulong user_id { get; set; }

    [StringLength(255)]
    public string title { get; set; } = null!;

    [Column(TypeName = "text")]
    public string message { get; set; } = null!;

    [Column(TypeName = "enum('EVENT','REGISTRATION','REMINDER','ANNOUNCEMENT','SYSTEM','FEEDBACK','CLUB')")]
    public string notification_type { get; set; } = null!;

    [StringLength(100)]
    public string? related_entity_type { get; set; }

    public ulong? related_entity_id { get; set; }

    [StringLength(1000)]
    public string? action_url { get; set; }

    public bool is_read { get; set; }

    [MaxLength(6)]
    public DateTime? read_at { get; set; }

    [MaxLength(6)]
    public DateTime? expires_at { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [ForeignKey("user_id")]
    [InverseProperty("notifications")]
    public virtual User user { get; set; } = null!;
}
