using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("action", Name = "idx_audit_logs_action")]
[Index("created_at", Name = "idx_audit_logs_created")]
[Index("entity_type", "entity_id", Name = "idx_audit_logs_entity")]
[Index("user_id", Name = "idx_audit_logs_user")]
public partial class audit_log
{
    [Key]
    public ulong id { get; set; }

    public ulong? user_id { get; set; }

    [StringLength(150)]
    public string action { get; set; } = null!;

    [StringLength(100)]
    public string? entity_type { get; set; }

    public ulong? entity_id { get; set; }

    [Column(TypeName = "json")]
    public string? old_values { get; set; }

    [Column(TypeName = "json")]
    public string? new_values { get; set; }

    [StringLength(45)]
    public string? ip_address { get; set; }

    [StringLength(500)]
    public string? user_agent { get; set; }

    [Column(TypeName = "text")]
    public string? description { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [ForeignKey("user_id")]
    [InverseProperty("audit_logs")]
    public virtual User? user { get; set; }
}
