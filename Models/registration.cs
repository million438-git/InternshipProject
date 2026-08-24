using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("event_id", Name = "idx_registrations_event")]
[Index("status", Name = "idx_registrations_status")]
[Index("user_id", Name = "idx_registrations_user")]
[Index("registration_code", Name = "uq_registration_code", IsUnique = true)]
[Index("event_id", "user_id", Name = "uq_registration_event_user", IsUnique = true)]
[Index("qr_token", Name = "uq_registration_qr_token", IsUnique = true)]
public partial class Registration
{
    [Key]
    public ulong id { get; set; }

    public ulong event_id { get; set; }

    public ulong user_id { get; set; }

    [StringLength(100)]
    public string registration_code { get; set; } = null!;

    public string qr_token { get; set; } = null!;

    [Column(TypeName = "enum('REGISTERED','WAITLISTED','CANCELLED','ATTENDED','NO_SHOW')")]
    public string status { get; set; } = null!;

    [MaxLength(6)]
    public DateTime registered_at { get; set; }

    [MaxLength(6)]
    public DateTime? cancelled_at { get; set; }

    [MaxLength(6)]
    public DateTime? checked_in_at { get; set; }

    [Column(TypeName = "enum('QR','MANUAL','SYSTEM')")]
    public string? check_in_method { get; set; }

    [StringLength(1000)]
    public string? notes { get; set; }

    [ForeignKey("event_id")]
    [InverseProperty("registrations")]
    public virtual _event _event { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("registrations")]
    public virtual User user { get; set; } = null!;
}
