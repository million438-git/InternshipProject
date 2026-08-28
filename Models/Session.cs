using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("expires_at", Name = "idx_sessions_expires")]
[Index("user_id", Name = "idx_sessions_user")]
[Index("session_token_hash", Name = "uq_sessions_token", IsUnique = true)]
public partial class Session
{
    [Key]
    public ulong id { get; set; }

    public ulong user_id { get; set; }

    public string session_token_hash { get; set; } = null!;

    [StringLength(45)]
    public string? ip_address { get; set; }

    [StringLength(500)]
    public string? user_agent { get; set; }

    [StringLength(255)]
    public string? device_name { get; set; }

    [MaxLength(6)]
    public DateTime started_at { get; set; }

    [MaxLength(6)]
    public DateTime last_activity_at { get; set; }

    [MaxLength(6)]
    public DateTime expires_at { get; set; }

    [MaxLength(6)]
    public DateTime? revoked_at { get; set; }

    [ForeignKey("user_id")]
    [InverseProperty("sessions")]
    public virtual User user { get; set; } = null!;
}
